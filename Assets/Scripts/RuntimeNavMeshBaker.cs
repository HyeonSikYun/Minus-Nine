using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Header("NavMesh 설정")]
    public NavMeshSurface navMeshSurface;

    [Header("베이크 타이밍")]
    public float bakeDelay = 2f; // 맵 생성 후 베이크 대기 시간

    private void Start()
    {
        // NavMeshSurface가 없으면 자동 추가
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }
        }

        // Read/Write 에러 해결을 위한 설정
        ConfigureNavMeshSurface();
    }

    private void ConfigureNavMeshSurface()
    {
        if (navMeshSurface == null) return;

        // Physics Colliders를 사용하여 메쉬 Read/Write 문제 우회
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // 모든 오브젝트 수집
        navMeshSurface.collectObjects = CollectObjects.All;

        Debug.Log("NavMeshSurface 설정 완료 - Physics Colliders 모드 활성화");
    }

    public void BakeNavMesh()
    {
        Invoke(nameof(DoBake), bakeDelay);
    }

    private void DoBake()
    {
        if (navMeshSurface != null)
        {
            try
            {
                Debug.Log("NavMesh 베이킹 시작...");
                navMeshSurface.BuildNavMesh();
                Debug.Log("<color=green>NavMesh 베이킹 완료!</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"NavMesh 베이킹 실패: {e.Message}");
                Debug.LogWarning("맵의 콜라이더를 확인하세요. NavMesh는 콜라이더가 있는 오브젝트만 인식합니다.");
            }
        }
        else
        {
            Debug.LogError("NavMeshSurface가 없습니다!");
        }
    }

    // 맵 재생성 시 NavMesh 제거
    public void ClearNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
            Debug.Log("NavMesh 제거 완료");
        }
    }

    // 수동 테스트용
    [ContextMenu("Test Bake NavMesh")]
    public void TestBake()
    {
        DoBake();
    }
}