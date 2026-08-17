using UnityEngine;

/// <summary>
/// 스테이지 출구(탈출구) 또는 최종 목표 지점에 배치되어,
/// 플레이어가 도달했을 때 GameManager에 StageClear를 통보하는 트리거 컴포넌트
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StageExitTrigger : MonoBehaviour
{
    [Header("Prerequisite (Optional)")]
    [Tooltip("출구가 열리기 위해 먼저 클리어되어야 하는 최종 방 (선택 사항)")]
    [SerializeField] private RoomManager requiredFinalRoom;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer exitRenderer;
    [SerializeField] private Color unlockedColor = new Color(0f, 1f, 0.7f, 0.9f);
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    private Collider2D col;
    private bool isUnlocked = true;
    private bool hasTriggered = false;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        if (exitRenderer == null) exitRenderer = GetComponent<SpriteRenderer>();

        if (requiredFinalRoom != null)
        {
            isUnlocked = requiredFinalRoom.IsRoomCleared;
            UpdateVisual();
        }
    }

    private void OnEnable()
    {
        if (requiredFinalRoom != null)
        {
            requiredFinalRoom.OnRoomCleared += UnlockExit;
        }
    }

    private void OnDisable()
    {
        if (requiredFinalRoom != null)
        {
            requiredFinalRoom.OnRoomCleared -= UnlockExit;
        }
    }

    private void UnlockExit()
    {
        isUnlocked = true;
        UpdateVisual();
        Debug.Log("[StageExitTrigger] 🚪 탈출구가 개방되었습니다! 탈출구로 이동하세요.");
    }

    private void UpdateVisual()
    {
        if (exitRenderer != null)
        {
            exitRenderer.color = isUnlocked ? unlockedColor : lockedColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked || hasTriggered) return;

        PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
        if (player != null || other.CompareTag("Player"))
        {
            hasTriggered = true;
            GameManager.Instance?.OnStageClear();
        }
    }
}
