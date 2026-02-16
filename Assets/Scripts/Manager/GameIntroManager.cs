using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameIntroManager : MonoBehaviour
{
    [Header("1. 대기용 검은 배경 (이미지)")]
    public Image initialBlackPanel;

    [Header("2. 구멍 뚫리는 쉐이더 이미지")]
    public Image irisOverlay;
    private Material overlayMat;

    [Header("3. 텍스트 UI 그룹")]
    public CanvasGroup introUIGroup;

    [Header("4. 플레이어 연결")]
    public PlayerController playerController;

    [Header("첫 시작 연출 설정")]
    public float initialWaitTime = 2.0f; // 첫 시작 대기 (2초)
    public float expandDuration = 2.0f;  // 구멍 열리는 시간 (2초)

    [Header("재시작 연출 설정")]
    public float retryWaitTime = 1.0f;   // ★ 재시작 대기 (1초)
    // 구멍 열리는 시간은 expandDuration(2초)를 공통으로 사용합니다.

    private void Start()
    {
        // 1. 쉐이더 준비 (공통)
        if (irisOverlay != null)
        {
            overlayMat = Instantiate(irisOverlay.material);
            irisOverlay.material = overlayMat;
            overlayMat.SetFloat("_Radius", 0f); // 구멍 닫음
            irisOverlay.gameObject.SetActive(true);
        }

        // 2. 플레이어 조작 차단 (공통)
        if (playerController != null) playerController.enabled = false;

        // 3. 검은 화면 켜기 (공통)
        // 재시작 때도 1초 대기가 생겼으므로, 무조건 켜야 합니다.
        if (initialBlackPanel != null)
        {
            initialBlackPanel.gameObject.SetActive(true);
            initialBlackPanel.color = Color.black;
        }

        // 4. 텍스트 그룹 초기화
        if (introUIGroup != null)
        {
            introUIGroup.alpha = 0f;
            // 텍스트는 첫 시작일 때만 켤 것이므로 일단 켜두고 코루틴에서 제어하거나, 
            // 아예 여기서 켜두고 재시작일 때만 안 보이게 처리해도 됩니다.
            introUIGroup.gameObject.SetActive(true);
        }

        // 5. 재시작 여부 확인 후 코루틴 시작
        bool isRetry = (GameManager.Instance != null && GameManager.Instance.isRetry);
        StartCoroutine(PlayIntroSequence(isRetry));
    }

    private IEnumerator PlayIntroSequence(bool isRetryMode)
    {
        // =================================================
        // [1단계] 대기 시간 (첫 시작 2초 / 재시작 1초)
        // =================================================

        float currentWaitTime = isRetryMode ? retryWaitTime : initialWaitTime;
        yield return new WaitForSeconds(currentWaitTime);

        // =================================================
        // [2단계] 연출 시작
        // =================================================

        // 1. 대기용 검은 패널 끄기 -> 쉐이더 구멍이 보이기 시작
        if (initialBlackPanel != null) initialBlackPanel.gameObject.SetActive(false);

        // 2. 플레이어 이동 허용
        if (playerController != null) playerController.enabled = true;

        // 3. BGM 처리
        if (!isRetryMode)
        {
            // 첫 시작: 튜토리얼 BGM 재생
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayBGM(SoundManager.Instance.tutorialBgm);
        }
        else
        {
            // 재시작: BGM 변경 없음 (GameManager가 틀어둔 엘리베이터 소음 유지)
            // 텍스트 패널 숨기기 (재시작 땐 텍스트 안 나오게)
            if (introUIGroup != null) introUIGroup.gameObject.SetActive(false);
        }

        // 4. 구멍 넓히기 (공통 2초)
        float timer = 0f;
        bool textFadeStarted = false;

        // 재시작이어도 구멍 열리는 시간은 expandDuration(2초) 사용
        while (timer < expandDuration)
        {
            timer += Time.deltaTime;

            // 0 -> 1.2 까지 커짐
            float progress = timer / expandDuration;
            float currentRadius = Mathf.Lerp(0f, 1.2f, progress);

            if (overlayMat != null)
                overlayMat.SetFloat("_Radius", currentRadius);

            // 텍스트 페이드인 (첫 시작일 때만!)
            if (!isRetryMode && !textFadeStarted && timer >= 0.5f)
            {
                textFadeStarted = true;
                StartCoroutine(FadeInUI());
            }

            yield return null;
        }

        // 최종적으로 구멍 완전히 열기
        if (overlayMat != null) overlayMat.SetFloat("_Radius", 1.5f);
    }

    private IEnumerator FadeInUI()
    {
        if (introUIGroup == null) yield break;
        float duration = 1.0f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            introUIGroup.alpha = Mathf.Lerp(0f, 1f, timer / duration);
            yield return null;
        }
        introUIGroup.alpha = 1f;
    }
}