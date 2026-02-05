using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("기본 UI")]
    public TextMeshProUGUI floorText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;
    public GameObject reloadingObject;

    [Header("재화 UI")]
    public TextMeshProUGUI bioSampleText;

    [Header("튜토리얼 UI")]
    public TextMeshProUGUI tutorialText;
    public GameObject tutorialUIGroup;

    [Header("발전기 UI")]
    public TextMeshProUGUI generatorCountText;
    public GameObject interactionPromptObj;
    public GameObject progressBarObj;
    public Image progressBarFill;

    [Header("패널")]
    public GameObject upgradePanel;
    public GameObject pausePanel;
    public GameObject settingsPanel;

    [Header("강화 메뉴 텍스트")]
    public TMPro.TextMeshProUGUI txtHealCost;
    public TMPro.TextMeshProUGUI txtDamageCost;
    public TMPro.TextMeshProUGUI txtAmmoCost;
    public TMPro.TextMeshProUGUI txtSpeedCost;

    [Header("전역 페이드 패널")]
    public CanvasGroup globalFadeCanvas;

    [Header("설정 UI 연결")]
    public TMP_Dropdown languageDropdown;    // 언어 변경 드롭다운
    public TMP_Dropdown resolutionDropdown;  // 해상도 변경 드롭다운
    public TMP_Dropdown displayModeDropdown; // 전체화면/창모드 드롭다운

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        // [중요] 여기서는 아무것도 하지 않습니다. 
        // 튜토리얼 매니저나 게임 매니저가 제어하게 둡니다.
    }

    // --- [핵심 추가] 버튼 연결용 중계 함수 (Bridge) ---
    // 유니티 에디터 버튼 OnClick에 GameManager 대신 이 함수들을 연결하세요!
    public void OnClickUpgradeHP()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("HP");
    }
    public void OnClickUpgradeDamage()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Damage");
    }
    public void OnClickUpgradeAmmo()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Ammo");
    }
    public void OnClickUpgradeSpeed()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Speed");
    }

    public void OnClickResumeBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickResume();
    }
    public void OnClickOptionsBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickOptions();
    }
    public void OnClickQuitBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickQuit();
    }
    // -----------------------------------------------------

    // [추가됨] 페이드 효과를 즉시 적용하는 함수 (재시작 시 깜빡임 방지용)
    public void SetFadeAlpha(float alpha)
    {
        if (globalFadeCanvas != null)
        {
            globalFadeCanvas.alpha = alpha;
            globalFadeCanvas.blocksRaycasts = (alpha > 0.1f);
        }
    }

    public void ShowTutorialText(string message)
    {
        // [핵심] 메시지가 비어있으면 패널을 끕니다.
        bool shouldShow = !string.IsNullOrEmpty(message);

        if (tutorialUIGroup != null)
        {
            tutorialUIGroup.SetActive(shouldShow);
        }

        if (shouldShow && tutorialText != null)
        {
            tutorialText.text = message;
        }
    }

    public void HideTutorialText()
    {
        if (tutorialUIGroup != null) tutorialUIGroup.SetActive(false);
    }

    // --- 기존 UI 함수들 ---
    public void UpdateBioSample(int amount) { if (bioSampleText != null) bioSampleText.text = $"Samples: {amount}"; }
    public void ShowUpgradePanel(bool show) { if (upgradePanel != null) upgradePanel.SetActive(show); }

    // [수정] 가격 업데이트 함수 ({0} 문제 해결)
    public void UpdateUpgradePrices(int heal, int dmg, int ammo, int spd)
    {
        // 1. LanguageManager가 없으면 그냥 리턴 (에러 방지)
        if (LanguageManager.Instance == null) return;

        // 2. {0} 자리에 실제 가격을 넣어서 완성된 문장으로 만듦
        string healFmt = LanguageManager.Instance.GetText("Upgrade_Heal");
        if (txtHealCost != null) txtHealCost.text = string.Format(healFmt, heal);

        string dmgFmt = LanguageManager.Instance.GetText("Upgrade_Damage");
        if (txtDamageCost != null) txtDamageCost.text = string.Format(dmgFmt, dmg);

        string ammoFmt = LanguageManager.Instance.GetText("Upgrade_Ammo");
        if (txtAmmoCost != null) txtAmmoCost.text = string.Format(ammoFmt, ammo);

        string speedFmt = LanguageManager.Instance.GetText("Upgrade_Speed");
        if (txtSpeedCost != null) txtSpeedCost.text = string.Format(speedFmt, spd);
    }

    public void UpdateFloor(int floorIndex) { if (floorText == null) return; string floorString = floorIndex < 0 ? $"B{Mathf.Abs(floorIndex)}" : (floorIndex == 0 ? "Lobby" : $"{floorIndex}F"); floorText.text = floorString; }
    public void UpdateHealth(int currentHealth) { if (healthText == null) return; int displayHealth = Mathf.Max(0, currentHealth); healthText.text = $"HP {displayHealth}"; healthText.color = displayHealth <= 30 ? Color.red : Color.white; }
    public void UpdateWeaponName(string name) { if (weaponNameText != null) weaponNameText.text = name; }
    public void UpdateAmmo(int current, int max) { if (ammoText != null) ammoText.text = $"{current} / {max}"; }
    public void ShowReloading(bool isReloading) { if (reloadingObject != null) reloadingObject.SetActive(isReloading); }
    public void UpdateGeneratorCount(int current, int total) { if (generatorCountText != null) generatorCountText.text = $"{current} / {total}"; }
    public void ShowInteractionPrompt(bool isVisible) { if (interactionPromptObj != null) interactionPromptObj.SetActive(isVisible); }
    public void UpdateInteractionProgress(float ratio) { bool shouldShow = ratio > 0f && ratio < 1.0f; if (progressBarObj != null) progressBarObj.SetActive(shouldShow); if (progressBarFill != null) progressBarFill.fillAmount = ratio; }
    public void ShowPausePanel(bool isOpen) { if (pausePanel != null) { pausePanel.SetActive(isOpen); if (!isOpen && settingsPanel != null) settingsPanel.SetActive(false); } }
    public void ShowSettingsPanel(bool isOpen) { if (settingsPanel != null) settingsPanel.SetActive(isOpen); }

    public IEnumerator FadeOut()
    {
        if (globalFadeCanvas == null) yield break;
        globalFadeCanvas.blocksRaycasts = true;
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime * 1.5f; globalFadeCanvas.alpha = t; yield return null; }
        globalFadeCanvas.alpha = 1f;
    }

    public IEnumerator FadeIn()
    {
        if (globalFadeCanvas == null) yield break;
        float t = 1f;
        while (t > 0f) { t -= Time.deltaTime * 1.5f; globalFadeCanvas.alpha = t; yield return null; }
        globalFadeCanvas.alpha = 0f;
        globalFadeCanvas.blocksRaycasts = false;
    }
}