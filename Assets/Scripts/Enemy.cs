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

    [Header("Ranged Attack Settings")]
    [Tooltip("적이 발사할 투사체 프리팹")]
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private float attackInterval = 1.8f;
    [SerializeField] private float projectileSpeed = 3.5f;
    private float nextAttackTime = 0f;

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
        LookAtPlayer();

        if (playerTransform == null) return;

        Vector2 myPos = transform.position;
        Vector2 playerPos = playerTransform.position;
        Vector2 toPlayer = playerPos - myPos;
        float currentDist = toPlayer.magnitude;
        Vector2 dirToPlayer = currentDist > 0.01f ? toPlayer / currentDist : Vector2.up;

        float preferredDist = GetPreferredCombatDistance();

        // 1. 키별 고유 유지 거리에 따른 전진/후퇴/선회 이동
        Vector2 targetMovement = Vector2.zero;

        if (currentDist > preferredDist + 0.6f)
        {
            // 너무 멀면 전진
            targetMovement += dirToPlayer;
        }
        else if (currentDist < preferredDist - 0.6f)
        {
            // 너무 가까우면 거리 벌리기
            targetMovement -= dirToPlayer * 0.7f;
        }
        else
        {
            // 적정 거리 도달 시 플레이어 주변을 시계/반시계 방향으로 선회(Strafe)하여 포위
            Vector2 strafeDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
            float strafeSign = (targetKey == KeyCode.W || targetKey == KeyCode.R) ? 1f : -1f;
            targetMovement += strafeDir * (strafeSign * 0.6f);
        }

        // 2. Separation (적들끼리 한 점으로 겹치지 않도록 밀어내는 반발력)
        Vector2 separation = CalculateSeparationForce();
        targetMovement += separation * 1.5f;

        if (targetMovement.sqrMagnitude > 1f) targetMovement.Normalize();

        transform.position += (Vector3)targetMovement * (chaseSpeed * Time.deltaTime);

        // 3. 발각(Chase) 상태일 때 주기적으로 플레이어를 향해 투사체 발사 (1.5~2.0초 간격)
        if (Time.time >= nextAttackTime)
        {
            FireProjectile();
            nextAttackTime = Time.time + attackInterval + UnityEngine.Random.Range(-0.2f, 0.3f);
        }
    }

    /// <summary>
    /// 플레이어 방향으로 투사체 1발 발사
    /// </summary>
    public void FireProjectile()
    {
        if (isDead || playerTransform == null) return;

        Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector3 spawnPos = transform.position + (Vector3)(dir * 0.7f);

        EnemyProjectile projInstance;

        if (projectilePrefab != null)
        {
            projInstance = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 프리팹 미할당 시 원형 스프라이트 투사체 동적 생성 (안전 Fallback)
            GameObject fallbackProj = new GameObject("EnemyProjectile");
            fallbackProj.transform.position = spawnPos;
            fallbackProj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            SpriteRenderer sr = fallbackProj.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.2f, 0.2f, 1f); // 붉은색 탄환
            sr.sortingOrder = 3;

            CircleCollider2D col = fallbackProj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            projInstance = fallbackProj.AddComponent<EnemyProjectile>();
        }

        projInstance.Init(dir, projectileSpeed, 1f);
    }

    /// <summary>
    /// 플레이어가 처형 공격을 실패했을 때, 튕겨나간 플레이어를 향해 즉각 반격 발사
    /// </summary>
    public void TriggerImmediateCounterAttack()
    {
        if (isDead) return;
        StopCoroutine(nameof(ImmediateCounterAttackRoutine));
        StartCoroutine(nameof(ImmediateCounterAttackRoutine));
    }

    private System.Collections.IEnumerator ImmediateCounterAttackRoutine()
    {
        // 튕겨나간 직후 0.15초 뒤 즉시 반격 탄환 발사
        yield return new WaitForSeconds(0.15f);
        if (!isDead)
        {
            FireProjectile();
            nextAttackTime = Time.time + attackInterval;
            Debug.Log($"[Enemy:{name}] <color=#FF2222>⚡ [COUNTER ATTACK] 튕겨나간 플레이어에게 즉각 반격 탄환 발사!</color>");
        }
    }

    /// <summary>
    /// 적 종류(Q, W, E, R)별 플레이어와의 고유 유지 거리
    /// </summary>
    private float GetPreferredCombatDistance()
    {
        return targetKey switch
        {
            KeyCode.Q => 1.8f, // Q: 근접 돌격형 (1.8m)
            KeyCode.W => 3.2f, // W: 미들 레인지 (3.2m)
            KeyCode.E => 5.5f, // E: 원거리 저격/견제 (5.5m)
            KeyCode.R => 4.0f, // R: 포위 서클러 (4.0m)
            _ => 2.5f
        };
    }

    /// <summary>
    /// 주변 다른 적들과의 거리 계산을 통한 겹침 방지 반발력(Separation) 산출
    /// </summary>
    private Vector2 CalculateSeparationForce()
    {
        Vector2 separation = Vector2.zero;
        int neighborCount = 0;
        float separationRadius = 1.8f;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy other = activeEnemies[i];
            if (other == null || other == this || other.isDead) continue;

            Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = diff.magnitude;

            if (dist > 0.001f && dist < separationRadius)
            {
                // 가까울수록 더 강한 반발력 적용
                separation += (diff / dist) * ((separationRadius - dist) / separationRadius);
                neighborCount++;
            }
        }

        if (neighborCount > 0)
        {
            separation /= neighborCount;
        }

        return separation;
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

        Color targetColor = currentState switch
        {
            EnemyState.Patrol => new Color(1f, 0.35f, 0.35f), // 기본 오렌지레드
            EnemyState.Alert => new Color(1f, 0.85f, 0.1f),   // 경계 옐로우
            EnemyState.Chase => new Color(1f, 0.1f, 0.1f),    // 추격 딥레드
            _ => new Color(1f, 0.35f, 0.35f)
        };

        spriteRenderer.color = targetColor;
        if (TryGetComponent<FlashEffect>(out var flash))
        {
            flash.SetOriginalColor(targetColor);
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

        // 처형 타격 시 순백색 번쩍임(White Flash)
        if (TryGetComponent<FlashEffect>(out var flash))
        {
            flash.FlashWhite(0.1f);
        }

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

    private void LateUpdate()
    {
        ResolveOverlappingTextOffsets();
    }

    /// <summary>
    /// 근접한 적들 간의 머리 위 문자 UI 겹침 방지 및 절대 회전 고정 (회전 방지)
    /// </summary>
    public static void ResolveOverlappingTextOffsets()
    {
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy enemyA = activeEnemies[i];
            if (enemyA == null || enemyA.isDead || enemyA.keyTextDisplay == null) continue;

            // 1. 적이 회전해도 글자는 항상 정면(0도)을 바라보도록 회전 고정
            enemyA.keyTextDisplay.transform.rotation = Quaternion.identity;

            float dynamicOffsetY = enemyA.textOffset.y;
            float dynamicOffsetX = enemyA.textOffset.x;

            for (int j = 0; j < activeEnemies.Count; j++)
            {
                if (i == j) continue;
                Enemy enemyB = activeEnemies[j];
                if (enemyB == null || enemyB.isDead) continue;

                float dist = Vector2.Distance(enemyA.transform.position, enemyB.transform.position);
                // 1.8m 이내로 근접한 경우 텍스트를 위/옆으로 분산
                if (dist < 1.8f && i > j)
                {
                    dynamicOffsetY += 0.5f;
                    dynamicOffsetX += (enemyA.transform.position.x >= enemyB.transform.position.x) ? 0.35f : -0.35f;
                }
            }

            enemyA.keyTextDisplay.transform.position = enemyA.transform.position + new Vector3(dynamicOffsetX, dynamicOffsetY, 0f);
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
