using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("단계별 텍스트 (Key)")]
    public string msgMove = "TUTORIAL_MOVE";
    public string msgGetGun = "TUTORIAL_GunPickup";
    public string msgCombat = "TUTORIAL_GunShoot";
    public string msgLoot = "TUTORIAL_Sample";
    public string msgUpgrade = "TUTORIAL_Tap";
    public string msgEscape = "TUTORIAL_FinUpgrade";
    public string msgGenerator = "TUTORIAL_Generator";
    public string msgFinalGoal = "TUTORIAL_Fin";

    [Header("오브젝트 연결")]
    public GameObject gunItem;         // 바닥에 떨어진 총 아이템
    public GameObject zombieGroup;     // 좀비 그룹
    public TutorialDoor exitDoor;      // 탈출구 문

    // 현재 진행 단계 (0:이동 -> 1:총발견 -> 2:전투 -> 3:파밍 -> 4:강화 -> 5:탈출)
    private int currentStep = 0;
    private int zombiesKilled = 0;
    private int totalZombies = 2; // 좀비 개수

    private string currentMessageKey = "";

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // ▼▼▼ [핵심 수정] 재시작(Retry) 상태라면 튜토리얼 매니저를 강제로 끕니다! ▼▼▼
        if (GameManager.Instance != null)
        {
            // 재시작 중이거나, 현재 층이 튜토리얼 층(-9)이 아니라면
            if (GameManager.Instance.isRetry || GameManager.Instance.currentFloor != -9)
            {
                // 1. UI 텍스트 끄기 (혹시 켜져있을까봐 확실하게)
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.HideTutorialText();
                }

                // 2. 이 스크립트 끄기 (더 이상 작동 안 하게)
                this.enabled = false;
                return; // Start 함수 종료
            }
        }
        // --- 기존 튜토리얼 로직 (아래는 원래 코드 그대로) ---
        currentStep = 0;

        if (zombieGroup != null) zombieGroup.SetActive(false);
        if (gunItem != null) gunItem.SetActive(false);

        UpdateText(msgMove);
    }

    // --- 이벤트 함수들 ---

    // [Step 1] 복도 진입 시 (Trigger가 호출)
    public void OnPlayerEnterCorridor()
    {
        // 이미 총을 먹었거나(2단계 이상) 진행 중이면 무시함 -> 메시지 안 꼬임!
        if (currentStep == 0)
        {
            currentStep = 1;
            UpdateText(msgGetGun);

            // ★ 여기서 총 아이템을 보이게 켭니다!
            if (gunItem != null) gunItem.SetActive(true);
        }
    }

    // [Step 2] 총을 먹었을 때 (GunPickup이 호출)
    public void OnGunPickedUp()
    {
        // 복도를 안 지나고 총을 먹는 버그가 있어도 강제로 2단계로 점프
        if (currentStep < 2)
        {
            currentStep = 2;
            UpdateText(msgCombat);

            // 좀비 등장!
            if (zombieGroup != null) zombieGroup.SetActive(true);
        }
    }

    // [Step 3] 좀비 처치 (ZombieAI가 호출)
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

    // [Step 4] 캡슐 2개 획득 (BioSample이 호출해야 함 - GameManager가 체크)
    // GameManager의 Update나 캡슐 획득 함수에서 체크해서 호출해주세요.
    public void CheckCapsuleCount(int currentCount)
    {
        if (currentStep == 3 && currentCount >= 2)
        {
            currentStep = 4;
            UpdateText(msgUpgrade);
        }
    }

    // [Step 5] 강화 완료 (GameManager가 호출)
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
        // 5단계(문 열림) 상태에서만 반응
        if (currentStep == 5)
        {
            currentStep = 6;
            UpdateText(msgGenerator); // "발전기를 가동하세요" 출력
        }
    }

    // [추가] 발전기가 켜졌을 때 (TutorialElevator에서 호출)
    public void OnTutorialGeneratorActivated()
    {
        if (currentStep == 6)
        {
            currentStep = 7; // 완료 상태
            // 텍스트 숨기기
            UpdateText(msgFinalGoal);
        }
    }

    // [추가] 엘리베이터 타고 올라갈 때 (최종 목표 안내)
    public void ShowFinalGoalMessage()
    {
        UpdateText(msgFinalGoal);
    }

    // 1. 텍스트 업데이트 함수 수정
    private void UpdateText(string key)
    {
        // 현재 띄우고 있는 키를 기억해둠 (나중에 언어 바꿀 때 쓰려고)
        currentMessageKey = key;

        if (UIManager.Instance != null && LanguageManager.Instance != null)
        {
            // 키를 주고 번역된 문장을 받아옴
            string localizedMsg = LanguageManager.Instance.GetText(key);
            UIManager.Instance.ShowTutorialText(localizedMsg);
        }
    }

    // 2. [추가] 언어가 바뀌었을 때, 현재 떠있는 텍스트를 새로고침하는 함수
    // (LanguageManager에서 호출함)
    public void RefreshCurrentMessage()
    {
        // 띄워놓은 메시지가 있다면, 현재 언어로 다시 번역해서 띄움
        if (!string.IsNullOrEmpty(currentMessageKey))
        {
            UpdateText(currentMessageKey);
        }
    }
}