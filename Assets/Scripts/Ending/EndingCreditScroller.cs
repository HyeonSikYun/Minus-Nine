using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // 씬 이동 필수
using System.Collections;
using TMPro;

public class EndingCreditScroller : MonoBehaviour
{
    public RectTransform scrollContent; // 위로 올라갈 내용물
    public float scrollSpeed = 50f;     // 속도 (천천히 올라가야 분위기 있음)
    public float targetPosY = 2000f;
    [Header("통계 표시용 텍스트")]
    public TextMeshProUGUI statsText;

    public void StartScroll()
    {
        UpdateStatsText();
        StartCoroutine(ScrollRoutine());
        if (AchiManager.Instance != null)
        {
            AchiManager.Instance.UnlockAchi(0);
            if (GameManager.Instance != null)
            {
                // playTime은 "00:45:10" 같은 형태입니다.
                string playTime = GameManager.Instance.GetPlayTimeFormatted();

                // 문자열이 "00:"으로 시작한다면 = 1시간(60분)이 안 넘었다는 뜻!
                if (playTime.StartsWith("00:"))
                {
                    // 스피드런 업적 번호가 1번이라고 가정했습니다. 맞게 수정해 주세요!
                    AchiManager.Instance.UnlockAchi(3);
                    Debug.Log($"업적 달성: 칼퇴근 (클리어 시간: {playTime})");
                }
            }
        }
            
    }

    private void UpdateStatsText()
    {
        if (statsText != null && GameManager.Instance != null)
        {
            // 예시: 
            // Total Kills: 150
            // Play Time: 00:15:30
            string playTime = GameManager.Instance.GetPlayTimeFormatted();
            int kills = GameManager.Instance.totalZombieKills;

            statsText.text = $"Total Zombies Killed : <color=red>{kills}</color>\n" +
                             $"Total Play Time : <color=red>{playTime}</color>";
        }
    }

    IEnumerator ScrollRoutine()
    {
        // 1. 목표 높이까지 스크롤 올리기
        // (현재 Y값이 목표값보다 작을 동안 계속 실행)
        while (scrollContent.anchoredPosition.y < targetPosY)
        {
            scrollContent.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
            yield return null;
        }

        // 2. 다 올라가면(혹은 로고가 멈추면) 잠시 대기 (여운)
        Debug.Log("크레딧 스크롤 종료. 3초 대기 후 재시작합니다.");
        yield return new WaitForSeconds(1.0f);

        // 3. 게임 완전 초기화 및 재시작
        RestartGame();
    }

    private void RestartGame()
    {
        Debug.Log("?? 게임 리셋 및 튜토리얼 재시작");

        // 1. 시간 정상화
        Time.timeScale = 1f;

        // 2. [중요] 살아있는 싱글톤 매니저들 강제 삭제
        // (삭제하지 않으면 재시작했을 때 예전 데이터가 남아서 꼬임)
        if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
        if (SoundManager.Instance != null) Destroy(SoundManager.Instance.gameObject);
        if (UIManager.Instance != null) Destroy(UIManager.Instance.gameObject);
        if (EndingSceneManager.Instance != null) Destroy(EndingSceneManager.Instance.gameObject);
        // 혹시 InventoryManager나 QuestManager가 있다면 여기 추가하세요.
        if (AchiManager.Instance != null) Destroy(AchiManager.Instance.gameObject);
        var steamManager = FindObjectOfType<SteamManager>();
        if (steamManager != null)
        {
            Destroy(steamManager.gameObject);
        }
        else
        {
            // 혹시 못 찾았을 경우를 대비해 이름으로도 시도
            GameObject smObj = GameObject.Find("SteamManager");
            if (smObj != null) Destroy(smObj);
        }
        // 3. 현재 씬(MainScene)을 다시 로드 -> 튜토리얼 상태로 시작됨
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}