using UnityEngine;

public class BioSample : MonoBehaviour
{
    public int amount = 1;
    public float rotateSpeed = 100f;
    public float pickupRange = 1.5f;

    private Transform playerTransform;

    // [최적화 1] 100개의 샘플이 매번 Find를 하지 않도록 static으로 캐싱 (공유)
    private static Transform cachedPlayer;

    // [최적화 2] 풀링을 위해 Start가 아닌 OnEnable 사용
    private void OnEnable()
    {
        // 유니티의 가비지 콜렉팅이나 씬 재시작으로 플레이어가 파괴되었는지 확실히 체크
        if (cachedPlayer == null || !cachedPlayer.gameObject.activeInHierarchy)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                cachedPlayer = player.transform;
            }
        }
        playerTransform = cachedPlayer;
    }

    private void Update()
    {
        // 1. 회전 연출
        transform.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime);

        // 2. 거리 체크 로직
        if (playerTransform != null)
        {
            // [최적화 3] 무거운 Vector3.Distance 대신 아주 가벼운 sqrMagnitude 사용
            float sqrDistance = (transform.position - playerTransform.position).sqrMagnitude;
            float sqrPickupRange = pickupRange * pickupRange;

            if (sqrDistance <= sqrPickupRange)
            {
                GetItem();
            }
        }
    }

    private void GetItem()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddBioSample(amount);

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.CheckCapsuleCount(GameManager.Instance.bioSamples);
            }
        }

        // [최적화 4] Destroy 대신 풀링 반환
        // 만약 PoolManager.Instance.ReturnObject(gameObject) 같은 전용 함수가 있다면 그걸로 교체하세요!
        gameObject.SetActive(false);
    }
}