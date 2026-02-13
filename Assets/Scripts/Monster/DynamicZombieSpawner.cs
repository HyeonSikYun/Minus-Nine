using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DynamicZombieSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    // 일반 좀비 목록 (여기 있는 것들은 '일반' 취급)
    public List<string> normalZombieTags;

    // [수정] 특수 좀비 목록 (여기 있는 것들은 '특수' 확률에 당첨됐을 때 랜덤 등장)
    public List<string> specialZombieTags;

    public int targetZombieCount = 15;
    public float checkInterval = 3.0f;

    [Header("거리 설정")]
    public float minDistance = 15f;
    public float maxDistance = 30f;

    [Header("끼임 방지 설정")]
    public LayerMask obstacleLayer;
    public float zombieRadius = 0.8f;

    [Header("특수 좀비 등장 확률")]
    public float startChance = 0.1f;    // B5층 확률 (10%)
    public float increaseRate = 0.05f; // 층당 증가량 (5%)

    private Transform playerTransform;
    private Camera mainCamera;
    private bool isSpawning = false;

    public void StartSpawning()
    {
        isSpawning = true;
        mainCamera = Camera.main;
        StartCoroutine(SpawnRoutine());
        Debug.Log("<color=green>>>> 다이내믹 좀비 리스폰 시스템 가동</color>");
    }

    public void StopSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning)
        {
            yield return new WaitForSeconds(checkInterval);

            // ★ [수정] 플레이어가 파괴되었거나 없을 때 코루틴 종료 (에러 방지)
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    // 플레이어를 못 찾았다면 이번 턴은 넘기거나, 
                    // 게임이 종료되는 중이라면 루프 탈출
                    if (GameManager.Instance == null) yield break;
                    continue;
                }
            }

            ZombieAI[] activeZombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int currentCount = 0;
            foreach (var z in activeZombies)
            {
                if (!z.isDead && z.gameObject.activeInHierarchy) currentCount++;
            }

            if (currentCount < targetZombieCount)
            {
                int spawnAmount = Mathf.Min(2, targetZombieCount - currentCount);
                for (int i = 0; i < spawnAmount; i++)
                {
                    // ★ [수정] 스폰 시도 전에도 플레이어 체크
                    if (playerTransform == null) break;

                    TrySpawnZombie();
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
    }

    private void TrySpawnZombie()
    {
        // ★ [핵심 수정] 플레이어가 없거나 파괴되었으면 즉시 리턴 (에러 원인 차단)
        if (playerTransform == null) return;

        // ★ [추가] 스포너 자신도 파괴되었는지 체크
        if (this == null || gameObject == null) return;

        if (normalZombieTags.Count == 0) return;

        for (int i = 0; i < 10; i++)
        {
            // 여기서 playerTransform.position을 쓸 때 에러가 났던 것임
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minDistance, maxDistance);
            Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y) * distance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 2.0f, NavMesh.AllAreas))
            {
                Vector3 finalPos = hit.position;

                if (!IsVisibleToCamera(finalPos))
                {
                    if (!Physics.CheckSphere(finalPos, zombieRadius, obstacleLayer))
                    {
                        // 태그 가져오기
                        string selectedTag = GetZombieTagByFloor();

                        GameObject monster = PoolManager.Instance.SpawnFromPool(
                            selectedTag,
                            finalPos,
                            Quaternion.LookRotation(playerTransform.position - finalPos)
                        );

                        if (monster != null)
                        {
                            ZombieAI ai = monster.GetComponent<ZombieAI>();
                            if (ai != null) ai.Initialize(finalPos);
                        }

                        return;
                    }
                }
            }
        }
    }

    private string GetZombieTagByFloor()
    {
        int currentFloor = -9;
        if (GameManager.Instance != null)
            currentFloor = GameManager.Instance.currentFloor;

        bool spawnSpecial = false;

        // 1. 특수 좀비 리스트가 비어있지 않고, B6층 이상일 때만 확률 계산
        if (specialZombieTags.Count > 0 && currentFloor >= -6)
        {
            int levelProgress = currentFloor - (-6);
            float chance = startChance + (levelProgress * increaseRate);
            chance = Mathf.Clamp(chance, 0f, 0.5f);

            if (Random.value < chance)
            {
                spawnSpecial = true;
            }
        }

        // 2. 결과에 따라 태그 반환
        if (spawnSpecial)
        {
            // 특수 좀비 리스트 중 하나를 랜덤으로 뽑음
            int randomIndex = Random.Range(0, specialZombieTags.Count);
            return specialZombieTags[randomIndex];
        }
        else
        {
            // 일반 좀비 리스트 중 하나를 랜덤으로 뽑음
            int randomIndex = Random.Range(0, normalZombieTags.Count);
            return normalZombieTags[randomIndex];
        }
    }

    private bool IsVisibleToCamera(Vector3 position)
    {
        if (mainCamera == null) return false;
        Vector3 viewPos = mainCamera.WorldToViewportPoint(position);
        return (viewPos.x > -0.2f && viewPos.x < 1.2f &&
                viewPos.y > -0.2f && viewPos.y < 1.2f &&
                viewPos.z > 0);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (playerTransform != null)
        {
            Gizmos.DrawWireSphere(playerTransform.position, minDistance);
            Gizmos.DrawWireSphere(playerTransform.position, maxDistance);
        }
    }
}