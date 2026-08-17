using System.Collections;
using UnityEngine;

/// <summary>
/// 특정 방(RoomManager)의 클리어 상태에 따라 열리고 닫히는 문(Door) 컨트롤러
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    [Header("Target Room")]
    [Tooltip("이 문을 열기 위해 클리어해야 하는 방 매니저")]
    [SerializeField] private RoomManager targetRoom;

    [Header("Door Settings")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private float openSpeed = 5f;
    [SerializeField] private Vector2 openSlideOffset = new Vector2(0, 3f);

    private Collider2D col;
    private SpriteRenderer sr;
    private Vector3 closedPos;
    private Vector3 openPos;
    private Coroutine moveCoroutine;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        closedPos = transform.position;
        openPos = closedPos + (Vector3)openSlideOffset;

        if (isOpen)
        {
            transform.position = openPos;
            if (col != null) col.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (targetRoom != null)
        {
            targetRoom.OnRoomCleared += OpenDoor;
        }
    }

    private void OnDisable()
    {
        if (targetRoom != null)
        {
            targetRoom.OnRoomCleared -= OpenDoor;
        }
    }

    /// <summary>
    /// 문 열기 (슬라이딩 연출 및 통행 가능 콜라이더 해제)
    /// </summary>
    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;

        Debug.Log($"[DoorController:{name}] <color=#00FFAA>🚪 [DOOR OPENED] 문이 열렸습니다! 다음 구역 통행 가능.</color>");

        if (col != null) col.enabled = false;

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveDoorRoutine(openPos));
    }

    /// <summary>
    /// 문 닫기 (플레이어 진입 후 등 뒤 차단용)
    /// </summary>
    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;

        Debug.Log($"[DoorController:{name}] <color=#FF5555>🔒 [DOOR LOCKED] 문이 닫혔습니다! 전투 구역 봉쇄.</color>");

        if (col != null) col.enabled = true;

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveDoorRoutine(closedPos));
    }

    private IEnumerator MoveDoorRoutine(Vector3 targetPos)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, openSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        moveCoroutine = null;
    }
}
