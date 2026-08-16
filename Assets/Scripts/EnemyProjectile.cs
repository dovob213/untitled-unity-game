using UnityEngine;

/// <summary>
/// 적이 발사하는 저속 투사체 (플레이어가 회피하거나 타이밍을 잴 수 있는 탄환)
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private float lifeTime = 5f;

    private Vector2 direction = Vector2.down;
    private bool isInitialized = false;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 투사체 발사 방향 및 속도 초기화
    /// </summary>
    public void Init(Vector2 dir, float customSpeed = 3.5f, float customDamage = 1f)
    {
        direction = dir.normalized;
        speed = customSpeed;
        damage = customDamage;
        isInitialized = true;

        // 투사체 회전 (날아가는 방향을 향하도록)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * (speed * Time.deltaTime));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 피격 판정
        if (collision.TryGetComponent<PlayerController>(out var player))
        {
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
