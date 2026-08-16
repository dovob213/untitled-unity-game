using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적에게 발각되어 경보가 울렸을 때 화면 상단에 경보 배너 및 붉은색 경고 연출을 표시하는 UI 매니저
/// </summary>
public class AlarmUIManager : MonoBehaviour
{
    public static AlarmUIManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup alarmBannerGroup;
    [SerializeField] private TMP_Text alarmText;
    [SerializeField] private Image screenVignette;

    [Header("Alarm Settings")]
    [SerializeField] private Color alertColor = new Color(1f, 0.15f, 0.15f, 0.8f);
    [SerializeField] private float flashSpeed = 4f;

    private bool isAlarmActive = false;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // UI 기본 상태 숨김
        if (alarmBannerGroup != null)
        {
            alarmBannerGroup.alpha = 0f;
        }
        if (screenVignette != null)
        {
            screenVignette.color = new Color(alertColor.r, alertColor.g, alertColor.b, 0f);
        }
    }

    private void OnEnable()
    {
        Enemy.OnAlarmTriggered += ShowAlarm;
        Enemy.OnAlarmCleared += HideAlarm;
        WaveManager.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        Enemy.OnAlarmTriggered -= ShowAlarm;
        Enemy.OnAlarmCleared -= HideAlarm;
        WaveManager.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveCleared(int wave)
    {
        HideAlarm();
    }

    /// <summary>
    /// 경보 발생 UI 표시
    /// </summary>
    public void ShowAlarm()
    {
        if (isAlarmActive) return;
        isAlarmActive = true;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(AlarmFlashRoutine());

        Debug.Log("[AlarmUI] <color=#FF2222>🚨 ALARM UI ACTIVATED - INTRUDER DETECTED! 🚨</color>");
    }

    /// <summary>
    /// 경보 해제
    /// </summary>
    public void HideAlarm()
    {
        if (!isAlarmActive) return;
        isAlarmActive = false;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        if (alarmBannerGroup != null) alarmBannerGroup.alpha = 0f;
        if (screenVignette != null) screenVignette.color = new Color(alertColor.r, alertColor.g, alertColor.b, 0f);

        Debug.Log("[AlarmUI] <color=#00FFAA>✅ Alarm Cleared</color>");
    }

    private IEnumerator AlarmFlashRoutine()
    {
        while (isAlarmActive)
        {
            float pingPong = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;

            if (alarmBannerGroup != null)
            {
                alarmBannerGroup.alpha = Mathf.Lerp(0.6f, 1f, pingPong);
            }

            if (screenVignette != null)
            {
                screenVignette.color = new Color(alertColor.r, alertColor.g, alertColor.b, Mathf.Lerp(0.05f, 0.25f, pingPong));
            }

            yield return null;
        }
    }

    private void OnGUI()
    {
        // Canvas UI가 미할당된 경우를 위한 안전한 OnGUI Fallback 렌더링
        if (isAlarmActive && alarmBannerGroup == null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 24;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.red;

            float width = 450f;
            float height = 50f;
            float x = (Screen.width - width) * 0.5f;
            float y = 30f;

            GUI.Box(new Rect(x, y, width, height), "🚨 ALERT! INTRUDER DETECTED! 🚨", style);
        }
    }
}
