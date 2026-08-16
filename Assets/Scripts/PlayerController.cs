using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 2단 분리형 전투 컨트롤러 (QWER 타겟팅 -> 체공 슬로우 모션 -> ASDF 처형 버퍼링 & 역경직)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[SelectionBase]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("플레이어의 기본 이동 속도 (방향키 조작)")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 50f;

    [Header("Combat & Dash Timing")]
    [Tooltip("타겟 적에게 날아가는 실제 체공 시간 (초)")]
    [SerializeField] private float dashDuration = 0.25f;

    [Tooltip("체공 중 적용할 타임 슬로우 배율 (0.3 = 30% 속도)")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMoScale = 0.3f;

    [Tooltip("처형 성공 시 역경직(Hit-Stop) 지속 시간 (초)")]
    [SerializeField] private float hitStopDuration = 0.05f;

    [Tooltip("순간이동 기본 타격 데미지")]
    [SerializeField] private float blinkDamage = 1f;

    [Tooltip("타격 시 대상 위치로부터의 오프셋")]
    [SerializeField] private Vector2 strikeOffset = Vector2.zero;

    [Header("Visual Feedback")]
    [Tooltip("플레이어 머리 위에 버퍼링된 키를 표시할 TMP 텍스트 (자동 생성)")]
    [SerializeField] private TMP_Text bufferedKeyDisplay;
    [SerializeField] private Vector3 keyDisplayOffset = new Vector3(0, 1.2f, 0);

    [Header("Combo Settings")]
    [Tooltip("콤보 유지 제한 시간 (초)")]
    [SerializeField] private float comboTimeout = 2.0f;
    private int currentCombo = 0;
    private float lastHitTime = 0f;

    // 전투 상태 변수
    private bool isDashing = false;
    private KeyCode? bufferedKey = null;
    private Vector3 dashStartPos;
    private Coroutine dashCoroutine;
    private Coroutine hitStopCoroutine;

    public event Action<Enemy, KeyCode> OnBlinkExecuted;
    public event Action<int> OnComboChanged;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;

    public bool IsDashing => isDashing;
    public int CurrentCombo => currentCombo;
    public KeyCode? BufferedKey => bufferedKey;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        SetupBufferedKeyDisplay();
    }

    private void OnEnable()
    {
        InputManager.OnTargetKeyPressed += HandleTargetKey;
        InputManager.OnAttackKeyPressed += HandleAttackBufferInput;
    }

    private void OnDisable()
    {
        InputManager.OnTargetKeyPressed -= HandleTargetKey;
        InputManager.OnAttackKeyPressed -= HandleAttackBufferInput;
        // 안전하게 TimeScale 원복
        Time.timeScale = 1.0f;
    }

    private void Update()
    {
        CheckComboTimeout();
    }

    private void FixedUpdate()
    {
        if (!isDashing)
        {
            HandleSmoothMovement();
        }
    }

    private void ConfigureRigidbody()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void HandleSmoothMovement()
    {
        Vector2 inputDir = InputManager.MoveInput;
        Vector2 targetVelocity = inputDir * moveSpeed;

        float rate = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = currentVelocity;
#else
        rb.velocity = currentVelocity;
#endif
    }

    /// <summary>
    /// [1단계: QWER 타겟팅] 타겟 선택 후 슬로우 모션 돌진 시작
    /// </summary>
    private void HandleTargetKey(KeyCode key)
    {
        if (isDashing) return;

        Enemy targetEnemy = Enemy.FindTargetByKey(key, transform.position);

        if (targetEnemy != null && !targetEnemy.IsDead)
        {
            StartDashFlight(targetEnemy);
        }
        else
        {
            ResetCombo();
            Debug.Log($"[PlayerController] Missed! No target for key: <color=yellow>{key}</color>");
        }
    }

    /// <summary>
    /// [2단계: ASDF 입력 버퍼링] 체공 중 입력 키 저장 및 플레이어 머리 위 UI 팝업
    /// </summary>
    private void HandleAttackBufferInput(KeyCode key)
    {
        if (isDashing)
        {
            bufferedKey = key;
            ShowBufferedKeyPopup(key);
            Debug.Log($"[PlayerController] 📥 <color=#FFCC00>[BUFFERED]</color> Attack Key: <b>[{key}]</b>");
        }
    }

    private void StartDashFlight(Enemy target)
    {
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = StartCoroutine(DashFlightRoutine(target));
    }

    private IEnumerator DashFlightRoutine(Enemy target)
    {
        isDashing = true;
        bufferedKey = null;
        HideBufferedKeyPopup();

        // 1. 체공 시작 시 타임 슬로우 (Bullet Time)
        Time.timeScale = slowMoScale;

        dashStartPos = transform.position;
        currentVelocity = Vector2.zero;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        float elapsed = 0f;

        // unscaledDeltaTime을 사용하여 슬로우 모션 중에도 일정한 체공 시간 유지
        while (elapsed < dashDuration)
        {
            if (target == null || target.IsDead) break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            float smoothT = t * t * (3f - 2f * t);

            Vector3 currentTargetPos = target.transform.position + (Vector3)strikeOffset;
            transform.position = Vector3.Lerp(dashStartPos, currentTargetPos, smoothT);

            yield return null;
        }

        // 도착 위치 보정
        if (target != null && !target.IsDead)
        {
            transform.position = target.transform.position + (Vector3)strikeOffset;
        }

        // 2. 도착 순간 타임 슬로우 해제
        Time.timeScale = 1.0f;
        isDashing = false;
        dashCoroutine = null;
        HideBufferedKeyPopup();

        // [3단계: 도착 및 처형 분기 실행]
        ExecuteBufferedAttack(target);
    }

    /// <summary>
    /// [3단계 & 4단계: 도착 및 처형 분기 / 역경직 처리]
    /// </summary>
    private void ExecuteBufferedAttack(Enemy target)
    {
        if (target == null || target.IsDead)
        {
            Debug.Log("[PlayerController] 타겟이 도착 전에 이미 사망했습니다.");
            return;
        }

        // 아무 키도 입력하지 않은 경우 (공격 실패)
        if (!bufferedKey.HasValue)
        {
            ResetCombo();
            Debug.Log("<color=#FF4444>[Attack: FAIL] 챙! 공격 타이밍을 놓쳐 튕겨나감!</color>");

            // 튕겨나가는 넉백 리코일
            Vector2 recoilDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            if (recoilDir.sqrMagnitude < 0.01f) recoilDir = -transform.up;
            transform.position += (Vector3)(recoilDir * 1.2f);
            return;
        }

        KeyCode attackKey = bufferedKey.Value;

        // 콤보 갱신
        currentCombo++;
        lastHitTime = Time.time;
        OnComboChanged?.Invoke(currentCombo);

        Vector2 dashDir = ((Vector2)target.transform.position - (Vector2)dashStartPos).normalized;
        if (dashDir.sqrMagnitude < 0.01f) dashDir = transform.up;

        // ASDF 처형 분기
        switch (attackKey)
        {
            case KeyCode.A:
                // A: 정면 넉백
                Debug.Log($"[PlayerController] <color=#00FFAA>⚔️ [ATTACK: A (정면 넉백)]</color> 적 즉사 및 타격 방향으로 강하게 밀어냄! (Combo: {currentCombo}x)");
                Rigidbody2D targetRbA = target.GetComponent<Rigidbody2D>();
                if (targetRbA != null) targetRbA.AddForce(dashDir * 12f, ForceMode2D.Impulse);
                target.TakeBlinkHit(blinkDamage);
                TriggerHitStop(hitStopDuration);
                break;

            case KeyCode.S:
                // S: 뒤로 빠지기
                Debug.Log($"[PlayerController] <color=#33CCFF>⚔️ [ATTACK: S (뒤로 빠지기)]</color> 적 즉사 및 왔던 반대 방향으로 뒤로 튕겨남! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                transform.position = target.transform.position - (Vector3)(dashDir * 2.0f);
                TriggerHitStop(hitStopDuration);
                break;

            case KeyCode.D:
                // D: 관통 슬라이딩
                Debug.Log($"[PlayerController] <color=#FFAA00>⚔️ [ATTACK: D (관통 슬라이딩)]</color> 적 즉사 및 적을 뚫고 타격 방향으로 더 이동! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                transform.position = target.transform.position + (Vector3)(dashDir * 2.0f);
                TriggerHitStop(hitStopDuration);
                break;

            case KeyCode.F:
                // F: 패링
                Debug.Log($"[PlayerController] <color=#CC44FF>🛡️ [ATTACK: F (패링)]</color> 적 즉사 및 투사체 쳐내기! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                TriggerHitStop(hitStopDuration);
                break;

            default:
                target.TakeBlinkHit(blinkDamage);
                TriggerHitStop(hitStopDuration);
                break;
        }

        bufferedKey = null;
        OnBlinkExecuted?.Invoke(target, attackKey);
    }

    /// <summary>
    /// 처형 성공 시 0.05초간 멈추는 역경직(Hit-Stop) 발동
    /// </summary>
    private void TriggerHitStop(float duration)
    {
        if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        hitStopCoroutine = null;
    }

    private void SetupBufferedKeyDisplay()
    {
        if (bufferedKeyDisplay == null)
        {
            bufferedKeyDisplay = GetComponentInChildren<TMP_Text>();
            if (bufferedKeyDisplay == null)
            {
                GameObject textObj = new GameObject("BufferedKeyPopup");
                textObj.transform.SetParent(transform);
                TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
                tmp.fontSize = 5;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.cyan;
                tmp.fontStyle = FontStyles.Bold;
                tmp.sortingOrder = 10;
                bufferedKeyDisplay = tmp;
            }
        }
        HideBufferedKeyPopup();
    }

    private void ShowBufferedKeyPopup(KeyCode key)
    {
        if (bufferedKeyDisplay != null)
        {
            bufferedKeyDisplay.text = $"[{key}]";
            bufferedKeyDisplay.gameObject.SetActive(true);
            bufferedKeyDisplay.transform.localPosition = keyDisplayOffset;
        }
    }

    private void HideBufferedKeyPopup()
    {
        if (bufferedKeyDisplay != null)
        {
            bufferedKeyDisplay.gameObject.SetActive(false);
        }
    }

    private void CheckComboTimeout()
    {
        if (currentCombo > 0 && Time.time - lastHitTime > comboTimeout)
        {
            ResetCombo();
        }
    }

    private void ResetCombo()
    {
        if (currentCombo > 0)
        {
            currentCombo = 0;
            OnComboChanged?.Invoke(0);
        }
    }
}
