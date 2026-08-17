using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 게임의 전체 진행 상태(Playing, GameOver, StageCleared)를 총괄 관리하고,
/// 사망/클리어 시 UI 표시 및 'Space' 키 즉시 재시작(Instant Restart)을 제어하는 싱글톤 매니저
/// New Input System과 Legacy Input 모두 완벽 호환됩니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Playing,
        GameOver,
        StageCleared
    }

    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Header("UI References (자동 생성 또는 수동 할당)")]
    [SerializeField] private GameObject uiOverlayPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    public GameState CurrentState => currentState;
    public bool IsPlaying => currentState == GameState.Playing;

    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        // 1. 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 2. 씬 로드 시 고질적인 TimeScale 버그 방지 (무조건 정상 속도 1.0f로 원복)
        Time.timeScale = 1.0f;
        currentState = GameState.Playing;

        // 3. UI 캔버스 및 텍스트 자동 구성
        SetupCanvasUI();
    }

    private void OnEnable()
    {
        PlayerController.OnPlayerDied += OnPlayerDeath;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerDied -= OnPlayerDeath;
    }

    private void Update()
    {
        // GameOver 또는 StageCleared 상태일 때 'Space' 또는 'Enter' 키로 즉시 재시작
        if (currentState == GameState.GameOver || currentState == GameState.StageCleared)
        {
            if (CheckRestartInput())
            {
                RestartCurrentScene();
            }
        }
    }

    /// <summary>
    /// New Input System 및 Legacy Input 호환 재시작 키 감지 (Space / Enter)
    /// </summary>
    private bool CheckRestartInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }

    /// <summary>
    /// 플레이어 사망 시 호출: 타임스케일 정지 및 게임오버 UI 출력
    /// </summary>
    public void OnPlayerDeath()
    {
        if (currentState != GameState.Playing) return;
        currentState = GameState.GameOver;

        Debug.Log("[GameManager] <color=#FF2244>💀 [GAME OVER] 플레이어 사망! [Space] 키를 눌러 즉시 재시작하세요.</color>");

        // 적들의 추가 공격 방지를 위해 시간 정지
        Time.timeScale = 0f;

        // UI 출력
        if (uiOverlayPanel != null) uiOverlayPanel.SetActive(true);
        if (titleText != null)
        {
            titleText.text = "<color=#FF2244>YOU ARE DEAD</color>";
        }
        if (subtitleText != null)
        {
            subtitleText.text = "Press <color=#FFFF00>[Space]</color> to Restart";
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// 스테이지 클리어 시 호출: 축하 UI 출력 및 재시작 대기
    /// </summary>
    public void OnStageClear()
    {
        if (currentState != GameState.Playing) return;
        currentState = GameState.StageCleared;

        Debug.Log("[GameManager] <color=#00FFAA>🎉 [STAGE CLEARED] 스테이지 클리어 완료!</color>");

        if (uiOverlayPanel != null) uiOverlayPanel.SetActive(true);
        if (titleText != null)
        {
            titleText.text = "<color=#00FFAA>STAGE CLEARED!</color>";
        }
        if (subtitleText != null)
        {
            subtitleText.text = "Press <color=#FFFF00>[Space]</color> to Play Again";
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// 현재 씬 즉시 재시작 (핫라인 마이애미식 노 딜레이 리스폰)
    /// </summary>
    public void RestartCurrentScene()
    {
        Debug.Log("[GameManager] 🔄 Instant Restarting Scene...");
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Inspector에 UI가 할당되어 있지 않을 경우 Canvas 및 TMP_Text 자동 생성
    /// </summary>
    private void SetupCanvasUI()
    {
        if (uiOverlayPanel != null && titleText != null)
        {
            uiOverlayPanel.SetActive(false);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("GameUI_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Transform existingOverlay = canvas.transform.Find("GameOverOverlay");
        if (existingOverlay != null)
        {
            uiOverlayPanel = existingOverlay.gameObject;
            titleText = uiOverlayPanel.transform.Find("TitleText")?.GetComponent<TMP_Text>();
            subtitleText = uiOverlayPanel.transform.Find("SubtitleText")?.GetComponent<TMP_Text>();
            uiOverlayPanel.SetActive(false);
            return;
        }

        // 새 Overlay Panel 생성
        GameObject overlay = new GameObject("GameOverOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image bgImage = overlay.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.65f); // 어두운 반투명 배경

        // Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(overlay.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 40);
        titleRect.sizeDelta = new Vector2(800, 100);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 54;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;

        // Subtitle Text
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(overlay.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0, -40);
        subRect.sizeDelta = new Vector2(800, 60);

        subtitleText = subObj.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = 24;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = Color.white;

        uiOverlayPanel = overlay;
        uiOverlayPanel.SetActive(false);
    }
}
