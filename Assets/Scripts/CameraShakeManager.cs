using System.Collections;
using UnityEngine;

/// <summary>
/// 화면 흔들림(Camera Shake) 효과를 전역에서 손쉽게 호출할 수 있도록 관리하는 싱글톤 매니저
/// TimeScale이 0(히트스탑)이거나 0.3(타임슬로우)일 때도 실시간(unscaledDeltaTime)으로 부드럽게 동작합니다.
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance { get; private set; }

    [Header("Camera Target")]
    [Tooltip("흔들 대상 카메라 (미지정 시 Camera.main 자동 탐색)")]
    [SerializeField] private Transform targetCamera;

    private Vector3 originalLocalPos;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (targetCamera != null)
        {
            originalLocalPos = targetCamera.localPosition;
        }
    }

    /// <summary>
    /// 카메라 쉐이크 실행
    /// </summary>
    /// <param name="intensity">흔들림 세기 (0.1 ~ 1.5)</param>
    /// <param name="duration">지속 시간 (초)</param>
    public void Shake(float intensity, float duration)
    {
        if (targetCamera == null)
        {
            if (Camera.main != null)
            {
                targetCamera = Camera.main.transform;
                originalLocalPos = targetCamera.localPosition;
            }
            else return;
        }

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);
            float currentIntensity = Mathf.Lerp(intensity, 0f, percent);

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * currentIntensity;
            targetCamera.localPosition = new Vector3(originalLocalPos.x + randomOffset.x, originalLocalPos.y + randomOffset.y, originalLocalPos.z);

            yield return null;
        }

        targetCamera.localPosition = originalLocalPos;
        shakeCoroutine = null;
    }
}
