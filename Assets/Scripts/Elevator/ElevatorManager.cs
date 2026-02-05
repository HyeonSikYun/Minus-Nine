using UnityEngine;
using System.Collections;

public class ElevatorManager : MonoBehaviour
{
    public enum ElevatorType
    {
        Normal,
        Finish,
        RestArea
    }

    [Header("엘리베이터 타입")]
    public ElevatorType currentType = ElevatorType.Normal;

    [Header("오브젝트 할당")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Transform playerTransform;

    [Header("시각 효과")]
    [SerializeField] private Light statusLight;
    [SerializeField] private Color lockedColor = Color.red;
    [SerializeField] private Color unlockedColor = Color.green;

    [Header("설정")]
    [SerializeField] private float restAreaWaitTime = 10f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;

    [Header("트리거")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    [Header("심리스 연출 설정")]
    // [중요] 여기에 'Map', 'Default', 'Wall' 등 맵을 구성하는 레이어를 모두 체크해야 합니다!
    public LayerMask hideLayerMask;

    private int originalCullingMask; // 원래 카메라가 보고 있던 레이어 목록 저장용
    private Camera mainCam;
    private bool isViewLocked = false; // 시야 차단 활성화 여부

    private Vector3 leftDoorClosedPos, leftDoorOpenPos;
    private Vector3 rightDoorClosedPos, rightDoorOpenPos;
    private bool doorsOpen = false;
    private bool isProcessing = false;
    private bool isPlayerInside = false;
    private Transform currentDestination;
    private bool isLocked = false;

    private bool isRestTimerStarted = false;

    public static ElevatorManager RestAreaInstance;

    void Awake()
    {
        if (currentType == ElevatorType.RestArea) RestAreaInstance = this;
        CalculateDoorPositions();
        FindComponents();
    }

    void Start()
    {
        // 카메라 원본 세팅 저장 (나중에 복구하기 위해)
        mainCam = Camera.main;
        if (mainCam != null)
        {
            originalCullingMask = mainCam.cullingMask;
        }

        // RestArea가 아니면 스스로 초기화
        if (currentType != ElevatorType.RestArea)
        {
            Initialize();
        }
    }

    // [핵심] 시야 잠금(isViewLocked)이 켜져 있으면, 매 프레임 강제로 맵을 숨깁니다.
    // GameManager가 실수로 맵을 켜버려도, 여기서 다시 꺼버립니다.
    void Update()
    {
        if (isViewLocked && mainCam != null)
        {
            mainCam.cullingMask &= ~hideLayerMask;
        }
    }

    public void Initialize()
    {
        InitializeRoutine();
    }

    private void InitializeRoutine()
    {
        mainCam = Camera.main;
        FindComponents();

        // ---------------------------------------------------------
        // A. 레스트룸 엘리베이터 전용 로직
        // ---------------------------------------------------------
        if (currentType == ElevatorType.RestArea)
        {
            bool shouldHideMap = (GameManager.Instance.currentFloor != -9) || GameManager.Instance.isRetry;
            // 튜토리얼(-9)이 아닐 때만 실행
            if (shouldHideMap)
            {
                CloseDoorsImmediate();
                LockDoor();

                if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.1f)
                {
                    StartCoroutine(FadeIn());
                }

                // [시야 차단]
                if (mainCam != null)
                {
                    isViewLocked = true; // Update에서 강제 고정 시작
                    mainCam.cullingMask &= ~hideLayerMask; // 즉시 가리기
                }

                StartCoroutine(RestAreaAutoOpenSequence());
            }
            else
            {
                // 튜토리얼이면 맵 다 보여주기
                CloseDoorsImmediate();
                if (mainCam != null)
                {
                    isViewLocked = false; // 감시 해제
                    mainCam.cullingMask = -1; // 모든 레이어 보이기
                }
            }
        }
        // ---------------------------------------------------------
        // B. 피니쉬 엘리베이터 전용 로직
        // ---------------------------------------------------------
        else if (currentType == ElevatorType.Finish)
        {
            FindDestination("RestAreaSpawnPoint");
            CloseDoorsImmediate();
            LockDoor();

            // 피니쉬 엘리베이터는 무조건 맵이 보여야 함
            if (mainCam != null)
            {
                isViewLocked = false;
                mainCam.cullingMask = -1;
            }
        }

        SetupTriggers();
    }

    // ====================================================
    // 휴식방 10초 대기 시퀀스
    // ====================================================
    IEnumerator RestAreaAutoOpenSequence()
    {
        isProcessing = true;
        UpdateLightColor(true);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.elevatorAmbience);
        }

        // 10초 대기
        yield return new WaitForSeconds(restAreaWaitTime);

        // [핵심] 대기 끝! 이제 맵을 보여줍니다.
        isViewLocked = false; // Update 감시 해제
        if (mainCam != null)
        {
            mainCam.cullingMask = -1; // 맵 짠! 하고 보여주기
        }

        Debug.Log("[RestArea] 대기 완료! 문을 엽니다.");

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.mainBgm);
        }
        UnlockDoor();
        isProcessing = false;
    }

    // ====================================================
    // 나머지 함수들 (기존 유지)
    // ====================================================
    void SetupTriggers()
    {
        if (doorTriggerObject)
        {
            var dt = GetOrAddTrigger(doorTriggerObject);
            dt.onPlayerEnter = () => {
                if (currentType != ElevatorType.RestArea && !isProcessing && !doorsOpen && !isLocked)
                    StartCoroutine(OpenDoors());
            };
            dt.onPlayerExit = () => {
                if (!isProcessing && doorsOpen && !isPlayerInside) StartCoroutine(CloseDoors());
            };
        }

        if (insideTriggerObject)
        {
            var it = GetOrAddTrigger(insideTriggerObject);
            System.Action onPlayerDetected = () =>
            {
                isPlayerInside = true;
                if (!isProcessing && doorsOpen && currentType != ElevatorType.RestArea)
                {
                    StartCoroutine(DepartSequence());
                }
            };
            it.onPlayerEnter = onPlayerDetected;
            it.onPlayerStay = onPlayerDetected;
            it.onPlayerExit = () => {
                isPlayerInside = false;
                if (!isProcessing && currentType == ElevatorType.RestArea && doorsOpen)
                {
                    StartCoroutine(CloseDoors());
                }
            };
        }
    }

    private void CloseDoorsImmediate()
    {
        doorsOpen = false;
        if (leftDoor) leftDoor.localPosition = leftDoorClosedPos;
        if (rightDoor) rightDoor.localPosition = rightDoorClosedPos;
    }

    public void LockDoor() { isLocked = true; UpdateLightColor(true); if (doorsOpen) StartCoroutine(CloseDoors()); }
    public void UnlockDoor() { isLocked = false; UpdateLightColor(false); StartCoroutine(OpenDoors()); }
    private void UpdateLightColor(bool locked) { if (statusLight != null) statusLight.color = locked ? lockedColor : unlockedColor; }

    IEnumerator DepartSequence()
    {
        isProcessing = true;
        yield return StartCoroutine(CloseDoors());
        yield return StartCoroutine(FadeOut());
        SoundManager.Instance.StopBGM();
        if (currentDestination) TeleportPlayer(currentDestination);

        if (currentType == ElevatorType.Finish)
        {
            if (GameManager.Instance) GameManager.Instance.LoadNextLevel();
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(FadeIn());
            yield return StartCoroutine(OpenDoors());
            isProcessing = false;
        }
    }

    IEnumerator ExitRestAreaSequence()
    {
        isProcessing = true;
        FindNewStartPoint();
        if (currentDestination) TeleportPlayer(currentDestination);
        StartCoroutine(CloseDoors());
        isRestTimerStarted = false;
        LockDoor();
        isProcessing = false;
        doorsOpen = false;
        yield return null;
    }

    void CalculateDoorPositions() { if (leftDoor) { leftDoorClosedPos = leftDoor.localPosition; leftDoorOpenPos = leftDoorClosedPos + new Vector3(0, 0, -0.66f); } if (rightDoor) { rightDoorClosedPos = rightDoor.localPosition; rightDoorOpenPos = rightDoorClosedPos + new Vector3(0, 0, 0.66f); } }
    void FindComponents() { if (!playerTransform) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) playerTransform = p.transform; } if (!fadeCanvasGroup) { GameObject c = GameObject.Find("FadeCanvas"); if (c) fadeCanvasGroup = c.GetComponent<CanvasGroup>(); if (!fadeCanvasGroup) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>(); } if (!statusLight) statusLight = GetComponentInChildren<Light>(); }
    void FindDestination(string name) { GameObject go = GameObject.Find(name); if (go) currentDestination = go.transform; }
    void FindNewStartPoint() { if (GameManager.Instance) { Transform sp = GameManager.Instance.GetStartRoomSpawnPoint(); if (sp) currentDestination = sp; } if (!currentDestination) FindDestination("StartPoint"); }
    ElevatorTrigger GetOrAddTrigger(GameObject obj) { var t = obj.GetComponent<ElevatorTrigger>(); if (!t) t = obj.AddComponent<ElevatorTrigger>(); return t; }
    void TeleportPlayer(Transform target) { if (!playerTransform) return; CharacterController cc = playerTransform.GetComponent<CharacterController>(); if (cc) cc.enabled = false; playerTransform.position = target.position; playerTransform.rotation = target.rotation; if (cc) cc.enabled = true; }
    IEnumerator MoveDoors(bool open) { float t = 0; Vector3 lStart = leftDoor ? leftDoor.localPosition : Vector3.zero; Vector3 rStart = rightDoor ? rightDoor.localPosition : Vector3.zero; Vector3 lEnd = open ? leftDoorOpenPos : leftDoorClosedPos; Vector3 rEnd = open ? rightDoorOpenPos : rightDoorClosedPos; while (t < 1) { t += Time.deltaTime * doorSpeed; if (leftDoor) leftDoor.localPosition = Vector3.Lerp(lStart, lEnd, t); if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rStart, rEnd, t); yield return null; } doorsOpen = open; }
    IEnumerator OpenDoors() { SoundManager.Instance.PlaySFX(SoundManager.Instance.elevatorDing); return MoveDoors(true); }
    IEnumerator CloseDoors() { return MoveDoors(false); }
    IEnumerator FadeOut() { if (!fadeCanvasGroup) yield break; fadeCanvasGroup.blocksRaycasts = true; float t = fadeCanvasGroup.alpha; while (t < 1) { t += Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 1; }
    IEnumerator FadeIn() { if (!fadeCanvasGroup) yield break; float t = fadeCanvasGroup.alpha; while (t > 0) { t -= Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 0; fadeCanvasGroup.blocksRaycasts = false; }
    public void SetType(ElevatorType type) => currentType = type;
}