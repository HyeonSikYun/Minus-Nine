using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 매니저에게 "나 복도 들어왔어!" 라고 알림
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnPlayerEnterCorridor();
            }

            // 할 일 다 했으니 이 트리거는 삭제 (두 번 다시 작동 안 함)
            Destroy(gameObject);
        }
    }
}