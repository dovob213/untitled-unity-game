using System;
using UnityEngine;

/// <summary>
/// Rigidbody2D 기반 2D 탑다운 이동 
/// Q/W/E/R 키 입력을 통한 순간이동(Blink) 타격을 담당하는 플레이어 컨트롤러
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[SelectionBase]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("플레이어의 기본 이동 속도")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("가속도 (부드러운 반응성 조절)")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("감속도 (키를 뗐을 때 멈추는 빠르기)")]
    [SerializeField] private float deceleration = 50f;

    [Header("Blink Attack Settings")]
    [Tooltip("순간이동 타격 시 가하는 데미지")]
    [SerializeField] private float blinkDamage = 1f;

    [Tooltip("타격 시 대상 위치로부터의 오프셋")]
    [SerializeField] private Vector2 strikeOffset = Vector2.zero;

    /// <summary>
    /// 플레이어가 적에게 순간이동 타격을 성공했을 때 호출되는 이벤트 (대상 적, 사용된 키)
    /// </summary>
    public event Action<Enemy, KeyCode> OnBlinkExecuted;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
    }

    private void OnEnable()
    {
        // InputManager의 전투 키 이벤트 구독
        InputManager.OnCombatKeyPressed += HandleCombatKey;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지
        InputManager.OnCombatKeyPressed -= HandleCombatKey;
    }

    private void FixedUpdate()
    {
        HandleSmoothMovement();
    }

    /// <summary>
    /// (탑다운 2D용) Rigidbody2D 기본 설정
    /// </summary>
    private void ConfigureRigidbody()
    {
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    /// <summary>
    /// 가속도 및 감속도를 적용한 물리 이동 처리
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
    /// Q, W, E, R 키가 입력되었을 때 타겟 적에게 순간이동 타격 수행
    /// </summary>
    private void HandleCombatKey(KeyCode key)
    {
        // 해당 키를 가진 가장 가까운 적 탐색
        Enemy targetEnemy = Enemy.FindTargetByKey(key, transform.position);

        if (targetEnemy != null && !targetEnemy.IsDead)
        {
            ExecuteBlinkStrike(targetEnemy, key);
        }
        else
        {
            Debug.Log($"[PlayerController] Missed! No active target for key: <color=yellow>{key}</color>");
        }
    }

    /// <summary>
    /// 지정된 적에게 즉시 이동하며 타격
    /// </summary>
    private void ExecuteBlinkStrike(Enemy target, KeyCode key)
    {
        Vector3 targetPosition = target.transform.position + (Vector3)strikeOffset;

        // 1. 순간이동 위치 갱신 및 기존 관성 리셋
        transform.position = targetPosition;
        currentVelocity = Vector2.zero;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        Debug.Log($"[PlayerController] <color=#00FFAA>⚡ BLINK STRIKE ⚡</color> to [{target.name}] with [{key}]");

        // 2. 적에게 데미지 적용
        target.TakeBlinkHit(blinkDamage);

        // 3. 연출 및 시너지 시스템을 위한 이벤트 발행
        OnBlinkExecuted?.Invoke(target, key);
    }
}
