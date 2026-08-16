using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 2단 분리형 전투 시스템 컨트롤러
/// 1단계: QWER 키로 타겟팅 및 체공(Dash) 돌진
/// 2단계: 체공 시간(약 0.25초) 동안 ASDF 키 입력 버퍼링
/// 3단계: 도달 시 버퍼링된 키에 따라 다른 처형 공격 분기 실행
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
    [Tooltip("타겟 적에게 날아가는 체공 시간 (초)")]
    [SerializeField] private float dashDuration = 0.25f;

    [Tooltip("순간이동 기본 타격 데미지")]
    [SerializeField] private float blinkDamage = 1f;

    [Tooltip("타격 시 대상 위치로부터의 오프셋")]
    [SerializeField] private Vector2 strikeOffset = Vector2.zero;

    [Header("Combo Settings")]
    [Tooltip("콤보 유지 제한 시간 (초)")]
    [SerializeField] private float comboTimeout = 2.0f;
    private int currentCombo = 0;
    private float lastHitTime = 0f;

    // 2단 분리형 전투 상태 변수
    private bool isDashing = false;
    private KeyCode? bufferedAttackKey = null;
    private Coroutine dashCoroutine;

    public event Action<Enemy, KeyCode> OnBlinkExecuted;
    public event Action<int> OnComboChanged;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;

    public bool IsDashing => isDashing;
    public int CurrentCombo => currentCombo;
    public KeyCode? BufferedAttackKey => bufferedAttackKey;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
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

    /// <summary>
    /// 방향키 입력을 통한 일반 물리 이동 (돌진 중이 아닐 때만 작동)
    /// </summary>
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
    /// [1단계: QWER 타겟팅] 타겟 적을 선택하고 체공(Dash) 비행 시작
    /// </summary>
    private void HandleTargetKey(KeyCode key)
    {
        if (isDashing) return; // 이미 돌진 중이면 새 타겟팅 무시

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
    /// [2단계: ASDF 입력 버퍼링] 체공 시간 도중 입력된 공격 키 저장
    /// </summary>
    private void HandleAttackBufferInput(KeyCode key)
    {
        if (isDashing)
        {
            bufferedAttackKey = key;
            Debug.Log($"[PlayerController] 📥 <color=#FFCC00>[BUFFERED]</color> Attack Key: <b>{key}</b> (Executing on arrival)");
        }
    }

    /// <summary>
    /// 타겟 적을 향한 체공 돌진 코루틴 실행
    /// </summary>
    private void StartDashFlight(Enemy target)
    {
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = StartCoroutine(DashFlightRoutine(target));
    }

    private IEnumerator DashFlightRoutine(Enemy target)
    {
        isDashing = true;
        bufferedAttackKey = null; // 버퍼 초기화

        // 이동 관성 리셋
        currentVelocity = Vector2.zero;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        // 체공 시간 동안 적을 향해 부드럽게 날아감
        while (elapsed < dashDuration)
        {
            if (target == null || target.IsDead) break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            // SmoothStep으로 날렵한 돌진 궤적 구현
            float smoothT = t * t * (3f - 2f * t);

            Vector3 currentTargetPos = target.transform.position + (Vector3)strikeOffset;
            transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);

            yield return null;
        }

        // 도착 위치 보정
        if (target != null && !target.IsDead)
        {
            transform.position = target.transform.position + (Vector3)strikeOffset;
        }

        isDashing = false;
        dashCoroutine = null;

        // [3단계 & 4단계: 도착 및 처형 공격 실행]
        ExecuteBufferedAttack(target);
    }

    /// <summary>
    /// [4단계: 도착 및 처형] 저장된 버퍼 키(A,S,D,F)에 따른 공격 분기 실행
    /// </summary>
    private void ExecuteBufferedAttack(Enemy target)
    {
        if (target == null || target.IsDead)
        {
            Debug.Log("[PlayerController] 타겟이 도착 전에 소멸되었습니다.");
            return;
        }

        // 아무 키도 입력하지 않은 경우 (공격 실패)
        if (!bufferedAttackKey.HasValue)
        {
            ResetCombo();
            Debug.Log("<color=#FF4444>[Attack: FAIL] 챙! 공격 타이밍을 놓쳐 튕겨나감!</color>");

            // 튕겨나가는 넉백 리코일 효과
            Vector2 recoilDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            if (recoilDir.sqrMagnitude < 0.01f) recoilDir = -transform.up;
            transform.position += (Vector3)(recoilDir * 1.0f);
            return;
        }

        KeyCode attackKey = bufferedAttackKey.Value;

        // 콤보 갱신
        currentCombo++;
        lastHitTime = Time.time;
        OnComboChanged?.Invoke(currentCombo);

        // ASDF 공격 분기 처리
        switch (attackKey)
        {
            case KeyCode.A:
                // A: 기본 베기 (적 즉사)
                Debug.Log($"[PlayerController] <color=#00FFAA>⚔️ [ATTACK: A (기본 베기)]</color> 적 즉사! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                break;

            case KeyCode.S:
                // S: 횡베기 (적 즉사 및 주변 적 넉백)
                Debug.Log($"[PlayerController] <color=#33CCFF>⚔️ [ATTACK: S (횡베기)]</color> 적 즉사 및 주변 적 넉백! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                ApplyHorizontalKnockback(target.transform.position, 4f);
                break;

            case KeyCode.D:
                // D: 관통 대쉬 (적 즉사 및 전방 추가 슬라이딩)
                Debug.Log($"[PlayerController] <color=#FFAA00>⚔️ [ATTACK: D (관통 대쉬)]</color> 적 즉사 및 바라보던 방향으로 약간 더 슬라이딩! (Combo: {currentCombo}x)");
                Vector3 slideDir = (target.transform.position - transform.position).normalized;
                if (slideDir.sqrMagnitude < 0.01f) slideDir = transform.up;
                target.TakeBlinkHit(blinkDamage);
                transform.position += slideDir * 1.5f; // 약간 더 전방 슬라이딩
                break;

            case KeyCode.F:
                // F: 패링 (적 즉사 및 투사체 쳐내기)
                Debug.Log($"[PlayerController] <color=#CC44FF>🛡️ [ATTACK: F (패링)]</color> 적 즉사 및 투사체 쳐내기! (Combo: {currentCombo}x)");
                target.TakeBlinkHit(blinkDamage);
                break;

            default:
                target.TakeBlinkHit(blinkDamage);
                break;
        }

        // 버퍼 초기화 및 이벤트 발행
        bufferedAttackKey = null;
        OnBlinkExecuted?.Invoke(target, attackKey);
    }

    /// <summary>
    /// S 스킬용 주변 적 넉백 처리
    /// </summary>
    private void ApplyHorizontalKnockback(Vector3 center, float radius)
    {
        var enemies = Enemy.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsDead) continue;

            float dist = Vector2.Distance(center, e.transform.position);
            if (dist <= radius)
            {
                Rigidbody2D erb = e.GetComponent<Rigidbody2D>();
                if (erb != null)
                {
                    Vector2 pushDir = ((Vector2)e.transform.position - (Vector2)center).normalized;
                    erb.AddForce(pushDir * 6f, ForceMode2D.Impulse);
                }
            }
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
