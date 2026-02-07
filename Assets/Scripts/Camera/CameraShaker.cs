using UnityEngine;
using System.Collections;

public class CameraShaker : MonoBehaviour
{
    // 싱글톤 패턴을 사용하여 어디서든 쉽게 접근할 수 있게 합니다.
    public static CameraShaker Instance { get; private set; }

    private Vector3 originalPos;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        Instance = this;
        // 카메라의 원래 로컬 위치를 저장합니다.
        originalPos = transform.localPosition;
    }

    // 외부에서 이 함수를 호출해서 흔듭니다.
    // duration: 흔들리는 시간 (초)
    // magnitude: 흔들리는 강도 (범위)
    public void Shake(float duration, float magnitude)
    {
        // 이미 흔들리고 있다면 멈추고 새로 시작
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // -1 ~ 1 사이의 랜덤한 값을 x, y에 적용하여 흔들림 위치 계산
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // 원래 위치를 기준으로 랜덤하게 위치 변경
            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            // 다음 프레임까지 대기
            yield return null;
        }

        // 흔들림이 끝나면 원래 위치로 복귀 (중요!)
        transform.localPosition = originalPos;
        shakeCoroutine = null;
    }
}