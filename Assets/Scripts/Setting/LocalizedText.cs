using UnityEngine;
using TMPro; // TextMeshPro 사용 시

public class LocalizedText : MonoBehaviour
{
    public string key; // 인스펙터에서 "START_BTN" 처럼 입력할 키
    private TextMeshProUGUI textComponent; // 일반 Text라면 Text로 변경

    // ★ [추가] 처음 인스펙터에 설정된 예쁜 폰트 크기를 기억할 변수
    private float originalFontSize;

    void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();

        // ★ [추가] 시작할 때 원래 폰트 크기를 저장해둡니다.
        if (textComponent != null)
        {
            originalFontSize = textComponent.fontSize;
        }
    }

    void Start()
    {
        UpdateText();
    }

    // 언어가 바뀔 때 호출됨
    public void UpdateText()
    {
        if (LanguageManager.Instance != null && textComponent != null)
        {
            // 1. 텍스트 내용 갱신
            textComponent.text = LanguageManager.Instance.GetText(key);

            // =========================================================
            // ★ 2. 언어별 폰트 크기 자동 조절
            // =========================================================
            LanguageManager.Language currentLang = LanguageManager.Instance.currentLanguage;

            // 한국어(Korean) 또는 영어(English)일 경우 -> 원래 크기 유지
            if (currentLang == LanguageManager.Language.Korean || currentLang == LanguageManager.Language.English)
            {
                textComponent.fontSize = originalFontSize;
            }
            // 그 외의 언어일 경우 -> 글씨 크기를 80%로 줄임
            else
            {
                textComponent.fontSize = originalFontSize * 0.8f;
            }
            // =========================================================
        }
    }
}