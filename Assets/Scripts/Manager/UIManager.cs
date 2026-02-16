using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems; // [필수] UI 포커스 제어용
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("기본 UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;

    [Header("무기 슬롯 UI")]
    public GameObject weaponSlotPanel;
    public Image[] weaponSlotImages;
    public Sprite[] normalWeaponSprites;
    public Sprite[] lockedWeaponSprites;
    public GameObject[] nextLabels;
    public Color nextPreviewColor = new Color(0f, 0f, 0f, 0.6f);
    public Color activeColor = Color.white;
    public Color unlockedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    private Vector3[] originalScales;

    [Header("층수UI")]
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private RectTransform[] floorAnchors;
    [SerializeField] private GameObject floorPanel;
    [SerializeField] private float iconMoveSpeed = 2f;
    [SerializeField] private float yOffset = -50f;

    [Header("장전(Reload) UI")]
    public GameObject reloadGaugeGroup;
    public Image reloadGaugeFill;
    private Coroutine currentReloadRoutine;

    [Header("재화 UI")]
    public TextMeshProUGUI bioSampleText;
    public Image bioSampleImg;

    [Header("튜토리얼 UI")]
    public TextMeshProUGUI tutorialText;
    public GameObject tutorialUIGroup;

    [Header("발전기 UI")]
    public TextMeshProUGUI generatorCountText;
    public GameObject interactionPromptObj;
    public GameObject progressBarObj;
    public Image progressBarFill;
    private int curGen = 0;
    private int totalGen = 0;

    [Header("미션 알림 UI")]
    public TextMeshProUGUI missionText;
    public CanvasGroup missionPanelGroup;

    [Header("패널 & 버튼 (패드 지원)")]
    public GameObject upgradePanel;
    public GameObject firstUpgradeBtn; // ★ [추가] 강화창 열릴 때 자동 선택될 버튼

    public GameObject pausePanel;
    public GameObject firstPauseBtn;   // ★ [추가] 일시정지 창 열릴 때 자동 선택될 버튼

    public GameObject settingsPanel;
    public GameObject firstSettingsUi;
    public GameObject quitPanel;
    public GameObject firstQuitBtn;    // ★ [추가] 종료 창 열릴 때 자동 선택될 버튼

    [Header("강화 메뉴 텍스트")]
    public TMPro.TextMeshProUGUI txtHealCost;
    public TMPro.TextMeshProUGUI txtDamageCost;
    public TMPro.TextMeshProUGUI txtAmmoCost;
    public TMPro.TextMeshProUGUI txtSpeedCost;

    [Header("전역 페이드 패널")]
    public CanvasGroup globalFadeCanvas;

    [Header("설정 UI 연결")]
    public TMP_Dropdown languageDropdown;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown displayModeDropdown;

    private int currentMissionCount = 0;
    private Coroutine iconMoveCoroutine;
    private bool isEnding = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }

        if (weaponSlotImages != null)
        {
            originalScales = new Vector3[weaponSlotImages.Length];
            for (int i = 0; i < weaponSlotImages.Length; i++)
            {
                if (weaponSlotImages[i] != null)
                {
                    originalScales[i] = weaponSlotImages[i].rectTransform.localScale;
                }
            }
        }
    }

    private void Start()
    {
        ResetGameUI();
        ShowGeneratorUI(false);
        if (missionPanelGroup != null)
        {
            missionPanelGroup.alpha = 0f;
            missionPanelGroup.gameObject.SetActive(false);
        }
    }

    public void ShowUpgradePanel(bool show)
    {
        if (upgradePanel != null)
        {
            upgradePanel.SetActive(show);

            // ★ [수정됨] GameManager.Instance.isUsingGamepad가 true일 때만 포커스
            if (show && firstUpgradeBtn != null && GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstUpgradeBtn);
            }
        }
    }

    // 2. 일시정지 창
    public void ShowPausePanel(bool isOpen)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(isOpen);
            if (!isOpen && settingsPanel != null) settingsPanel.SetActive(false);

            // ★ [수정됨] 패드 모드일 때만 포커스
            if (isOpen && firstPauseBtn != null && GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstPauseBtn);
            }
        }
    }

    // 3. 종료 확인 창
    public void ShowQuitConfirmPanel(bool isShow)
    {
        if (quitPanel)
        {
            quitPanel.SetActive(isShow);

            // ★ [수정됨] 패드 모드일 때만 포커스
            if (isShow && firstQuitBtn != null && GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstQuitBtn);
            }
        }
    }

    public void ShowSettingsPanel(bool isOpen)
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(isOpen);

            // ★ [핵심 추가] 패드 사용 중이면 첫 번째 UI(BGM 슬라이더) 강제 선택
            if (isOpen && firstSettingsUi != null && GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstSettingsUi);
            }
        }
    }

    // ----------------------------------------------------------------

    // (기존 함수들 그대로 유지)
    public void OnClickUpgradeHP() { if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("HP"); }
    public void OnClickUpgradeDamage() { if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Damage"); }
    public void OnClickUpgradeAmmo() { if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Ammo"); }
    public void OnClickUpgradeSpeed() { if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Speed"); }

    public void OnClickResumeBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickResume(); }
    public void OnClickOptionsBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickOptions(); }
    public void OnClickQuitBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickQuit(); }
    public void OnClickOptionsBackBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickOptionsBack(); }

    public void OnClickQuitYesBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickQuitYes(); }
    public void OnClickQuitNoBridge() { if (GameManager.Instance != null) GameManager.Instance.OnClickQuitNo(); }

    public void SetFadeAlpha(float alpha)
    {
        if (globalFadeCanvas != null)
        {
            globalFadeCanvas.alpha = alpha;
            globalFadeCanvas.blocksRaycasts = (alpha > 0.1f);
        }
    }

    public void ShowGeneratorUI(bool isShow) { if (generatorCountText != null) generatorCountText.gameObject.SetActive(isShow); }

    public void ShowMissionStartMessage(int count)
    {
        currentMissionCount = count;
        ShowGeneratorUI(true);
        RefreshMissionText();
        if (missionPanelGroup != null) StartCoroutine(MissionFadeRoutine());
    }

    public void RefreshMissionText()
    {
        if (missionText != null && LanguageManager.Instance != null)
        {
            string format = LanguageManager.Instance.GetText("Mission_Start");
            missionText.text = string.Format(format, currentMissionCount);
        }
    }

    private IEnumerator MissionFadeRoutine()
    {
        missionPanelGroup.gameObject.SetActive(true);
        missionPanelGroup.alpha = 0f;
        float timer = 0f;
        while (timer < 0.5f) { timer += Time.deltaTime; missionPanelGroup.alpha = Mathf.Lerp(0f, 1f, timer / 0.5f); yield return null; }
        missionPanelGroup.alpha = 1f;
        yield return new WaitForSeconds(2.0f);
        timer = 0f;
        while (timer < 1.0f) { timer += Time.deltaTime; missionPanelGroup.alpha = Mathf.Lerp(1f, 0f, timer / 1.0f); yield return null; }
        missionPanelGroup.alpha = 0f;
        missionPanelGroup.gameObject.SetActive(false);
    }


    public void ShowTutorialText(string message)
    {
        bool shouldShow = !string.IsNullOrEmpty(message);
        if (tutorialUIGroup != null) tutorialUIGroup.SetActive(shouldShow);
        if (shouldShow && tutorialText != null) tutorialText.text = message;
    }
    public void HideTutorialText() { if (tutorialUIGroup != null) tutorialUIGroup.SetActive(false); }

    public void UpdateBioSample(int amount) { if (bioSampleText != null) bioSampleText.text = $"X {amount}"; }

    public void UpdateUpgradePrices(int healCost, int dmgCost, int ammoCost, int spdCost, int dmgVal, int ammoVal, float spdVal)
    {
        if (LanguageManager.Instance == null) return;
        string healFmt = LanguageManager.Instance.GetText("Upgrade_Heal");
        if (txtHealCost != null) txtHealCost.text = string.Format(healFmt, healCost);
        string dmgFmt = LanguageManager.Instance.GetText("Upgrade_Damage");
        if (txtDamageCost != null) txtDamageCost.text = string.Format(dmgFmt, dmgCost, dmgVal);
        string ammoFmt = LanguageManager.Instance.GetText("Upgrade_Ammo");
        if (txtAmmoCost != null) txtAmmoCost.text = string.Format(ammoFmt, ammoCost, ammoVal);
        string speedFmt = LanguageManager.Instance.GetText("Upgrade_Speed");
        if (txtSpeedCost != null) txtSpeedCost.text = string.Format(speedFmt, spdCost, spdVal.ToString("F1"));
    }

    public void UpdateHealth(int currentHealth) { if (healthText == null) return; int displayHealth = Mathf.Max(0, currentHealth); healthText.text = $"HP {displayHealth}"; healthText.color = displayHealth <= 30 ? Color.red : Color.white; }
    public void UpdateWeaponName(string name) { if (weaponNameText != null) weaponNameText.text = name; }
    public void UpdateAmmo(int current, int max) { if (ammoText != null) ammoText.text = $"{current} / {max}"; }

    public void ShowReloading(bool isReloading)
    {
        if (isReloading)
        {
            if (reloadGaugeGroup != null)
            {
                reloadGaugeGroup.SetActive(true);
                if (currentReloadRoutine != null) StopCoroutine(currentReloadRoutine);
                currentReloadRoutine = StartCoroutine(ReloadBarRoutine(3.0f));
            }
        }
        else
        {
            if (reloadGaugeGroup != null) reloadGaugeGroup.SetActive(false);
            if (currentReloadRoutine != null) StopCoroutine(currentReloadRoutine);
        }
    }
    IEnumerator ReloadBarRoutine(float duration)
    {
        float timer = 0f;
        if (reloadGaugeFill != null) reloadGaugeFill.fillAmount = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (reloadGaugeFill != null) reloadGaugeFill.fillAmount = timer / duration;
            yield return null;
        }
        if (reloadGaugeFill != null) reloadGaugeFill.fillAmount = 1f;
    }

    public void UpdateGeneratorCount(int current, int total) { curGen = current; totalGen = total; RefreshGeneratorUI(); }
    public void RefreshGeneratorUI()
    {
        if (generatorCountText == null) return;
        if (LanguageManager.Instance != null)
        {
            string format = LanguageManager.Instance.GetText("Generator_Task");
            if (format == "Generator_Task") format = "Generators: {0} / {1}";
            generatorCountText.text = string.Format(format, curGen, totalGen);
        }
    }
    // UIManager.cs 내부

    public void ShowInteractionPrompt(bool isVisible)
    {
        if (interactionPromptObj != null)
        {
            interactionPromptObj.SetActive(isVisible);

            // 켜질 때 텍스트 내용을 갱신
            if (isVisible)
            {
                // 자식 오브젝트에 있는 TextMeshProUGUI 컴포넌트를 찾습니다.
                // (만약 직접 연결된 변수가 있다면 그걸 쓰셔도 됩니다)
                TMPro.TextMeshProUGUI promptText = interactionPromptObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();

                if (promptText != null)
                {
                    if (GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
                    {
                        // [패드] X 버튼 아이콘이나 텍스트
                        // (노란색 강조 예시)
                        promptText.text = "Hold <color=yellow>X</color> to Activate";
                    }
                    else
                    {
                        // [키보드] E 키
                        promptText.text = "Hold <color=yellow>E</color> to Activate";
                    }
                }
            }
        }
    }
    public void UpdateInteractionProgress(float ratio) { bool shouldShow = ratio > 0f && ratio < 1.0f; if (progressBarObj != null) progressBarObj.SetActive(shouldShow); if (progressBarFill != null) progressBarFill.fillAmount = ratio; }

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

    public void SetFloorIconImmediate(int floor)
    {
        if (isEnding) return;
        if (playerIcon != null && !playerIcon.gameObject.activeSelf) playerIcon.gameObject.SetActive(true);
        int anchorIndex = floor + 9;
        if (anchorIndex >= 0 && anchorIndex < floorAnchors.Length)
        {
            Vector2 targetPos = floorAnchors[anchorIndex].anchoredPosition;
            targetPos.y += yOffset;
            playerIcon.anchoredPosition = targetPos;
        }
    }

    public void AnimateFloorIcon(int targetFloor, float duration)
    {
        if (playerIcon != null && !playerIcon.gameObject.activeSelf) playerIcon.gameObject.SetActive(true);
        int anchorIndex = targetFloor + 9;
        if (anchorIndex >= 0 && anchorIndex < floorAnchors.Length)
        {
            if (iconMoveCoroutine != null) StopCoroutine(iconMoveCoroutine);
            Vector2 targetPos = floorAnchors[anchorIndex].anchoredPosition;
            targetPos.y += yOffset;
            iconMoveCoroutine = StartCoroutine(MoveIconRoutine(targetPos, duration));
        }
    }

    private IEnumerator MoveIconRoutine(Vector2 targetPos, float duration)
    {
        Vector2 startPos = playerIcon.anchoredPosition;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerIcon.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
        playerIcon.anchoredPosition = targetPos;
        iconMoveCoroutine = null;
    }

    public void UpdateWeaponSlots(bool[] unlockedStates, int currentIndex, int nextUnlockIndex)
    {
        if (weaponSlotImages == null) return;
        for (int i = 0; i < weaponSlotImages.Length; i++)
        {
            if (weaponSlotImages[i] == null) continue;
            if (nextLabels != null && i < nextLabels.Length && nextLabels[i] != null)
            {
                bool isNext = (i == nextUnlockIndex);
                nextLabels[i].SetActive(isNext);
            }
            if (i == currentIndex)
            {
                if (normalWeaponSprites != null && i < normalWeaponSprites.Length) weaponSlotImages[i].sprite = normalWeaponSprites[i];
                weaponSlotImages[i].color = activeColor;
                weaponSlotImages[i].rectTransform.localScale = originalScales[i] * 1.4f;
            }
            else if (unlockedStates[i])
            {
                if (normalWeaponSprites != null && i < normalWeaponSprites.Length) weaponSlotImages[i].sprite = normalWeaponSprites[i];
                weaponSlotImages[i].color = unlockedColor;
                weaponSlotImages[i].rectTransform.localScale = originalScales[i];
            }
            else
            {
                if (lockedWeaponSprites != null && i < lockedWeaponSprites.Length) weaponSlotImages[i].sprite = lockedWeaponSprites[i];
                weaponSlotImages[i].color = Color.white;
                weaponSlotImages[i].rectTransform.localScale = originalScales[i];
            }
        }
    }

    public void SetEndingUIState()
    {
        isEnding = true;
        if (floorPanel != null) floorPanel.SetActive(false);
        upgradePanel.SetActive(false);
        if (weaponSlotPanel != null) weaponSlotPanel.SetActive(false);
        if (playerIcon != null) playerIcon.gameObject.SetActive(false);
        bioSampleImg.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(false);
        if (ammoText != null) ammoText.gameObject.SetActive(false);
        if (bioSampleText != null) bioSampleText.gameObject.SetActive(false);
        if (generatorCountText != null) generatorCountText.gameObject.SetActive(false);
        if (HealthSystem.Instance != null) HealthSystem.Instance.gameObject.SetActive(false);
        if (reloadGaugeGroup != null) reloadGaugeGroup.SetActive(false);
    }

    public void SetWeaponUIVisible(bool isVisible)
    {
        if (weaponSlotPanel != null) weaponSlotPanel.SetActive(isVisible);
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(isVisible);
        if (ammoText != null) ammoText.gameObject.SetActive(isVisible);
    }

    public void ResetGameUI()
    {
        isEnding = false;
        if (floorPanel != null) floorPanel.SetActive(true);
        if (upgradePanel != null) upgradePanel.SetActive(false);
        bool showWeaponUI = true;
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.currentFloor == -9 && !GameManager.Instance.isRetry) showWeaponUI = false;
        }
        SetWeaponUIVisible(showWeaponUI);
        if (playerIcon != null) playerIcon.gameObject.SetActive(true);
        if (bioSampleImg != null) bioSampleImg.gameObject.SetActive(true);
        if (bioSampleText != null) bioSampleText.gameObject.SetActive(true);
        ShowGeneratorUI(false);
        if (HealthSystem.Instance != null) HealthSystem.Instance.gameObject.SetActive(true);
        if (globalFadeCanvas != null) { globalFadeCanvas.alpha = 0f; globalFadeCanvas.blocksRaycasts = false; }
    }
}