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

    [Header("흔들림 효과 (이동 연출)")]
    [SerializeField] private float shakeIntensity = 0.05f; // 흔들리는 세기 (0.02 ~ 0.1 추천)
    [SerializeField] private float shakeSpeed = 1.0f;      // (사용 안 함, 랜덤 진동 사용)

    [Header("트리거")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    [Header("심리스 연출 설정")]
    public LayerMask hideLayerMask;

    private int originalCullingMask;
    private Camera mainCam;
    private bool isViewLocked = false;

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
        mainCam = Camera.main;
        if (mainCam != null)
        {
            originalCullingMask = mainCam.cullingMask;
        }

        if (currentType != ElevatorType.RestArea)
        {
            Initialize();
        }
    }

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

        if (currentType == ElevatorType.RestArea)
        {
            bool shouldHideMap = (GameManager.Instance.currentFloor != -9) || GameManager.Instance.isRetry;

            if (shouldHideMap)
            {
                CloseDoorsImmediate();
                LockDoor();

                if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.1f)
                {
                    StartCoroutine(FadeIn());
                }

                if (mainCam != null)
                {
                    isViewLocked = true;
                    mainCam.cullingMask &= ~hideLayerMask;
                }

                StartCoroutine(RestAreaAutoOpenSequence());
            }
            else
            {
                CloseDoorsImmediate();
                if (mainCam != null)
                {
                    isViewLocked = false;
                    mainCam.cullingMask = -1;
                }
            }
        }
        else if (currentType == ElevatorType.Finish)
        {
            FindDestination("RestAreaSpawnPoint");
            CloseDoorsImmediate();
            LockDoor();

            if (mainCam != null)
            {
                isViewLocked = false;
                mainCam.cullingMask = -1;
            }
        }

        SetupTriggers();
    }

    // ====================================================
    // [수정됨] 휴식방 10초 대기 + 흔들림 연출 시퀀스
    // ====================================================
    IEnumerator RestAreaAutoOpenSequence()
    {
        isProcessing = true;
        UpdateLightColor(true);

        // 1. 소리 재생 (웅~ 하는 엘리베이터 이동음)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.elevatorAmbience);
        }

        // 2. [핵심] 흔들림 효과 시작
        Vector3 originalPosition = transform.position; // 흔들리기 전 원래 위치 저장
        float timer = 0f;

        // "restAreaWaitTime" 동안 루프를 돌면서 매 프레임 위치를 랜덤하게 흔듭니다.
        while (timer < restAreaWaitTime)
        {
            timer += Time.deltaTime;

            // 랜덤한 방향(Sphere) * 세기(Intensity)만큼 원래 위치에서 벗어나게 함
            // 이러면 덜덜덜 거리는 효과가 납니다.
            transform.position = originalPosition + (Random.insideUnitSphere * shakeIntensity);

            yield return null; // 한 프레임 대기
        }

        // 3. 흔들림 종료: 위치 원상복구 (중요! 안 하면 문 위치가 틀어짐)
        transform.position = originalPosition;

        // 4. 대기 끝! 맵 보여주기
        isViewLocked = false;
        if (mainCam != null)
        {
            mainCam.cullingMask = -1;
        }

        Debug.Log("[RestArea] 이동 완료! 문을 엽니다.");

        if (SoundManager.Instance != null)
        {
            // 도착했으니 메인 BGM으로 변경 (또는 띵~ 소리 후 변경)
            SoundManager.Instance.PlayBGM(SoundManager.Instance.mainBgm);
        }

        UnlockDoor();
        isProcessing = false;
    }

    // ... (이하 나머지 함수들은 수정 없음, 그대로 유지) ...

    // (편의를 위해 아래 내용도 그대로 두셔도 되고, 기존 파일에서 안 건드렸다면 복사 안 해도 됩니다)
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