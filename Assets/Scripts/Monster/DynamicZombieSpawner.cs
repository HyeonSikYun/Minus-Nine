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

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                else { if (GameManager.Instance == null) yield break; continue; }
            }

            ZombieAI[] activeZombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int currentCount = 0;
            foreach (var z in activeZombies)
            {
                if (!z.isDead && z.gameObject.activeInHierarchy) currentCount++;
            }

            // ========================================================
            // ★ [수정 1] 기획하신 층별 타겟 좀비 수 (F8~F7은 30, F1은 85)
            // ========================================================
            int currentFloor = GameManager.Instance != null ? GameManager.Instance.currentFloor : -8;
            int currentTargetCount = targetZombieCount; // 기본값

            switch (currentFloor)
            {
                case -9: currentTargetCount = 0; break;  // ★ 튜토리얼: 절대 스폰 안 됨! (0마리 유지)
                case -8: currentTargetCount = 20; break; // 8층
                case -7: currentTargetCount = 30; break; // 7층
                case -6: currentTargetCount = 40; break; // 6층
                case -5: currentTargetCount = 50; break; // 5층
                case -4: currentTargetCount = 60; break; // 4층
                case -3: currentTargetCount = 63; break; // 3층
                case -2: currentTargetCount = 65; break; // 2층
                case -1: currentTargetCount = 70; break; // 1층
            }
            // ========================================================
            if (currentTargetCount <= 0)
            {
                continue; // 아래 스폰 로직을 무시하고 다음 1초 뒤로 넘어감
            }
            if (currentCount < currentTargetCount)
            {
                int spawnAmount = Mathf.Min(2, currentTargetCount - currentCount);
                for (int i = 0; i < spawnAmount; i++)
                {
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

        // ========================================================
        // ★ [수정 3] 특수 좀비 등장 확률 (기하급수적 증가)
        // ========================================================
        if (specialZombieTags.Count > 0 && currentFloor >= -6)
        {
            float chance = 0f;
            switch (currentFloor)
            {
                case -6: chance = 0.01f; break; // 6층: 5% (가끔 한 마리 깜짝 등장)
                case -5: chance = 0.05f; break; // 5층: 10%
                case -4: chance = 0.10f; break; // 4층: 20%
                case -3: chance = 0.20f; break; // 3층: 25% (급증가 시작)
                case -2: chance = 0.25f; break; // 2층: 35% (절반 이상이 특수 좀비)
                case -1: chance = 0.35f; break; // 1층: 50% (거의 다 특수 좀비밭)
            }

            if (Random.value < chance)
            {
                spawnSpecial = true;
            }
        }
        // ========================================================

        if (spawnSpecial)
        {
            int randomIndex = Random.Range(0, specialZombieTags.Count);
            return specialZombieTags[randomIndex];
        }
        else
        {
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