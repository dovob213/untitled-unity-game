using UnityEngine;

/// <summary>
/// 플레이어가 방 입구에 진입했을 때 적들을 활성화하고, 등 뒤의 문을 닫아 전투를 개시하는 트리거
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RoomTrigger : MonoBehaviour
{
    [Header("Room Activation")]
    [Tooltip("진입 시 활성화할 방 매니저")]
    [SerializeField] private RoomManager roomToActivate;

    [Header("Door Lock (Optional)")]
    [Tooltip("진입 시 플레이어 등 뒤에서 닫을 문 (선택 사항)")]
    [SerializeField] private DoorController entranceDoorToClose;

    [Header("Settings")]
    [SerializeField] private bool triggerOnce = true;

    private Collider2D triggerCol;
    private bool hasTriggered = false;

    private void Awake()
    {
        triggerCol = GetComponent<Collider2D>();
        if (triggerCol != null)
        {
            triggerCol.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered && triggerOnce) return;

        // PlayerController 탐색 (루트 및 자식 콜라이더 모두 대응)
        PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();

        if (player != null || other.CompareTag("Player") || other.name.Contains("Player"))
        {
            hasTriggered = true;
            Debug.Log($"[RoomTrigger:{name}] <color=#FFAA00>🚩 [TRIGGER ACTIVATED] 플레이어가 {roomToActivate?.RoomName ?? "방"} 입구에 진입했습니다!</color>");

            // 1. 방 활성화 (소속된 적들 활성화)
            if (roomToActivate != null)
            {
                roomToActivate.ActivateRoom();
            }

            // 2. 등 뒤 문 폐쇄 (퇴로 차단)
            if (entranceDoorToClose != null)
            {
                entranceDoorToClose.CloseDoor();
            }

            if (triggerOnce)
            {
                if (triggerCol != null) triggerCol.enabled = false;
            }
        }
    }

    private void OnDrawGizmos()
    {
        // 씬 뷰에서 트리거 영역 시각화 (노란색 와이어 박스)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.4f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
