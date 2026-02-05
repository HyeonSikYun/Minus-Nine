using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 필수
using UnityEngine.SceneManagement; // [필수] 씬 매니지먼트 추가

public class GraphicSettings : MonoBehaviour
{
    public static GraphicSettings Instance;

    [Header("UI Components")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown displayModeDropdown;

    private Resolution[] resolutions;
    private List<Resolution> filteredResolutions;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    // [핵심] 씬 로드 이벤트 등록 (재시작 감지용)
    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // [핵심] 씬이 로드될 때마다 실행 (처음 시작 + 재시작 모두 포함)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. UIManager를 통해 "새로 태어난 드롭다운"을 가져와서 연결
        if (UIManager.Instance != null)
        {
            resolutionDropdown = UIManager.Instance.resolutionDropdown;
            displayModeDropdown = UIManager.Instance.displayModeDropdown;
        }

        // 2. 드롭다운이 잘 연결됐으면 내용 채우기 (기존 Start에 있던 로직)
        if (displayModeDropdown != null && resolutionDropdown != null)
        {
            // 이벤트 잠시 해제 (초기화 중 이벤트 발동 방지)
            displayModeDropdown.onValueChanged.RemoveAllListeners();
            resolutionDropdown.onValueChanged.RemoveAllListeners();

            // 내용 채우기
            RefreshDisplayModeOptions();
            InitResolution();

            // 이벤트 다시 연결
            displayModeDropdown.onValueChanged.AddListener(SetDisplayMode);
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }
    }

    // Start 함수는 비워두거나 제거 (OnSceneLoaded가 대신함)
    void Start()
    {
        // OnSceneLoaded가 알아서 다 합니다.
    }

    // =========================================================
    // 디스플레이 모드 (전체화면 / 창모드)
    // =========================================================
    public void RefreshDisplayModeOptions()
    {
        if (displayModeDropdown == null) return;

        // 1. 현재 선택된 모드 번호 저장 (0: 전체, 1: 창)
        int currentMode = PlayerPrefs.GetInt("DisplayMode", 0);

        // 2. 기존 옵션 싹 지우기
        displayModeDropdown.ClearOptions();

        // 3. 언어 매니저에서 단어 가져와서 리스트 만들기
        List<string> options = new List<string>();

        if (LanguageManager.Instance != null)
        {
            options.Add(LanguageManager.Instance.GetText("Opt_DisplayFull"));   // 전체화면
            options.Add(LanguageManager.Instance.GetText("Opt_DisplayWindow")); // 창모드
        }
        else
        {
            options.Add("Fullscreen");
            options.Add("Windowed");
        }

        // 4. 드롭다운에 새 옵션 넣기
        displayModeDropdown.AddOptions(options);

        // 5. 아까 저장해둔 선택값 복구 (이벤트 없이)
        displayModeDropdown.SetValueWithoutNotify(currentMode);
        displayModeDropdown.RefreshShownValue();
    }

    public void SetDisplayMode(int index)
    {
        // index 0: 전체화면, 1: 창모드
        bool isFullscreen = (index == 0);
        Screen.fullScreen = isFullscreen;

        Screen.fullScreenMode = isFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        PlayerPrefs.SetInt("DisplayMode", index);
    }

    // =========================================================
    // 해상도 (자동 감지 및 설정)
    // =========================================================
    void InitResolution()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;

        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResIndex = 0;

        HashSet<string> addedResolutions = new HashSet<string>();
        filteredResolutions = new List<Resolution>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            // 1. 해상도 문자열 생성 (예: "1920 x 1080")
            string option = resolutions[i].width + " x " + resolutions[i].height;

            // 2. 이미 등록된 해상도라면 건너뜀
            if (addedResolutions.Contains(option)) continue;

            // 3. 목록에 추가
            addedResolutions.Add(option);
            options.Add(option);
            filteredResolutions.Add(resolutions[i]);

            // 4. 현재 내 화면 크기와 같다면 인덱스 저장
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
            {
                currentResIndex = filteredResolutions.Count - 1;
            }
        }

        // 드롭다운에 옵션 추가 및 현재값 선택
        resolutionDropdown.AddOptions(options);

        // 저장된 값 불러오기 (없으면 현재 해상도)
        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", currentResIndex);

        // 안전 장치: 인덱스가 범위 벗어나면 0으로
        if (savedIndex >= filteredResolutions.Count) savedIndex = 0;

        resolutionDropdown.SetValueWithoutNotify(savedIndex);
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resolutionIndex)
    {
        if (filteredResolutions == null || resolutionIndex >= filteredResolutions.Count) return;

        Resolution resolution = filteredResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
    }

    // 언어 바뀔 때 UI 갱신용
    public void RefreshUI()
    {
        RefreshDisplayModeOptions();
    }
}