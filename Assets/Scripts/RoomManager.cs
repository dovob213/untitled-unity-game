using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특정 구역(Room) 내의 적들을 관리하고, 전원 처치 시 다음 구역 개방 이벤트를 발생시키는 매니저
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private string roomName = "Room A";
    [SerializeField] private bool autoActivateOnStart = false;

    [Header("Enemies in Room")]
    [Tooltip("이 방에 속한 적 목록 (비어있을 경우 자식 오브젝트에서 자동 수집)")]
    [SerializeField] private List<Enemy> roomEnemies = new List<Enemy>();

    public event Action OnRoomCleared;
    public event Action OnRoomActivated;

    private bool isRoomActive = false;
    private bool isRoomCleared = false;

    public string RoomName => roomName;
    public bool IsRoomActive => isRoomActive;
    public bool IsRoomCleared => isRoomCleared;
    public int RemainingEnemiesCount => roomEnemies.Count;

    private void Awake()
    {
        // 수동 할당이 안 되어 있다면 자식 오브젝트의 Enemy 자동 탐색
        if (roomEnemies.Count == 0)
        {
            roomEnemies.AddRange(GetComponentsInChildren<Enemy>(true));
        }

        // 시작 시 비활성화 상태로 대기 (autoActivateOnStart가 false인 경우)
        if (!autoActivateOnStart)
        {
            for (int i = 0; i < roomEnemies.Count; i++)
            {
                if (roomEnemies[i] != null)
                {
                    roomEnemies[i].gameObject.SetActive(false);
                }
            }
        }
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
        if (autoActivateOnStart)
        {
            ActivateRoom();
        }
    }

    /// <summary>
    /// 플레이어가 방에 진입했을 때 호출되어 적들을 활성화하고 전투 개시
    /// </summary>
    public void ActivateRoom()
    {
        if (isRoomActive || isRoomCleared) return;
        isRoomActive = true;

        Debug.Log($"[RoomManager] <color=#FFAA00>🚪 [{roomName}] 진입! 적 {roomEnemies.Count}마리 활성화!</color>");

        for (int i = 0; i < roomEnemies.Count; i++)
        {
            Enemy enemy = roomEnemies[i];
            if (enemy != null)
            {
                enemy.gameObject.SetActive(true);
            }
        }

        OnRoomActivated?.Invoke();
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        if (!roomEnemies.Contains(enemy)) return;

        roomEnemies.Remove(enemy);
        Debug.Log($"[RoomManager] [{roomName}] 적 처치! 남은 적: {roomEnemies.Count}마리");

        if (roomEnemies.Count == 0 && !isRoomCleared)
        {
            ClearRoom();
        }
    }

    private void ClearRoom()
    {
        isRoomCleared = true;
        isRoomActive = false;

        Debug.Log($"[RoomManager] <color=#00FFAA>🎉 [{roomName} CLEARED] 모든 적 처치 완료! 다음 구역 문 개방!</color>");
        OnRoomCleared?.Invoke();
    }
}
