using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("단계별 텍스트 (기본 키값)")]
    // 뒤에 _PC, _PAD는 떼고 입력하세요. (LanguageManager에는 _PC, _PAD 버전이 등록되어 있어야 함)
    public string msgMove = "Tuto_Move";
    public string msgGetGun = "TUTORIAL_GunPickup"; // 공용
    public string msgCombat = "Tuto_GunShoot";
    public string msgLoot = "TUTORIAL_Sample";
    public string msgUpgrade = "Tuto_Tap";
    public string msgEscape = "TUTORIAL_FinUpgrade";
    public string msgGenerator = "TUTORIAL_Generator";
    public string msgFinalGoal = "TUTORIAL_Fin";

    [Header("오브젝트 연결")]
    public GameObject gunItem;
    public GameObject zombieGroup;
    public TutorialDoor exitDoor;

    private int currentStep = 0;
    private int zombiesKilled = 0;
    private int totalZombies = 2;

    // 현재 띄우고 있는 메시지의 '기본 키값' (예: "Tuto_Move")
    private string currentMessageKey = "";

    // 입력 장치 상태가 바뀌었는지 체크하기 위한 변수
    private bool lastGamepadState = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 재시작(Retry) 상태라면 튜토리얼 끄기
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isRetry || GameManager.Instance.currentFloor != -9)
            {
                if (UIManager.Instance != null) UIManager.Instance.HideTutorialText();
                this.enabled = false; // Update도 안 돌아가게 꺼버림
                return;
            }
            // 초기 상태 동기화
            lastGamepadState = GameManager.Instance.isUsingGamepad;
        }

        currentStep = 0;

        if (zombieGroup != null) zombieGroup.SetActive(false);
        if (gunItem != null) gunItem.SetActive(false);

        // 첫 메시지 출력
        UpdateText(msgMove);
    }

    // ★ [추가됨] 매 프레임 입력 장치가 바뀌었는지 감시
    private void Update()
    {
        if (GameManager.Instance == null) return;

        // 입력 장치 상태가 이전과 달라졌다면? (키보드 <-> 패드 전환 시)
        if (GameManager.Instance.isUsingGamepad != lastGamepadState)
        {
            lastGamepadState = GameManager.Instance.isUsingGamepad;

            // 현재 메시지를 새 입력 장치에 맞춰서 새로고침!
            RefreshCurrentMessage();
        }
    }

    // --- 이벤트 함수들 (기존 그대로) ---

    public void OnPlayerEnterCorridor()
    {
        if (currentStep == 0)
        {
            currentStep = 1;
            UpdateText(msgGetGun);
            if (gunItem != null) gunItem.SetActive(true);
        }
    }

    public void OnGunPickedUp()
    {
        if (currentStep < 2)
        {
            currentStep = 2;
            UpdateText(msgCombat);
            if (zombieGroup != null) zombieGroup.SetActive(true);
        }
    }

    public void OnZombieKilled()
    {
        if (currentStep == 2)
        {
            zombiesKilled++;
            if (zombiesKilled >= totalZombies)
            {
                currentStep = 3;
                UpdateText(msgLoot);
            }
        }
    }

    public void CheckCapsuleCount(int currentCount)
    {
        if (currentStep == 3 && currentCount >= 2)
        {
            currentStep = 4;
            UpdateText(msgUpgrade);
        }
    }

    public void OnUpgradeCompleted()
    {
        if (currentStep == 4)
        {
            currentStep = 5;
            UpdateText(msgEscape);
            if (exitDoor != null) exitDoor.OpenDoor();
        }
    }

    public void OnPlayerEnterGeneratorRoom()
    {
        if (currentStep == 5)
        {
            currentStep = 6;
            UpdateText(msgGenerator);
        }
    }

    public void OnTutorialGeneratorActivated()
    {
        if (currentStep == 6)
        {
            currentStep = 7;
            UpdateText(msgFinalGoal);
        }
    }

    public void ShowFinalGoalMessage()
    {
        UpdateText(msgFinalGoal);
    }

    // =================================================================
    // ★ [핵심 수정] 여기서 PC/PAD를 구분해서 텍스트를 결정합니다.
    // =================================================================

    private void UpdateText(string key)
    {
        // 1. 현재 키 저장 (나중에 새로고침용)
        currentMessageKey = key;

        if (UIManager.Instance != null && LanguageManager.Instance != null && GameManager.Instance != null)
        {
            // 2. 현재 입력 장치 확인
            string suffix = "";

            // 패드를 쓰고 있다면 "_PAD", 아니면 "_PC"를 붙일 준비
            // (단, 공용 텍스트일 수도 있으니 체크가 필요할 수 있음)
            if (GameManager.Instance.isUsingGamepad)
                suffix = "_PAD";
            else
                suffix = "_PC";

            // 3. 최종 키 조합 (예: "Tuto_Move" + "_PC" => "Tuto_Move_PC")
            string finalKey = key + suffix;

            // 4. 번역 가져오기 시도
            string localizedMsg = LanguageManager.Instance.GetText(finalKey);

            // [안전장치] 만약 "_PC" 붙인 키가 없어서 키값 그대로 리턴됐다면?
            // (예: TUTORIAL_GunPickup 같은 공용 키는 _PC 버전이 없을 수 있음)
            if (localizedMsg == finalKey)
            {
                // 그냥 원래 키("TUTORIAL_GunPickup")로 다시 시도
                localizedMsg = LanguageManager.Instance.GetText(key);
            }

            // 5. UIManager에 '완성된 문장' 전달 (기존 함수 사용!)
            UIManager.Instance.ShowTutorialText(localizedMsg);
        }
    }

    // 언어 변경이나 패드 변경 시 호출됨
    public void RefreshCurrentMessage()
    {
        if (!string.IsNullOrEmpty(currentMessageKey))
        {
            UpdateText(currentMessageKey);
        }
    }
}