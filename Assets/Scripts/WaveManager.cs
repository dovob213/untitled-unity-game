using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Q, W, E, R 키를 중복 없이 할당하여 적을 스폰하고 웨이브 진행을 관리하는 매니저
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Enemy Prefab")]
    [Tooltip("스폰할 적 프리팹 (Enemy 컴포넌트 포함)")]
    [SerializeField] private Enemy enemyPrefab;

    [Header("Wave Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int startWave = 1;
    [SerializeField] private float timeBetweenSpawns = 1.2f;
    [SerializeField] private float timeBetweenWaves = 2.5f;

    [Header("Spawn Area")]
    [SerializeField] private float minSpawnRadius = 4f;
    [SerializeField] private float maxSpawnRadius = 7f;

    public static event Action<int> OnWaveStarted;
    public static event Action<int> OnWaveCleared;

    private readonly KeyCode[] keyPool = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
    private int currentWave = 1;
    private int enemiesRemainingToSpawn = 0;
    private int enemiesAlive = 0;
    private bool isWaveActive = false;
    private Transform playerTransform;

    public int CurrentWave => currentWave;
    public int EnemiesAlive => enemiesAlive;
    public bool IsWaveActive => isWaveActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    private void Start()
    {
        FindPlayer();
        if (autoStart)
        {
            StartWave(startWave);
        }
    }

    private void FindPlayer()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    public void StartWave(int waveNumber)
    {
        currentWave = waveNumber;
        enemiesRemainingToSpawn = 2 + waveNumber * 2; // 웨이브당 적 수 점진 증가
        enemiesAlive = 0;
        isWaveActive = true;

        Debug.Log($"[WaveManager] <color=#FFFF00>⚔️ WAVE {currentWave} STARTED ⚔️</color> Total Enemies: {enemiesRemainingToSpawn}");
        OnWaveStarted?.Invoke(currentWave);

        StopAllCoroutines();
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        while (enemiesRemainingToSpawn > 0)
        {
            // 화면에 4개 키가 모두 차있지 않을 때만 스폰
            KeyCode? availableKey = GetUnusedKey();
            if (availableKey.HasValue)
            {
                SpawnEnemy(availableKey.Value);
                enemiesRemainingToSpawn--;
                enemiesAlive++;
            }

            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        // 모든 적이 스폰된 후 처치 대기
        while (enemiesAlive > 0)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 웨이브 클리어
        isWaveActive = false;
        Debug.Log($"[WaveManager] <color=#00FFAA>🎉 WAVE {currentWave} CLEARED! 🎉</color>");
        OnWaveCleared?.Invoke(currentWave);

        // 다음 웨이브 준비
        yield return new WaitForSeconds(timeBetweenWaves);
        StartWave(currentWave + 1);
    }

    private KeyCode? GetUnusedKey()
    {
        var activeEnemies = Enemy.ActiveEnemies;
        List<KeyCode> usedKeys = new List<KeyCode>();

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            if (activeEnemies[i] != null && !activeEnemies[i].IsDead)
            {
                usedKeys.Add(activeEnemies[i].TargetKey);
            }
        }

        List<KeyCode> candidates = new List<KeyCode>();
        for (int i = 0; i < keyPool.Length; i++)
        {
            if (!usedKeys.Contains(keyPool[i]))
            {
                candidates.Add(keyPool[i]);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void SpawnEnemy(KeyCode key)
    {
        Vector3 spawnPos = GetRandomSpawnPosition();
        Enemy enemyInstance;

        if (enemyPrefab != null)
        {
            enemyInstance = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 프리팹 미할당 시 임시 오브젝트 동적 생성 (안전 fallback)
            GameObject fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackObj.name = $"Enemy_{key}";
            fallbackObj.transform.position = spawnPos;
            enemyInstance = fallbackObj.AddComponent<Enemy>();
        }

        enemyInstance.name = $"Enemy_{key}";
        enemyInstance.Init(key, 1f);
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (playerTransform == null) FindPlayer();
        Vector3 center = playerTransform != null ? playerTransform.position : Vector3.zero;

        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distance = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;

        return center + offset;
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
    }

    private void OnDrawGizmosSelected()
    {
        // 에디터에서 스폰 반경 시각화
        Vector3 center = playerTransform != null ? playerTransform.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, minSpawnRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, maxSpawnRadius);
    }
}
