using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 머리 위에 할당된 알파벳 키(Q, W, E, R 등)를 가지고 있는 적 클래스
/// 플레이어의 순간이동(Blink) 타격 대상이 되며, 사망 시 이벤트를 발행
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

    [Header("Target Key Settings")]
    [Tooltip("이 적을 타겟팅하기 위한 키 (Q, W, E, R)")]
    [SerializeField] private KeyCode targetKey = KeyCode.Q;

    [Header("Visual Feedback")]
    [Tooltip("머리 위에 키를 표시할 TextMeshPro 컴포넌트 (비어있으면 자식 오브젝트에서 자동 검색)")]
    [SerializeField] private TMP_Text keyTextDisplay;
    [SerializeField] private Vector3 textOffset = new Vector3(0, 1.2f, 0);

    [Header("Enemy Stats")]
    [SerializeField] private float maxHealth = 1f;
    private float currentHealth;
    private bool isDead = false;

    public KeyCode TargetKey => targetKey;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        SetupKeyDisplay();
    }

    private void OnEnable()
    {
        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    private void Start()
    {
        UpdateKeyDisplay();
    }

    private void OnValidate()
    {
        UpdateKeyDisplay();
    }

    /// <summary>
    /// 스포너에서 적 생성 시 키와 스탯 동적 초기화
    /// </summary>
    public void Init(KeyCode key, float health = 1f)
    {
        targetKey = key;
        maxHealth = health;
        currentHealth = health;
        isDead = false;
        SetupKeyDisplay();
        UpdateKeyDisplay();
    }

    /// <summary>
    /// 특정 키에 매칭되는 가장 가까운 적을 탐색
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

    /// <summary>
    /// 타겟 키를 런타임에 동적으로 변경할 때 사용
    /// </summary>
    public void SetTargetKey(KeyCode newKey)
    {
        targetKey = newKey;
        UpdateKeyDisplay();
    }

    /// <summary>
    /// 플레이어가 순간이동 공격(Blink Attack)을 가했을 때 호출
    /// </summary>
    public void TakeBlinkHit(float damage = 1f)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"[Enemy:{gameObject.name}] Hit! Remaining HP: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[Enemy:{gameObject.name}] <color=#FF4444>Defeated!</color> Key: {targetKey}");
        OnEnemyDied?.Invoke(this);

        Destroy(gameObject);
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
        // 에디터 씬 뷰에서 머리 위 텍스트 위치 가이드라인 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + textOffset, 0.2f);
    }
}
