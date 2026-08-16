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
    [MenuItem("Tools/⚡ 1-Click Game Setup", false, 1)]
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

        EditorUtility.DisplayDialog("1-Click Game Setup", "✅ 세팅이 완벽하게 완료되었습니다!\n상단의 Play(▶) 버튼을 누르면 바로 게임을 플레이할 수 있습니다.", "확인");
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

        // Enemy 컴포넌트 추가
        Enemy enemyComp = tempEnemy.AddComponent<Enemy>();

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

        // Enemy Prefab 바인딩
        if (enemyPrefab != null)
        {
            SerializedObject serializedWave = new SerializedObject(waveManager);
            SerializedProperty prefabProp = serializedWave.FindProperty("enemyPrefab");
            prefabProp.objectReferenceValue = enemyPrefab.GetComponent<Enemy>();
            serializedWave.ApplyModifiedProperties();
        }

        Debug.Log("[QuickSetup] @Managers (InputManager, WaveManager, AlarmUIManager) 구성 완료");
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
