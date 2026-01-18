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

    [Header("게임 상태")]
    public bool isMapGenerated = false;

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

        // AutoRoomSpawnerSetup 자동 할당
        if (autoSpawnerSetup == null)
        {
            autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        }

        // NavMeshBaker 자동 할당
        if (navMeshBaker == null)
        {
            navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log("게임 초기화 시작...");

        // PGG 맵 생성
        yield return StartCoroutine(GenerateMap());

        // FinishRoom에 엘리베이터 배치
        PlaceFinishRoomElevator();

        // NavMesh 베이크 (몬스터 스폰 전에 완료)
        if (navMeshBaker != null)
        {
            Debug.Log("NavMesh 베이킹 대기 중...");
            navMeshBaker.BakeNavMesh();
            yield return new WaitForSeconds(navMeshBaker.bakeDelay + 0.5f);
            Debug.Log("NavMesh 베이킹 완료!");
        }

        // NavMesh 베이크 완료 후 몬스터 스폰
        if (isMapGenerated && autoSpawnerSetup != null)
        {
            autoSpawnerSetup.SetupSpawners();
        }

        Debug.Log("게임 초기화 완료!");
    }

    private IEnumerator GenerateMap()
    {
        if (buildPlanner == null)
        {
            Debug.LogError("BuildPlanner가 할당되지 않았습니다!");
            yield break;
        }

        Debug.Log("맵 생성 중...");

        // BuildPlanner의 Generate Objects 실행
        buildPlanner.Generate();

        // 생성 완료까지 대기
        yield return new WaitForSeconds(1f);

        // NavMesh 베이킹 문제 해결: MeshCollider → BoxCollider 교체
        if (replaceMeshColliders)
        {
            ReplaceProblematicColliders();
        }

        isMapGenerated = true;
        Debug.Log("맵 생성 완료!");
    }

    private void PlaceFinishRoomElevator()
    {
        if (finishRoomElevatorPrefab == null)
        {
            Debug.LogWarning("FinishRoom 엘리베이터 프리팹이 할당되지 않았습니다!");
            return;
        }

        // FinishRoom 찾기
        GameObject finishRoom = GameObject.Find("FinishRoom");
        if (finishRoom == null)
        {
            Debug.LogError("FinishRoom을 찾을 수 없습니다!");
            return;
        }

        // ElevatorSpawnPoint 찾기 (없으면 FinishRoom 중앙)
        Transform spawnPoint = finishRoom.transform.Find("ElevatorSpawnPoint");
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : finishRoom.transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : finishRoom.transform.rotation;

        // 엘리베이터 생성
        currentFinishElevator = Instantiate(finishRoomElevatorPrefab, spawnPosition, spawnRotation);
        currentFinishElevator.name = "FinishRoomElevator";
        currentFinishElevator.transform.SetParent(finishRoom.transform);

        // ElevatorManager 설정
        ElevatorManager elevatorManager = currentFinishElevator.GetComponent<ElevatorManager>();
        if (elevatorManager != null)
        {
            elevatorManager.SetAsFinishRoomElevator();
            Debug.Log("FinishRoom에 엘리베이터 배치 완료");
        }
        else
        {
            Debug.LogError("엘리베이터 프리팹에 ElevatorManager가 없습니다!");
        }
    }

    public void RegenerateMap()
    {
        Debug.Log("맵 재생성 시작");

        isMapGenerated = false;

        // 기존 FinishRoom 엘리베이터 제거
        if (currentFinishElevator != null)
        {
            Destroy(currentFinishElevator);
        }

        // NavMesh 제거
        if (navMeshBaker != null)
        {
            navMeshBaker.ClearNavMesh();
        }

        // 기존 맵 제거
        if (buildPlanner != null)
        {
            buildPlanner.ClearGenerated();
        }

        // 모든 활성 몬스터 제거
        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            spawner.ClearAllMonsters();
            Destroy(spawner.gameObject);
        }

        // 새 맵 생성
        StartCoroutine(InitializeGame());
    }

    // StartRoom의 플레이어 스폰 위치 반환
    public Transform GetStartRoomSpawnPoint()
    {
        GameObject startRoom = GameObject.Find("StartRoom");
        if (startRoom != null)
        {
            Transform spawnPoint = startRoom.transform.Find("PlayerSpawnPoint");
            if (spawnPoint != null)
            {
                return spawnPoint;
            }

            // 스폰 포인트가 없으면 StartRoom 자체 반환
            Debug.LogWarning("PlayerSpawnPoint를 찾을 수 없어 StartRoom 위치를 사용합니다.");
            return startRoom.transform;
        }

        Debug.LogError("StartRoom을 찾을 수 없습니다!");
        return null;
    }

    // MeshCollider를 BoxCollider로 교체 (NavMesh Read/Write 문제 해결)
    private void ReplaceProblematicColliders()
    {
        Debug.Log("문제가 있는 MeshCollider 교체 시작...");

        MeshCollider[] meshColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        int replacedCount = 0;

        foreach (MeshCollider mc in meshColliders)
        {
            // Read/Write가 안 되는 메쉬만 교체
            if (mc.sharedMesh != null && !mc.sharedMesh.isReadable)
            {
                GameObject obj = mc.gameObject;

                // 원래 설정 저장
                bool isTrigger = mc.isTrigger;
                PhysicsMaterial physicMaterial = mc.sharedMaterial;
                Bounds bounds = mc.bounds;

                // MeshCollider 제거
                DestroyImmediate(mc);

                // BoxCollider 추가
                BoxCollider bc = obj.AddComponent<BoxCollider>();
                bc.center = obj.transform.InverseTransformPoint(bounds.center);
                bc.size = bounds.size;
                bc.isTrigger = isTrigger;
                bc.material = physicMaterial;

                replacedCount++;
            }
        }

        Debug.Log($"<color=cyan>MeshCollider 교체 완료: {replacedCount}개</color>");
    }
}