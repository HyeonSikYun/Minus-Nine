using System.Collections;
using UnityEngine;
using FIMSpace.Generating;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("PGG 설정")]
    public BuildPlannerExecutor buildPlanner;

    [Header("몬스터 스포너 설정")]
    public AutoRoomSpawnerSetup autoSpawnerSetup;

    [Header("NavMesh 설정")]
    public RuntimeNavMeshBaker navMeshBaker;

    [Header("엘리베이터 설정")]
    public GameObject finishRoomElevatorPrefab;
    private GameObject currentFinishElevator;

    [Header("콜라이더 교체 옵션")]
    [Tooltip("MeshCollider를 BoxCollider로 교체하여 NavMesh 문제 해결")]
    public bool replaceMeshColliders = true;

    [Tooltip("이 단어가 이름에 포함된 오브젝트는 MeshCollider를 유지합니다.")]
    public string[] keepMeshColliderKeywords = new string[] { "Corner", "Stairs", "Door" };

    [Header("게임 상태")]
    public bool isMapGenerated = false;

    // [추가] 층수 관리 (지하 8층 시작)
    public int currentFloor = -8;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (autoSpawnerSetup == null) autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        if (navMeshBaker == null) navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
    }

    private void Start()
    {
        // [추가] 게임 시작 시 UI 초기화
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateFloor(currentFloor);
        }

        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log($"=== 게임 초기화 시작 (현재 층: {currentFloor}) ===");

        // 1. PGG 맵 생성
        yield return StartCoroutine(GenerateMap());

        // 2. FinishRoom에 엘리베이터 배치
        PlaceFinishRoomElevator();

        // 3. NavMesh 베이크
        if (navMeshBaker != null)
        {
            Debug.Log("NavMesh 베이킹 중...");
            navMeshBaker.BakeNavMesh();
            // 베이킹 안정성을 위해 약간 대기
            yield return new WaitForSeconds(navMeshBaker.bakeDelay + 0.5f);
            Debug.Log("NavMesh 베이킹 완료!");
        }

        // 4. 몬스터 스폰
        if (isMapGenerated && autoSpawnerSetup != null)
        {
            autoSpawnerSetup.SetupSpawners();
        }

        Debug.Log("=== 스테이지 준비 완료 ===");
    }

    private IEnumerator GenerateMap()
    {
        if (buildPlanner == null)
        {
            Debug.LogError("BuildPlanner가 할당되지 않았습니다!");
            yield break;
        }

        Debug.Log("맵 오브젝트 생성 중...");
        buildPlanner.Generate();

        // 생성 완료 대기 (PGG 내부 로직에 따라 시간 조절 필요할 수 있음)
        yield return new WaitForSeconds(1f);

        // NavMesh 충돌체 문제 해결
        if (replaceMeshColliders)
        {
            ReplaceProblematicColliders();
        }

        isMapGenerated = true;
        Debug.Log("맵 생성 로직 종료");
    }

    private void PlaceFinishRoomElevator()
    {
        if (finishRoomElevatorPrefab == null)
        {
            Debug.LogWarning("FinishRoom 엘리베이터 프리팹이 없습니다!");
            return;
        }

        // FinishRoom 찾기
        GameObject finishRoom = GameObject.Find("FinishRoom");

        // 못 찾았을 경우 대비 (태그로 찾기 시도)
        if (finishRoom == null)
        {
            GameObject[] finishes = GameObject.FindGameObjectsWithTag("Finish");
            if (finishes.Length > 0) finishRoom = finishes[0];
        }

        if (finishRoom == null)
        {
            Debug.LogError("FinishRoom을 찾을 수 없습니다! 방 이름이나 태그를 확인하세요.");
            return;
        }

        // 스폰 위치 결정
        Transform spawnPoint = finishRoom.transform.Find("ElevatorSpawnPoint");
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : finishRoom.transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : finishRoom.transform.rotation;

        // 엘리베이터 생성
        currentFinishElevator = Instantiate(finishRoomElevatorPrefab, spawnPosition, spawnRotation);
        currentFinishElevator.name = "FinishRoomElevator";

        // ElevatorManager 설정
        ElevatorManager elevatorManager = currentFinishElevator.GetComponent<ElevatorManager>();
        if (elevatorManager != null)
        {
            elevatorManager.SetType(ElevatorManager.ElevatorType.Finish);
            Debug.Log("FinishRoom 엘리베이터 설정 완료 (Type: Finish)");
        }
        else
        {
            Debug.LogError("엘리베이터 프리팹에 ElevatorManager 스크립트가 없습니다!");
        }
    }

    // RestArea 엘리베이터가 호출하는 함수
    public void RegenerateMap()
    {
        StartCoroutine(RegenerateSequence());
    }

    private IEnumerator RegenerateSequence()
    {
        Debug.Log("=== 맵 재생성 프로세스 시작 ===");

        // [추가] 다음 층으로 이동 로직
        currentFloor++;

        // 0층을 건너뛰고 싶다면 아래 코드 사용 (B1 -> 1F)
        if (currentFloor == 0) currentFloor = 1;

        // [추가] UI 갱신
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateFloor(currentFloor);
        }

        isMapGenerated = false;

        // 1. 기존 Finish 엘리베이터 제거
        if (currentFinishElevator != null)
        {
            Destroy(currentFinishElevator);
        }

        // 2. 몬스터 제거
        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            spawner.ClearAllMonsters();
            Destroy(spawner.gameObject);
        }

        // 3. NavMesh 초기화
        if (navMeshBaker != null)
        {
            navMeshBaker.ClearNavMesh();
        }

        // 4. 기존 맵 오브젝트 제거
        if (buildPlanner != null)
        {
            buildPlanner.ClearGenerated();
        }

        // 청소를 위해 잠시 대기
        yield return null;

        // 5. 새 게임 초기화 (맵 생성 -> 엘리베이터 배치 -> 네비 -> 몬스터)
        yield return StartCoroutine(InitializeGame());
    }

    // StartRoom의 플레이어 스폰 위치 반환
    public Transform GetStartRoomSpawnPoint()
    {
        // 1. 이름으로 찾기
        GameObject startRoom = GameObject.Find("StartRoom");

        // 2. 태그로 찾기
        if (startRoom == null)
        {
            GameObject tagObj = GameObject.FindGameObjectWithTag("Respawn");
            if (tagObj != null) return tagObj.transform;
        }

        if (startRoom != null)
        {
            Transform spawnPoint = startRoom.transform.Find("PlayerSpawnPoint");
            return spawnPoint != null ? spawnPoint : startRoom.transform;
        }

        Debug.LogError("StartRoom을 찾을 수 없습니다!");
        return null;
    }

    // MeshCollider를 BoxCollider로 교체 (NavMesh 베이킹 오류 및 최적화)
    private void ReplaceProblematicColliders()
    {
        Debug.Log("문제가 있는 MeshCollider 교체 시작...");

        MeshCollider[] meshColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        int replacedCount = 0;
        int skippedCount = 0;

        foreach (MeshCollider mc in meshColliders)
        {
            // 0. 예외 처리: 이름에 특정 키워드(Corner 등)가 있으면 건너뜀
            bool shouldSkip = false;
            foreach (string keyword in keepMeshColliderKeywords)
            {
                if (mc.gameObject.name.Contains(keyword))
                {
                    shouldSkip = true;
                    break;
                }
            }

            if (shouldSkip)
            {
                skippedCount++;
                continue; // 교체하지 않고 다음 루프로 넘어감
            }

            // 1. Read/Write가 안 되는 메쉬만 교체 대상
            if (mc.sharedMesh != null && !mc.sharedMesh.isReadable)
            {
                GameObject obj = mc.gameObject;
                MeshFilter mf = obj.GetComponent<MeshFilter>();

                // MeshFilter가 있어야 정확한 로컬 크기를 알 수 있음
                if (mf != null && mf.sharedMesh != null)
                {
                    bool isTrigger = mc.isTrigger;
                    PhysicsMaterial physicMaterial = mc.sharedMaterial;
                    Bounds localBounds = mf.sharedMesh.bounds;

                    DestroyImmediate(mc);

                    BoxCollider bc = obj.AddComponent<BoxCollider>();

                    // 로컬 바운드 기준으로 크기 설정 (회전 문제 해결)
                    bc.center = localBounds.center;
                    bc.size = localBounds.size;

                    bc.isTrigger = isTrigger;
                    bc.material = physicMaterial;

                    replacedCount++;
                }
                else
                {
                    // MeshFilter 없는 경우 (기존 방식 - 월드 기준)
                    Bounds worldBounds = mc.bounds;
                    bool isTrigger = mc.isTrigger;
                    PhysicsMaterial mat = mc.sharedMaterial;

                    DestroyImmediate(mc);

                    BoxCollider bc = obj.AddComponent<BoxCollider>();
                    bc.center = obj.transform.InverseTransformPoint(worldBounds.center);
                    bc.size = obj.transform.InverseTransformVector(worldBounds.size);
                    bc.isTrigger = isTrigger;
                    bc.material = mat;

                    replacedCount++;
                }
            }
        }

        Debug.Log($"<color=cyan>콜라이더 최적화 결과 - 교체됨: {replacedCount}개, 유지됨(Corner등): {skippedCount}개</color>");
    }
}