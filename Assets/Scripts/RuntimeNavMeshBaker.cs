using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Header("NavMesh 설정")]
    public NavMeshSurface navMeshSurface;

    [Tooltip("네비메쉬를 구울 대상 레이어 (예: Map, Level)")]
    public LayerMask targetLayer; // ★ 추가된 부분: 원하는 레이어만 선택

    [Header("베이크 타이밍")]
    public float bakeDelay = 2f;

    // 비동기 연산을 위한 데이터
    private NavMeshData navMeshData;
    private AsyncOperation navMeshOperation;

    private void Start()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }

        ConfigureNavMeshSurface();
    }

    private void ConfigureNavMeshSurface()
    {
        if (navMeshSurface == null) return;

        // Physics Colliders 모드 사용
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // 모든 오브젝트를 검사하되, LayerMask로 필터링합니다.
        navMeshSurface.collectObjects = CollectObjects.All;

        // ★ 중요: 인스펙터에서 설정한 레이어만 굽도록 설정
        navMeshSurface.layerMask = targetLayer;
    }

    public void BakeNavMesh()
    {
        StartCoroutine(WaitAndBake());
    }

    private IEnumerator WaitAndBake()
    {
        yield return new WaitForSeconds(bakeDelay);
        yield return StartCoroutine(BakeNavMeshAsync());
    }

    private IEnumerator BakeNavMeshAsync()
    {
        if (navMeshSurface == null) yield break;

        if (navMeshOperation != null && !navMeshOperation.isDone)
        {
            Debug.LogWarning("이미 NavMesh 베이킹이 진행 중입니다.");
            yield break;
        }

        Debug.Log("NavMesh 비동기 베이킹 시작 (지정된 레이어만)...");

        if (navMeshData == null)
        {
            navMeshData = new NavMeshData();
            navMeshSurface.navMeshData = navMeshData;
            NavMesh.AddNavMeshData(navMeshData);
        }

        NavMeshBuildSettings settings = navMeshSurface.GetBuildSettings();
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();

        // 영역 설정 (전체 맵 커버)
        Bounds bounds = navMeshSurface.GetComponent<Collider>() != null
            ? navMeshSurface.GetComponent<Collider>().bounds
            : new Bounds(transform.position, new Vector3(5000, 1000, 5000));

        // ★ 핵심: targetLayer에 포함된 오브젝트만 소스로 수집
        NavMeshBuilder.CollectSources(
            bounds,
            targetLayer, // 여기서 레이어 필터링이 적용됨
            navMeshSurface.useGeometry,
            navMeshSurface.defaultArea,
            new List<NavMeshBuildMarkup>(),
            sources
        );

        navMeshOperation = NavMeshBuilder.UpdateNavMeshDataAsync(
            navMeshData,
            settings,
            sources,
            bounds
        );

        while (!navMeshOperation.isDone)
        {
            yield return null;
        }

        Debug.Log("<color=green>NavMesh 비동기 베이킹 완료!</color>");
    }

    public void ClearNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
            navMeshData = null;
        }
        navMeshOperation = null;
        Debug.Log("NavMesh 데이터 제거 완료");
    }
}