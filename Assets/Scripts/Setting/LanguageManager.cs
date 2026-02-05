using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    public enum Language { Korean, English }
    public Language currentLanguage;

    // 텍스트 데이터 (Key, [한국어, 영어])
    private Dictionary<string, string[]> localizedData = new Dictionary<string, string[]>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        InitLocalizationData();
    }

    private void Start()
    {
        // 저장된 언어 불러오기 (기본값: 한국어(0))
        int langIndex = PlayerPrefs.GetInt("Language", 0);

        // [중요] 값을 넣기 전에, 현재 언어 기준으로 옵션 텍스트를 먼저 생성해야 함
        currentLanguage = (Language)langIndex;
        RefreshLanguageDropdown();

        // 그 다음 값 설정
        languageDropdown.value = langIndex;

        // 전체 텍스트 갱신
        ChangeLanguage(langIndex);
    }

    // 언어 데이터 등록
    void InitLocalizationData()
    {
        localizedData.Add("Upgrade_Heal", new string[] { "체력 회복\n필요 샘플: {0}", "Heal \nCost: {0}" });
        localizedData.Add("Upgrade_Damage", new string[] { "공격력 강화\n필요 샘플: {0}", "Damage\nCost: {0}" });
        localizedData.Add("Upgrade_Ammo", new string[] { "탄약 확장\n필요 샘플: {0}", "Ammo \nCost: {0}" });
        localizedData.Add("Upgrade_Speed", new string[] { "속도 증가\n필요 샘플: {0}", "Speed \nCost: {0}" });
        localizedData.Add("Resume_Btn", new string[] { "계속 하기", "Resume" });
        localizedData.Add("Option_Btn", new string[] { "설정", "Option" });
        localizedData.Add("Exit_Btn", new string[] { "게임 종료", "Exit Game" });
        localizedData.Add("Opt_BgmText", new string[] { "배경음악", "BGM" });
        localizedData.Add("Opt_SFXText", new string[] { "효과음", "SFX" });
        localizedData.Add("Opt_DisplayText", new string[] { "디스플레이", "Display" });
        localizedData.Add("Opt_DisplayFull", new string[] { "전체화면", "FullScreen" });
        localizedData.Add("Opt_DisplayWindow", new string[] { "창모드", "Windowed" });
        localizedData.Add("Opt_Resolution", new string[] { "해상도", "Resolution" });
        localizedData.Add("Opt_LanguageText", new string[] { "언어", "Language" });
        localizedData.Add("Opt_LanguageKor", new string[] { "한국어", "Korean" });
        localizedData.Add("Opt_LanguageEng", new string[] { "영어", "English" });

        localizedData.Add("TUTORIAL_MOVE", new string[] { "WASD를 눌러 이동하세요.", "Press WASD to move." });
        localizedData.Add("TUTORIAL_GunPickup", new string[] { "전방의 무기를 획득하세요.", "Acquire the weapon ahead." });
        localizedData.Add("TUTORIAL_GunShoot", new string[] { "프로토타입 무기 가동.\n[L-Click]으로 타겟을 제거하세요.", "Prototype weapon activated.\nEliminate targets with [L-Click]." });
        localizedData.Add("TUTORIAL_Sample", new string[] { "바이오 캡슐을 획득하세요.", "Collect Bio Capsules." });
        localizedData.Add("TUTORIAL_Tap", new string[] { "[TAB] 키를 눌러 능력치를 강화하세요.", "Press [TAB] to upgrade your abilities." });
        localizedData.Add("TUTORIAL_FinUpgrade", new string[] { "보안 프로토콜 해제.\n다음 구역으로 이동하십시오.", "Security protocol disabled.\nProceed to the next sector." });
        localizedData.Add("TUTORIAL_Generator", new string[] { "발전기를 가동하여 엘리베이터 전력을 공급하세요.", "Activate the generator to power the elevator." });
        localizedData.Add("TUTORIAL_Fin", new string[] { "목표 갱신: 최상층(지상)으로 탈출하십시오.", "Objective Updated: Escape to the surface." });
    }

    public void ChangeLanguage(int index)
    {
        currentLanguage = (Language)index;
        PlayerPrefs.SetInt("Language", index);

        // [추가] 언어 드롭다운(자기 자신)의 텍스트도 갱신
        RefreshLanguageDropdown();

        // 모든 LocalizedText 컴포넌트에게 갱신하라고 알림
        UpdateAllText();

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.RefreshCurrentMessage();
        }
    }

    // [추가됨] 언어 선택 드롭다운 자체를 새로고침하는 함수
    public void RefreshLanguageDropdown()
    {
        if (languageDropdown == null) return;

        // 1. 현재 선택된 값 기억
        int currentIndex = languageDropdown.value;

        // 2. 기존 옵션 삭제
        languageDropdown.ClearOptions();

        // 3. 새 옵션 리스트 생성 (등록한 키값 사용)
        List<string> options = new List<string>();
        options.Add(GetText("Opt_LanguageKor")); // "한국어" or "Korean"
        options.Add(GetText("Opt_LanguageEng")); // "영어" or "English"

        // 4. 드롭다운에 적용
        languageDropdown.AddOptions(options);

        // 5. 선택값 복구 (이벤트 발생 없이 값만 변경)
        languageDropdown.SetValueWithoutNotify(currentIndex);
        languageDropdown.RefreshShownValue();
    }

    // 씬에 있는 모든 LocalizedText를 찾아서 업데이트
    void UpdateAllText()
    {
        LocalizedText[] texts = FindObjectsOfType<LocalizedText>(true);
        foreach (var text in texts)
        {
            text.UpdateText();
        }

        GraphicSettings graphicSettings = FindObjectOfType<GraphicSettings>(); // 씬에서 찾기
        if (graphicSettings != null)
        {
            graphicSettings.RefreshDisplayModeOptions();
        }
    }

    // 텍스트 가져오기 함수
    public string GetText(string key)
    {
        if (localizedData.ContainsKey(key))
        {
            return localizedData[key][(int)currentLanguage];
        }
        return key;
    }
}