using UnityEngine;
using System.Collections;

public class TutorialDoor : MonoBehaviour
{
    [Header("문 오브젝트")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("설정")]
    public float openDistance = 2.0f;
    public float openSpeed = 2.0f;
    public bool isOpened = false;

    public void OpenDoor()
    {
        if (isOpened) return;
        isOpened = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Vector3 leftStart = leftDoor.localPosition;
        Vector3 rightStart = rightDoor.localPosition;

        // [수정됨] 문이 열리는 방향을 Z축(forward/back)으로 변경
        // 만약 문이 반대로(안쪽으로) 열리면 forward와 back을 서로 바꾸세요.
        Vector3 leftEnd = leftStart + Vector3.forward * openDistance;
        Vector3 rightEnd = rightStart + Vector3.back * openDistance;

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;

            leftDoor.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
            rightDoor.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
            yield return null;
        }
    }
}