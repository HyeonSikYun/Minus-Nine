using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerDamageEffect : MonoBehaviour
{
    [Header("블러드 스크린 설정")]
    public Image bloodScreenImage; // 방금 만든 빨간색 Image 연결

    [Tooltip("맞았을 때 얼마나 빨개질지 (0:투명 ~ 1:완전빨강)")]
    public float maxAlpha = 0.5f; // 너무 빨개서 앞이 안 보이면 안 되니까 0.5 정도 추천

    [Tooltip("피가 사라지는 속도")]
    public float fadeSpeed = 2.0f;

    [Header("카메라 흔들림 (선택사항)")]
    public Transform cameraTransform; // 카메라 연결 (없으면 안 흔들림)
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.3f;

    private Vector3 originalPos;
    private Coroutine effectCoroutine;

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
        originalPos = cameraTransform.localPosition;
    }

    // 플레이어가 맞았을 때 이 함수를 호출하세요!
    public void OnTakeDamage()
    {
        // 코루틴이 겹치지 않게 재시작
        if (effectCoroutine != null) StopCoroutine(effectCoroutine);
        effectCoroutine = StartCoroutine(DamageEffectRoutine());
    }

    private IEnumerator DamageEffectRoutine()
    {
        // 1. 맞자마자 빨간색 확 띄우기
        if (bloodScreenImage != null)
        {
            Color c = bloodScreenImage.color;
            c.a = maxAlpha;
            bloodScreenImage.color = c;
        }

        float elapsed = 0f;

        while (elapsed < shakeDuration || (bloodScreenImage != null && bloodScreenImage.color.a > 0))
        {
            elapsed += Time.deltaTime;

            // --- 카메라 흔들기 ---
            if (elapsed < shakeDuration)
            {
                float x = Random.Range(-1f, 1f) * shakeMagnitude;
                float y = Random.Range(-1f, 1f) * shakeMagnitude;
                cameraTransform.localPosition = originalPos + new Vector3(x, y, 0);
            }
            else
            {
                // 흔들림 끝나면 원위치
                cameraTransform.localPosition = originalPos;
            }

            // --- 빨간 화면 서서히 투명하게 (페이드 아웃) ---
            if (bloodScreenImage != null)
            {
                Color c = bloodScreenImage.color;
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * fadeSpeed);
                bloodScreenImage.color = c;
            }

            yield return null;
        }

        // 깔끔한 마무리 (확실하게 원위치 및 투명화)
        cameraTransform.localPosition = originalPos;
        if (bloodScreenImage != null)
        {
            Color c = bloodScreenImage.color;
            c.a = 0f;
            bloodScreenImage.color = c;
        }
    }
}