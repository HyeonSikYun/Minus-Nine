using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameIntroManager : MonoBehaviour
{
    [Header("1. 2초간 보여질 검은 배경 (신규)")]
    public Image initialBlackPanel; // 새로 만들어서 연결할 일반 검은색 이미지

    [Header("2. 구멍 뚫리는 쉐이더 이미지 (기존)")]
    public Image irisOverlay;
    private Material overlayMat;

    [Header("3. 텍스트 UI 그룹 (기존)")]
    public CanvasGroup introUIGroup;

    [Header("4. 플레이어 연결 (이동 제어용)")]
    public PlayerController playerController; // 플레이어 스크립트 연결

    [Header("연출 설정")]
    public float initialWaitTime = 2.0f; // 처음에 멈춰있는 시간 (2초)
    public float expandDuration = 2.0f;  // 구멍이 커지는 데 걸리는 시간
    public float textDelay = 0.5f;       // 구멍 커지기 시작 후 텍스트 나올 때까지 대기
    public float textFadeDuration = 1.0f; // 텍스트 밝아지는 시간

    private void Start()
    {
        // 1. 플레이어 조작 차단
        if (playerController != null) playerController.enabled = false;

        // 2. 초기 검은 화면 켜기 (완전 암전)
        if (initialBlackPanel != null)
        {
            initialBlackPanel.gameObject.SetActive(true);
            initialBlackPanel.color = Color.black; // 확실하게 검은색
        }

        // 3. 쉐이더 이미지 설정 (구멍 크기 0으로 초기화)
        if (irisOverlay != null)
        {
            overlayMat = Instantiate(irisOverlay.material);
            irisOverlay.material = overlayMat;
            overlayMat.SetFloat("_Radius", 0f); // 구멍 닫힌 상태
            irisOverlay.gameObject.SetActive(true);
        }

        // 4. 텍스트 그룹 숨기기
        if (introUIGroup != null)
        {
            introUIGroup.alpha = 0f;
            introUIGroup.gameObject.SetActive(true);
        }

        // 5. 시퀀스 시작
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // =================================================
        // [1단계] 2초 동안 완전 암전 상태 유지 (대기)
        // =================================================
        yield return new WaitForSeconds(initialWaitTime);


        // =================================================
        // [2단계] 2초 지남 -> 검은막 제거 & 플레이어 해방 & 쉐이더 연출 시작
        // =================================================

        // 1. 검은 화면 끄기 (이제 뒤에 있던 쉐이더 이미지가 보임)
        if (initialBlackPanel != null) initialBlackPanel.gameObject.SetActive(false);

        // 2. 플레이어 이동 허용
        if (playerController != null) playerController.enabled = true;
        SoundManager.Instance.PlayBGM(SoundManager.Instance.tutorialBgm);
        // 3. 구멍 넓히기 연출 시작 (기존 로직)
        float timer = 0f;
        bool textFadeStarted = false;

        while (timer < expandDuration)
        {
            timer += Time.deltaTime;

            // 구멍 크기 키우기 (0 -> 1.2)
            float progress = timer / expandDuration;
            float currentRadius = Mathf.Lerp(0f, 1.2f, progress);

            if (overlayMat != null)
                overlayMat.SetFloat("_Radius", currentRadius);

            // 중간에 텍스트 페이드인 시작
            if (!textFadeStarted && timer >= textDelay)
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

        float timer = 0f;
        while (timer < textFadeDuration)
        {
            timer += Time.deltaTime;
            introUIGroup.alpha = Mathf.Lerp(0f, 1f, timer / textFadeDuration);
            yield return null;
        }
        introUIGroup.alpha = 1f;
    }
}