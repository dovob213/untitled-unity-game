using System;
using UnityEngine;

/// <summary>
/// 방향키(Arrow Keys) 이동 및 Q/W/E/R 키 입력을 통한 순간이동(Blink) 타격 컨트롤러
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[SelectionBase]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("플레이어의 기본 이동 속도 (방향키 조작)")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("가속도 (부드러운 반응성 조절)")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("감속도 (키를 뗐을 때 멈추는 빠르기)")]
    [SerializeField] private float deceleration = 50f;

    [Header("Blink Attack Settings")]
    [Tooltip("순간이동 기본 타격 데미지")]
    [SerializeField] private float blinkDamage = 1f;

    [Tooltip("타격 시 대상 위치로부터의 오프셋")]
    [SerializeField] private Vector2 strikeOffset = Vector2.zero;

    [Header("Combo Settings")]
    [Tooltip("콤보 유지 제한 시간 (초)")]
    [SerializeField] private float comboTimeout = 2.0f;
    private int currentCombo = 0;
    private float lastHitTime = 0f;

    public event Action<Enemy, KeyCode> OnBlinkExecuted;
    public event Action<int> OnComboChanged;

    private Rigidbody2D rb;
    private Vector2 currentVelocity;
    private SkillModuleSystem skillSystem;

    public int CurrentCombo => currentCombo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        skillSystem = GetComponent<SkillModuleSystem>();
        if (skillSystem == null)
        {
            skillSystem = gameObject.AddComponent<SkillModuleSystem>();
        }
    }

    private void OnEnable()
    {
        InputManager.OnCombatKeyPressed += HandleCombatKey;
    }

    private void OnDisable()
    {
        InputManager.OnCombatKeyPressed -= HandleCombatKey;
    }

    private void Update()
    {
        CheckComboTimeout();
    }

    private void FixedUpdate()
    {
        HandleSmoothMovement();
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
    /// 방향키 입력을 통한 부드러운 물리 이동
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
        Enemy targetEnemy = Enemy.FindTargetByKey(key, transform.position);

        if (targetEnemy != null && !targetEnemy.IsDead)
        {
            ExecuteBlinkStrike(targetEnemy, key);
        }
        else
        {
            ResetCombo();
            Debug.Log($"[PlayerController] Missed! No target for key: <color=yellow>{key}</color> (Combo Reset)");
        }
    }

    /// <summary>
    /// 지정된 적에게 즉시 이동하며 타격 및 시너지 모듈 발동
    /// </summary>
    private void ExecuteBlinkStrike(Enemy target, KeyCode key)
    {
        Vector3 targetPosition = target.transform.position + (Vector3)strikeOffset;

        // 1. 순간이동 및 관성 리셋
        transform.position = targetPosition;
        currentVelocity = Vector2.zero;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        // 2. 콤보 증가
        currentCombo++;
        lastHitTime = Time.time;
        OnComboChanged?.Invoke(currentCombo);

        Debug.Log($"[PlayerController] <color=#00FFAA>⚡ BLINK STRIKE ⚡</color> [{key}] -> [{target.name}] (Combo: <color=#FFAA00>{currentCombo}x</color>)");

        // 3. 적 기본 타격
        target.TakeBlinkHit(blinkDamage);

        // 4. 시너지 모듈 발동 (Q: 광역 shock, W: 치명타, E: 폭발, R: 연쇄)
        if (skillSystem != null)
        {
            skillSystem.TriggerModule(key, this, target);
        }

        OnBlinkExecuted?.Invoke(target, key);
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
