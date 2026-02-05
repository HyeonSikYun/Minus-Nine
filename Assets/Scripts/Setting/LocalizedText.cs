using UnityEngine;
using TMPro; // TextMeshPro 사용 시

public class LocalizedText : MonoBehaviour
{
    public string key; // 인스펙터에서 "START_BTN" 처럼 입력할 키
    private TextMeshProUGUI textComponent; // 일반 Text라면 Text로 변경

    void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
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
            textComponent.text = LanguageManager.Instance.GetText(key);
        }
    }
}