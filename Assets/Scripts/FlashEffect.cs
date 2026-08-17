using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 또는 타격 시 스프라이트를 순간적으로 번쩍이게(Flash) 만드는 시각 효과 컴포넌트
/// 히트스탑(TimeScale=0) 중에도 정상적으로 번쩍였다가 원래 색상으로 복구됩니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FlashEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalColor = sr.color;
        }
    }

    /// <summary>
    /// 외부에서 원래 색상이 변경되었을 때 기준 색상 갱신
    /// </summary>
    public void SetOriginalColor(Color color)
    {
        originalColor = color;
        if (flashCoroutine == null && sr != null)
        {
            sr.color = color;
        }
    }

    /// <summary>
    /// 지정된 색상으로 순간 번쩍임 실행
    /// </summary>
    public void Flash(Color flashColor, float duration = 0.1f)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(FlashRoutine(flashColor, duration));
    }

    /// <summary>
    /// 처형/타격 시 순백색 번쩍임
    /// </summary>
    public void FlashWhite(float duration = 0.1f)
    {
        Flash(Color.white, duration);
    }

    /// <summary>
    /// 피격 시 붉은색 번쩍임
    /// </summary>
    public void FlashRed(float duration = 0.15f)
    {
        Flash(new Color(1f, 0.2f, 0.2f, 1f), duration);
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration)
    {
        sr.color = flashColor;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        sr.color = originalColor;
        flashCoroutine = null;
    }
}
