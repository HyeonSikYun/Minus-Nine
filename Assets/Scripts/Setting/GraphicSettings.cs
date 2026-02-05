using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 쓴다면 필수 (기본 Text라면 UnityEngine.UI 사용)

public class GraphicSettings : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_Dropdown resolutionDropdown; // 해상도 드롭다운
    [SerializeField] private TMP_Dropdown displayModeDropdown; // 전체화면/창모드 드롭다운

    private Resolution[] resolutions; // 지원 가능한 해상도 목록
    private List<Resolution> filteredResolutions; // 중복 제거된 해상도 목록

    private float currentRefreshRate;
    private int currentResolutionIndex = 0;

    void Start()
    {
        // 1. 디스플레이 모드 옵션을 언어에 맞춰 생성
        RefreshDisplayModeOptions();

        // 2. 해상도 초기화
        InitResolution();
    }

    // =========================================================
    // 디스플레이 모드 (전체화면 / 창모드)
    // =========================================================
    public void RefreshDisplayModeOptions()
    {
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
            // 매니저가 없을 때를 대비한 기본값
            options.Add("Fullscreen");
            options.Add("Windowed");
        }

        // 4. 드롭다운에 새 옵션 넣기
        displayModeDropdown.AddOptions(options);

        // 5. 아까 저장해둔 선택값 복구
        displayModeDropdown.value = currentMode;
        displayModeDropdown.RefreshShownValue();
    }

    public void SetDisplayMode(int index)
    {
        // index 0: 전체화면, 1: 창모드
        bool isFullscreen = (index == 0);
        Screen.fullScreen = isFullscreen;

        // 전체화면 모드 변경 (ExclusiveFullScreen이 가장 안정적)
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;

        PlayerPrefs.SetInt("DisplayMode", index);
    }

    // =========================================================
    // 해상도 (자동 감지 및 설정)
    // =========================================================
    void InitResolution()
    {
        resolutions = Screen.resolutions;
        filteredResolutions = new List<Resolution>();

        resolutionDropdown.ClearOptions();

        // [Unity 6 수정] .value는 double이므로 (float)로 강제 형변환 필수!
        currentRefreshRate = (float)Screen.currentResolution.refreshRateRatio.value;

        int currentResIndex = 0;
        List<string> options = new List<string>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            // [Unity 6 수정] 비교할 때도 (float)로 변환해서 비교
            float resolutionRate = (float)resolutions[i].refreshRateRatio.value;

            // 주사율이 같은 것만 필터링 (Mathf.Approximately는 실수 비교 오차를 줄여줌)
            if (Mathf.Approximately(resolutionRate, currentRefreshRate))
            {
                filteredResolutions.Add(resolutions[i]);
            }
        }

        // 필터링 된 게 없으면 그냥 다 넣기
        if (filteredResolutions.Count == 0) filteredResolutions.AddRange(resolutions);

        for (int i = 0; i < filteredResolutions.Count; i++)
        {
            // 드롭다운에 표시될 텍스트 (예: 1920 x 1080)
            string option = filteredResolutions[i].width + " x " + filteredResolutions[i].height;
            options.Add(option);

            if (filteredResolutions[i].width == Screen.width &&
                filteredResolutions[i].height == Screen.height)
            {
                currentResIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", currentResIndex);
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = filteredResolutions[resolutionIndex];

        // 마지막 인자는 전체화면 여부 (현재 설정 따라감)
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
    }
}