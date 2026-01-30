using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameIntroManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Image irisOverlay;       // 검은 화면 (구멍 뚫리는 쉐이더 이미지)
    public CanvasGroup introUIGroup; // [수정됨] 텍스트+패널을 묶은 그룹

    [Header("연출 설정")]
    public float expandDuration = 2.0f; // 동그라미가 커지는 시간
    public float textDelay = 0.5f;      // 텍스트 등장 대기 시간
    public float textFadeDuration = 1.0f; // 텍스트(그룹)가 밝아지는 시간

    private Material overlayMat;

    private void Start()
    {
        // 1. 재질 인스턴스 생성
        if (irisOverlay != null)
        {
            overlayMat = Instantiate(irisOverlay.material);
            irisOverlay.material = overlayMat;
            overlayMat.SetFloat("_Radius", 0f);
        }

        // 2. [수정됨] 그룹 전체 숨기기 (CanvasGroup Alpha 조절)
        if (introUIGroup != null)
        {
            introUIGroup.alpha = 0f; // 투명하게 시작
            introUIGroup.gameObject.SetActive(true);
        }

        // 3. 연출 시작
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        float timer = 0f;
        bool textFadeStarted = false;

        while (timer < expandDuration)
        {
            timer += Time.deltaTime;

            // 구멍 크기 조절
            float progress = timer / expandDuration;
            float currentRadius = Mathf.Lerp(0f, 1.2f, progress);

            if (overlayMat != null)
                overlayMat.SetFloat("_Radius", currentRadius);

            // 0.5초 뒤 텍스트(그룹) 페이드인 시작
            if (!textFadeStarted && timer >= textDelay)
            {
                textFadeStarted = true;
                StartCoroutine(FadeInUI());
            }

            yield return null;
        }

        if (overlayMat != null) overlayMat.SetFloat("_Radius", 1.5f);
    }

    // [수정됨] 그룹 전체 페이드인 함수
    private IEnumerator FadeInUI()
    {
        if (introUIGroup == null) yield break;

        float timer = 0f;

        while (timer < textFadeDuration)
        {
            timer += Time.deltaTime;
            // Alpha 값을 0에서 1로 부드럽게 변경
            introUIGroup.alpha = Mathf.Lerp(0f, 1f, timer / textFadeDuration);
            yield return null;
        }

        introUIGroup.alpha = 1f;
    }
}