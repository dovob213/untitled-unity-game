using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 적의 행동 상태 머신
/// </summary>
public enum EnemyState
{
    Patrol, // 순찰 상태 (느린 이동, 시야 탐색)
    Alert,  // 경계 상태 (시야 내 플레이어 감지, 0.4초 유예)
    Chase   // 추격 상태 (발각 확정, 빠른 이동, 전체 알람)
}

/// <summary>
/// 2D 부채꼴 시야(FoV)와 상태 머신(Patrol, Alert, Chase)을 가지는 적 클래스
/// 시야 밖 타격 시 조용히 암살되며, 발각 시 전체 경보(Alarm)를 발령하고 플레이어를 추격합니다.
/// </summary>
[SelectionBase]
public class Enemy : MonoBehaviour
{
    private static readonly List<Enemy> activeEnemies = new List<Enemy>();
    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;

    /// <summary>
    /// 적이 처치되었을 때 호출되는 전역 이벤트
    /// </summary>
    public static event Action<Enemy> OnEnemyDied;

    /// <summary>
    /// 플레이어가 적의 시야에 발각되어 전체 경보가 발령되었을 때 호출되는 이벤트
    /// </summary>
    public static event Action OnAlarmTriggered;

    /// <summary>
    /// 경보가 해제되었을 때 호출되는 이벤트
    /// </summary>
    public static event Action OnAlarmCleared;

    public static bool IsAlarmActive { get; private set; }

    [Header("State Machine Settings")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float alertDelay = 0.4f;

    [Header("Field of View (2D)")]
    [Tooltip("시야 반경 (거리)")]
    [SerializeField] private float viewRadius = 5.5f;
    [Tooltip("부채꼴 시야 각도")]
    [Range(0f, 360f)]
    [SerializeField] private float viewAngle = 90f;
    [Tooltip("시야를 가리는 장애물 레이어 (벽 등)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Target Key Settings")]
    [Tooltip("이 적을 타겟팅하기 위한 키 (Q, W, E, R)")]
    [SerializeField] private KeyCode targetKey = KeyCode.Q;

    [Header("Visual Feedback")]
    [SerializeField] private TMP_Text keyTextDisplay;
    [SerializeField] private Vector3 textOffset = new Vector3(0, 1.2f, 0);
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Enemy Stats")]
    [SerializeField] private float maxHealth = 1f;
    private float currentHealth;
    private bool isDead = false;

    private Transform playerTransform;
    private float alertTimer = 0f;
    private Vector2 patrolDirection;
    private float nextPatrolDirChangeTime;

    public KeyCode TargetKey => targetKey;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public EnemyState CurrentState => currentState;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        SetupKeyDisplay();
    }

    private void OnEnable()
    {
        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }

        // 이미 전역 경보 상태인 경우 즉시 추격 상태로 진입
        if (IsAlarmActive)
        {
            SetState(EnemyState.Chase);
        }
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
        CheckAllEnemiesCleared();
    }

    private void Start()
    {
        FindPlayer();
        PickRandomPatrolDirection();
        UpdateKeyDisplay();
    }

    private void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        HandleStateMachine();
    }

    private void FindPlayer()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    /// <summary>
    /// 상태 머신 로직 분기
    /// </summary>
    private void HandleStateMachine()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                HandlePatrol();
                break;

            case EnemyState.Alert:
                HandleAlert();
                break;

            case EnemyState.Chase:
                HandleChase();
                break;
        }
    }

    private void HandlePatrol()
    {
        // 1. 주기적으로 방향 전환하며 순찰 이동
        if (Time.time >= nextPatrolDirChangeTime)
        {
            PickRandomPatrolDirection();
        }

        transform.position += (Vector3)patrolDirection * (patrolSpeed * Time.deltaTime);

        // 이동 방향으로 부드러운 회전 (Sprite의 위쪽(transform.up)이 전방)
        if (patrolDirection.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle), 5f * Time.deltaTime);
        }

        // 2. 시야 내 플레이어 감지 체크
        if (IsPlayerInFoV())
        {
            SetState(EnemyState.Alert);
        }
    }

    private void HandleAlert()
    {
        // 플레이어를 바라봄
        LookAtPlayer();

        if (IsPlayerInFoV())
        {
            alertTimer += Time.deltaTime;
            if (alertTimer >= alertDelay)
            {
                TriggerGlobalAlarm();
            }
        }
        else
        {
            // 플레이어가 시야 밖으로 벗어남 -> 다시 순찰로 복귀
            alertTimer -= Time.deltaTime * 0.5f;
            if (alertTimer <= 0f)
            {
                alertTimer = 0f;
                SetState(EnemyState.Patrol);
            }
        }
    }

    private void HandleChase()
    {
        // 플레이어를 향해 빠른 속도로 추격
        LookAtPlayer();
        Vector2 dirToPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        transform.position += (Vector3)dirToPlayer * (chaseSpeed * Time.deltaTime);
    }

    private void LookAtPlayer()
    {
        if (playerTransform == null) return;
        Vector2 dir = (playerTransform.position - transform.position).normalized;
        if (dir.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle), 10f * Time.deltaTime);
        }
    }

    private void PickRandomPatrolDirection()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        nextPatrolDirChangeTime = Time.time + UnityEngine.Random.Range(2.5f, 4.5f);
    }

    /// <summary>
    /// 2D 부채꼴 시야(FoV) 및 장애물 Raycast 검사
    /// </summary>
    public bool IsPlayerInFoV()
    {
        if (playerTransform == null) return false;

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        Vector2 dirToPlayer = target - origin;
        float distanceToPlayer = dirToPlayer.magnitude;

        // 1. 거리 체크
        if (distanceToPlayer > viewRadius) return false;

        // 2. 부채꼴 각도 체크 (transform.up 기준)
        Vector2 forwardDir = transform.up;
        float angleToPlayer = Vector2.Angle(forwardDir, dirToPlayer);

        if (angleToPlayer <= viewAngle * 0.5f)
        {
            // 3. 장애물 Line of Sight (Raycast) 체크
            if (obstacleLayer.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, dirToPlayer.normalized, distanceToPlayer, obstacleLayer);
                if (hit.collider != null)
                {
                    return false; // 장애물에 가려짐
                }
            }

            return true; // 시야 내 플레이어 발견!
        }

        return false;
    }

    /// <summary>
    /// 전역 경보 발령: 모든 살아있는 적을 Chase 상태로 전환
    /// </summary>
    public static void TriggerGlobalAlarm()
    {
        if (IsAlarmActive) return;
        IsAlarmActive = true;

        Debug.Log("<color=#FF0000>🚨 [ALARM TRIGGERED] PLAYER SPOTTED! All enemies enter CHASE mode! 🚨</color>");

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy e = activeEnemies[i];
            if (e != null && !e.isDead)
            {
                e.SetState(EnemyState.Chase);
            }
        }

        OnAlarmTriggered?.Invoke();
    }

    /// <summary>
    /// 전역 경보 해제
    /// </summary>
    public static void ClearGlobalAlarm()
    {
        if (!IsAlarmActive) return;
        IsAlarmActive = false;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy e = activeEnemies[i];
            if (e != null && !e.isDead)
            {
                e.SetState(EnemyState.Patrol);
            }
        }

        OnAlarmCleared?.Invoke();
    }

    private void SetState(EnemyState newState)
    {
        currentState = newState;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null) return;

        switch (currentState)
        {
            case EnemyState.Patrol:
                spriteRenderer.color = new Color(1f, 0.35f, 0.35f); // 기본 오렌지레드
                break;
            case EnemyState.Alert:
                spriteRenderer.color = new Color(1f, 0.85f, 0.1f); // 경계 옐로우
                break;
            case EnemyState.Chase:
                spriteRenderer.color = new Color(1f, 0.1f, 0.1f); // 추격 딥레드
                break;
        }
    }

    /// <summary>
    /// 스포너에서 적 생성 시 초기화
    /// </summary>
    public void Init(KeyCode key, float health = 1f)
    {
        targetKey = key;
        maxHealth = health;
        currentHealth = health;
        isDead = false;
        alertTimer = 0f;

        SetState(IsAlarmActive ? EnemyState.Chase : EnemyState.Patrol);

        SetupKeyDisplay();
        UpdateKeyDisplay();
    }

    /// <summary>
    /// 특정 키에 매칭되는 가장 가까운 적 탐색
    /// </summary>
    public static Enemy FindTargetByKey(KeyCode key, Vector3 originPosition)
    {
        Enemy bestTarget = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy enemy = activeEnemies[i];
            if (enemy == null || enemy.isDead || enemy.targetKey != key) continue;

            float distSqr = (enemy.transform.position - originPosition).sqrMagnitude;
            if (distSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distSqr;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    public void SetTargetKey(KeyCode newKey)
    {
        targetKey = newKey;
        UpdateKeyDisplay();
    }

    /// <summary>
    /// 순간이동 공격 피격 처리
    /// </summary>
    public void TakeBlinkHit(float damage = 1f)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (currentState == EnemyState.Patrol)
        {
            Debug.Log($"[Enemy:{gameObject.name}] <color=#55FF55>🗡️ [STEALTH KILL]</color> Assassinated quietly outside sight cone! Key: {targetKey}");
        }
        else
        {
            Debug.Log($"[Enemy:{gameObject.name}] <color=#FF4444>💥 [COMBAT KILL]</color> Defeated in combat! Key: {targetKey}");
        }

        OnEnemyDied?.Invoke(this);
        Destroy(gameObject);
    }

    private static void CheckAllEnemiesCleared()
    {
        if (activeEnemies.Count == 0 && IsAlarmActive)
        {
            ClearGlobalAlarm();
        }
    }

    private void SetupKeyDisplay()
    {
        if (keyTextDisplay == null)
        {
            keyTextDisplay = GetComponentInChildren<TMP_Text>();
        }
    }

    public void UpdateKeyDisplay()
    {
        if (keyTextDisplay != null)
        {
            keyTextDisplay.text = targetKey.ToString().ToUpper();
            keyTextDisplay.transform.localPosition = textOffset;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 1. 머리 위 텍스트 위치 가이드라인
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + textOffset, 0.2f);

        // 2. 2D 부채꼴 시야(FoV) 시각화
        DrawFieldOfViewGizmo();
    }

    private void DrawFieldOfViewGizmo()
    {
        Color coneColor = currentState switch
        {
            EnemyState.Patrol => new Color(0f, 1f, 0.3f, 0.35f),
            EnemyState.Alert => new Color(1f, 0.9f, 0f, 0.5f),
            EnemyState.Chase => new Color(1f, 0f, 0f, 0.5f),
            _ => Color.white
        };

        Gizmos.color = coneColor;

        Vector3 origin = transform.position;
        float halfAngle = viewAngle * 0.5f;

        Vector3 leftDir = Quaternion.Euler(0, 0, halfAngle) * transform.up;
        Vector3 rightDir = Quaternion.Euler(0, 0, -halfAngle) * transform.up;

        Gizmos.DrawLine(origin, origin + leftDir * viewRadius);
        Gizmos.DrawLine(origin, origin + rightDir * viewRadius);

        // 부채꼴 호 그리기
        int segments = 16;
        Vector3 prevPoint = origin + leftDir * viewRadius;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = halfAngle - (viewAngle / segments) * i;
            Vector3 currentDir = Quaternion.Euler(0, 0, currentAngle) * transform.up;
            Vector3 currentPoint = origin + currentDir * viewRadius;

            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }
}
