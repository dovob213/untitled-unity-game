#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 유니티 에디터 상단 메뉴에서 클릭 한 번으로
/// 프리팹 생성, 씬 오브젝트 배치, 컴포넌트 연결을 100% 자동 세팅해주는 개발 도구
/// </summary>
public static class QuickSetupTool
{
    [MenuItem("Tools/⚡ 1-Click Game Setup (Wave Mode)", false, 1)]
    public static void SetupGameSceneAndPrefabs()
    {
        Debug.Log("<color=#00FFAA>========================================</color>");
        Debug.Log("<color=#00FFAA>[QuickSetup] ⚡ 1-Click 자동 세팅 시작...</color>");

        // 1. Prefabs 디렉토리 확인
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
        }

        // 2. Enemy 프리팹 자동 생성/갱신
        GameObject enemyPrefab = SetupEnemyPrefab();

        // 3. 씬의 @Managers 생성 및 세팅 (InputManager, WaveManager, AlarmUIManager)
        SetupManagers(enemyPrefab);

        // 4. 씬의 Player 생성 및 세팅
        SetupPlayer();

        // 5. 씬 정리 (기존 수동 배치된 Enemy 정리 및 조명/카메라 확인)
        CleanupAndVerifyScene();

        // 6. 씬 변경사항 저장 마킹
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00FFAA>[QuickSetup] ✅ 1-Click 자동 셋업 완료! 바로 Play(▶)를 누르세요.</color>");
        Debug.Log("<color=#00FFAA>========================================</color>");

        EditorUtility.DisplayDialog("1-Click Game Setup", "✅ 웨이브 모드 세팅이 완료되었습니다!\nPlay(▶)를 누르면 바로 웨이브 전투를 즐길 수 있습니다.", "확인");
    }

    [MenuItem("Tools/🏰 1-Click 2-Room Stage Setup (A방 -> 문 -> B방)", false, 2)]
    public static void SetupTwoRoomStage()
    {
        Debug.Log("<color=#FFAA00>[QuickSetup] 🏰 2개 방(A방 -> 문 -> B방) 스테이지 자동 생성 시작...</color>");

        GameObject enemyPrefab = SetupEnemyPrefab();
        SetupManagers(null); // 방 단위 모드이므로 WaveManager의 자동 스폰은 비활성화
        WaveManager wm = Object.FindFirstObjectByType<WaveManager>();
        if (wm != null) wm.enabled = false;

        SetupPlayer();
        CleanupAndVerifyScene();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographicSize = 9f; // 2개 방을 한눈에 볼 수 있도록 시야 확대
            cam.transform.position = new Vector3(6f, 0f, -10f);
        }

        // 기존 스테이지 오브젝트 정리
        GameObject oldStage = GameObject.Find("Stage_Root");
        if (oldStage != null) Object.DestroyImmediate(oldStage);

        GameObject stageRoot = new GameObject("Stage_Root");

        // 1. Room A 생성 (좌측 방: X: 0, Y: 0)
        GameObject roomAObj = new GameObject("Room_A");
        roomAObj.transform.SetParent(stageRoot.transform);
        roomAObj.transform.position = Vector3.zero;
        RoomManager roomAMgr = roomAObj.AddComponent<RoomManager>();
        SerializedObject sRoomA = new SerializedObject(roomAMgr);
        sRoomA.FindProperty("roomName").stringValue = "Room A (시작 구역)";
        sRoomA.FindProperty("autoActivateOnStart").boolValue = true;
        sRoomA.ApplyModifiedProperties();

        // Room A 적 2마리 배치 (Q, W)
        GameObject enemyQ = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
        enemyQ.name = "Enemy_Q";
        enemyQ.transform.SetParent(roomAObj.transform);
        enemyQ.transform.position = new Vector3(3f, 2f, 0f);
        enemyQ.GetComponent<Enemy>().Init(KeyCode.Q);

        GameObject enemyW = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
        enemyW.name = "Enemy_W";
        enemyW.transform.SetParent(roomAObj.transform);
        enemyW.transform.position = new Vector3(3f, -2f, 0f);
        enemyW.GetComponent<Enemy>().Init(KeyCode.W);

        // 2. Door A->B 생성 (X: 6, Y: 0)
        GameObject doorObj = new GameObject("Door_AB");
        doorObj.transform.SetParent(stageRoot.transform);
        doorObj.transform.position = new Vector3(6f, 0f, 0f);
        doorObj.transform.localScale = new Vector3(0.6f, 4f, 1f);

        SpriteRenderer doorSr = doorObj.AddComponent<SpriteRenderer>();
        doorSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        doorSr.color = new Color(0.9f, 0.4f, 0.1f); // 주황색 철문
        doorSr.sortingOrder = 2;

        BoxCollider2D doorCol = doorObj.AddComponent<BoxCollider2D>();
        doorCol.size = Vector2.one;

        DoorController doorCtrl = doorObj.AddComponent<DoorController>();
        SerializedObject sDoor = new SerializedObject(doorCtrl);
        sDoor.FindProperty("targetRoom").objectReferenceValue = roomAMgr;
        sDoor.FindProperty("openSlideOffset").vector2Value = new Vector2(0f, 4f);
        sDoor.ApplyModifiedProperties();

        // 3. Room B 생성 (우측 방: X: 12, Y: 0)
        GameObject roomBObj = new GameObject("Room_B");
        roomBObj.transform.SetParent(stageRoot.transform);
        roomBObj.transform.position = new Vector3(12f, 0f, 0f);
        RoomManager roomBMgr = roomBObj.AddComponent<RoomManager>();
        SerializedObject sRoomB = new SerializedObject(roomBMgr);
        sRoomB.FindProperty("roomName").stringValue = "Room B (보스/후속 구역)";
        sRoomB.FindProperty("autoActivateOnStart").boolValue = false;
        sRoomB.ApplyModifiedProperties();

        // Room B 적 2마리 배치 (E, R)
        GameObject enemyE = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
        enemyE.name = "Enemy_E";
        enemyE.transform.SetParent(roomBObj.transform);
        enemyE.transform.position = new Vector3(12f, 2.5f, 0f);
        enemyE.GetComponent<Enemy>().Init(KeyCode.E);

        GameObject enemyR = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
        enemyR.name = "Enemy_R";
        enemyR.transform.SetParent(roomBObj.transform);
        enemyR.transform.position = new Vector3(12f, -2.5f, 0f);
        enemyR.GetComponent<Enemy>().Init(KeyCode.R);

        // 4. Room B 입구 트리거 생성 (X: 7.5, Y: 0)
        GameObject triggerObj = new GameObject("Trigger_RoomB");
        triggerObj.transform.SetParent(stageRoot.transform);
        triggerObj.transform.position = new Vector3(7.5f, 0f, 0f);

        BoxCollider2D trigCol = triggerObj.AddComponent<BoxCollider2D>();
        trigCol.size = new Vector2(3f, 8f);
        trigCol.isTrigger = true;

        RoomTrigger roomTrig = triggerObj.AddComponent<RoomTrigger>();
        SerializedObject sTrig = new SerializedObject(roomTrig);
        sTrig.FindProperty("roomToActivate").objectReferenceValue = roomBMgr;
        sTrig.FindProperty("entranceDoorToClose").objectReferenceValue = doorCtrl;
        sTrig.ApplyModifiedProperties();

        // 5. Stage Exit Trigger 생성 (X: 16, Y: 0 - B방 안쪽 탈출구)
        GameObject exitObj = new GameObject("Stage_Exit");
        exitObj.transform.SetParent(stageRoot.transform);
        exitObj.transform.position = new Vector3(16f, 0f, 0f);
        exitObj.transform.localScale = new Vector3(1.2f, 3f, 1f);

        SpriteRenderer exitSr = exitObj.AddComponent<SpriteRenderer>();
        exitSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        exitSr.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // 잠김 상태: 반투명 그레이
        exitSr.sortingOrder = 1;

        BoxCollider2D exitCol = exitObj.AddComponent<BoxCollider2D>();
        exitCol.isTrigger = true;

        StageExitTrigger exitTrig = exitObj.AddComponent<StageExitTrigger>();
        SerializedObject sExit = new SerializedObject(exitTrig);
        sExit.FindProperty("requiredFinalRoom").objectReferenceValue = roomBMgr;
        sExit.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00FFAA>[QuickSetup] ✅ 2-Room Stage 구성 완료! (A방 적 처치 -> 문 개방 -> B방 진입 시 문 폐쇄 & 전투 -> B방 클리어 후 탈출구 도달)</color>");
        EditorUtility.DisplayDialog("2-Room Stage Setup", "✅ [A방 -> 문 -> B방 -> 탈출구] 스테이지가 생성되었습니다!\nPlay(▶)를 누르면 바로 테스트할 수 있습니다.", "확인");
    }

    private static GameObject SetupEnemyPrefab()
    {
        string prefabPath = "Assets/Prefabs/Enemy.prefab";

        // 임시 Enemy 게임오브젝트 생성
        GameObject tempEnemy = new GameObject("Enemy");
        SpriteRenderer sr = tempEnemy.AddComponent<SpriteRenderer>();

        // 내장 2D 스프라이트(Triangle 또는 Knob) 로드
        Sprite triangleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sr.sprite = triangleSprite;
        sr.color = new Color(1f, 0.35f, 0.35f); // 기본 오렌지레드
        sr.sortingOrder = 1;
        tempEnemy.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        // Enemy 및 FlashEffect 컴포넌트 추가
        Enemy enemyComp = tempEnemy.AddComponent<Enemy>();
        tempEnemy.AddComponent<FlashEffect>();

        // 투사체 프리팹 생성 및 바인딩
        GameObject projPrefab = SetupProjectilePrefab();
        if (projPrefab != null)
        {
            SerializedObject serializedEnemy = new SerializedObject(enemyComp);
            SerializedProperty projProp = serializedEnemy.FindProperty("projectilePrefab");
            if (projProp != null)
            {
                projProp.objectReferenceValue = projPrefab.GetComponent<EnemyProjectile>();
                serializedEnemy.ApplyModifiedProperties();
            }
        }

        // 머리 위 텍스트 자식 오브젝트 생성
        GameObject textObj = new GameObject("KeyText");
        textObj.transform.SetParent(tempEnemy.transform);
        textObj.transform.localPosition = new Vector3(0, 1.2f, 0);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = "Q";
        tmp.fontSize = 5;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.yellow;
        tmp.fontStyle = FontStyles.Bold;
        tmp.sortingOrder = 5;

        // 프리팹 파일로 저장
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempEnemy, prefabPath);
        Object.DestroyImmediate(tempEnemy);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[QuickSetup] Enemy 프리팹 생성 완료: {prefabPath}");
        return savedPrefab;
    }

    private static GameObject SetupProjectilePrefab()
    {
        string projPrefabPath = "Assets/Prefabs/EnemyProjectile.prefab";

        GameObject tempProj = new GameObject("EnemyProjectile");
        tempProj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        SpriteRenderer sr = tempProj.AddComponent<SpriteRenderer>();
        Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sr.sprite = circleSprite;
        sr.color = new Color(1f, 0.25f, 0.15f, 1f); // 붉은색 네온 탄환
        sr.sortingOrder = 3;

        CircleCollider2D col = tempProj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        tempProj.AddComponent<EnemyProjectile>();

        GameObject savedProjPrefab = PrefabUtility.SaveAsPrefabAsset(tempProj, projPrefabPath);
        Object.DestroyImmediate(tempProj);

        return savedProjPrefab;
    }

    private static void SetupManagers(GameObject enemyPrefab)
    {
        GameObject managersObj = GameObject.Find("@Managers");
        if (managersObj == null)
        {
            managersObj = new GameObject("@Managers");
        }

        // InputManager 확인 및 추가
        if (!managersObj.TryGetComponent<InputManager>(out _))
        {
            managersObj.AddComponent<InputManager>();
        }

        // WaveManager 확인 및 추가
        if (!managersObj.TryGetComponent<WaveManager>(out var waveManager))
        {
            waveManager = managersObj.AddComponent<WaveManager>();
        }

        // AlarmUIManager 확인 및 추가
        if (!managersObj.TryGetComponent<AlarmUIManager>(out _))
        {
            managersObj.AddComponent<AlarmUIManager>();
        }

        // CameraShakeManager 확인 및 추가
        if (!managersObj.TryGetComponent<CameraShakeManager>(out _))
        {
            managersObj.AddComponent<CameraShakeManager>();
        }

        // GameManager 확인 및 추가
        if (!managersObj.TryGetComponent<GameManager>(out _))
        {
            managersObj.AddComponent<GameManager>();
        }

        // Enemy Prefab 바인딩
        if (enemyPrefab != null)
        {
            SerializedObject serializedWave = new SerializedObject(waveManager);
            SerializedProperty prefabProp = serializedWave.FindProperty("enemyPrefab");
            prefabProp.objectReferenceValue = enemyPrefab.GetComponent<Enemy>();
            serializedWave.ApplyModifiedProperties();
        }

        Debug.Log("[QuickSetup] @Managers (InputManager, WaveManager, AlarmUIManager, CameraShakeManager, GameManager) 구성 완료");
    }

    private static void SetupPlayer()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj == null)
        {
            playerObj = new GameObject("Player");
        }

        // 혹시 남아있을 수 있는 Missing Script 컴포넌트 자동 정리
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(playerObj);

        playerObj.transform.position = Vector3.zero;
        playerObj.transform.localScale = Vector3.one;

        // 스프라이트 렌더러 (네온 사이언)
        if (!playerObj.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr = playerObj.AddComponent<SpriteRenderer>();
        }
        Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        sr.sprite = squareSprite;
        sr.color = new Color(0f, 0.9f, 1f); // 네온 사이언
        sr.sortingOrder = 2;

        // Collider2D 추가 (트리거 및 충돌 판정용)
        if (!playerObj.TryGetComponent<CircleCollider2D>(out var col))
        {
            col = playerObj.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = false;
        }
        playerObj.tag = "Player";

        // FlashEffect 추가
        if (!playerObj.TryGetComponent<FlashEffect>(out _))
        {
            playerObj.AddComponent<FlashEffect>();
        }

        // PlayerController & SkillModuleSystem
        if (!playerObj.TryGetComponent<PlayerController>(out _))
        {
            playerObj.AddComponent<PlayerController>();
        }

        if (!playerObj.TryGetComponent<SkillModuleSystem>(out _))
        {
            playerObj.AddComponent<SkillModuleSystem>();
        }

        Selection.activeGameObject = playerObj;
        Debug.Log("[QuickSetup] Player (PlayerController, SkillModuleSystem) 구성 완료");
    }

    private static void CleanupAndVerifyScene()
    {
        // 기존 씬에 수동으로 올려둔 Enemy_Q나 Enemy 제거 (WaveManager가 스폰하므로)
        Enemy[] sceneEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include);
        foreach (var enemy in sceneEnemies)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(enemy)) continue;
            Object.DestroyImmediate(enemy.gameObject);
        }

        // 카메라 확인
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0, 0, -10f);
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f); // 레트로 우주 느낌의 딥 다크 네이비
        }

        // Global Light 2D 중복 정리
        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        int globalLightCount = 0;
        foreach (var l in lights)
        {
            if (l.lightType == Light2D.LightType.Global)
            {
                globalLightCount++;
                if (globalLightCount > 1)
                {
                    Object.DestroyImmediate(l.gameObject);
                }
                else
                {
                    l.intensity = 1.0f;
                    l.color = Color.white;
                }
            }
        }
    }
}
#endif
