using UnityEngine;

public class DoorTeleporter : MonoBehaviour
{
    [Header("목표 방 위치")]
    [SerializeField] private Transform targetRoomPosition;

    [Header("목표 방 오브젝트")]
    [SerializeField] private GameObject targetRoom;

    private RoomManager roomManager;
    private bool hasUsed = false; // 한 번만 사용 가능하도록

    private void Start()
    {
        roomManager = FindObjectOfType<RoomManager>();
        if (roomManager == null)
        {
            Debug.LogError("RoomManager를 찾을 수 없습니다!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasUsed)
        {
            TeleportPlayer(other.transform);
            hasUsed = true; // 사용 완료 표시
        }
    }

    private void TeleportPlayer(Transform player)
    {
        if (targetRoomPosition == null || targetRoom == null)
        {
            Debug.LogWarning("목표 방 위치 또는 방 오브젝트가 설정되지 않았습니다!");
            return;
        }

        // 목표 방 활성화
        if (roomManager != null)
        {
            roomManager.ShowRoom(targetRoom);
        }

        // CharacterController 처리
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // 플레이어 워프
        player.position = targetRoomPosition.position;
        player.rotation = targetRoomPosition.rotation;

        // CharacterController 재활성화
        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}