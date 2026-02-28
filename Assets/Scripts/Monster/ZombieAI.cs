using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using FIMSpace.FProceduralAnimation;

public interface IZombieState
{
    void Enter(ZombieAI zombie);
    void Execute(ZombieAI zombie);
    void Exit(ZombieAI zombie);
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour, IPooledObject
{
    // ★ [스피드 좀비 추가] enum에 Speed 추가
    public enum ZombieType { Normal, Explosive, King, Speed }
    private static float lastGlobalHitSoundTime = 0f;
    [Header("좀비 타입 설정")]
    public ZombieType zombieType = ZombieType.Normal;

    [Header("타겟 설정")]
    public Transform player;

    [Header("AI 설정")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 3.5f;

    // ★ [스피드 좀비 추가] 스피드 좀비 전용 속도 배율
    [Header("스피드 좀비 설정")]
    public float speedMultiplier = 2.0f; // 기본 속도의 2배

    [Header("충돌 방지")]
    public LayerMask zombieLayer;

    [Header("전투 설정")]
    public float attackCooldown = 2f;
    public float attackDelay = 0.5f;
    public int defaultMaxHealth = 100;
    public int maxHealth;
    public int currentHealth;

    [Header("죽음 설정")]
    public float deathAnimationDuration = 3f;

    [Header("폭발 설정 (Explosive 타입 전용)")]
    public float explosionRange = 3.0f; // 폭발 범위
    public int explosionDamage = 50;    // 플레이어에게 줄 데미지
    public GameObject explosionEffect;  // 폭발 이펙트 프리팹
    public GameObject rangeIndicatorPrefab;

    [Header("피격 플래시 설정")]
    public Renderer meshRenderer;
    public Color damageColor = Color.red;
    private Color originColor;
    public Material flashMaterial;
    private Material originalMaterial;

    [Header("드랍 아이템")]
    public GameObject bioSamplePrefab;
    public LayerMask groundLayer;

    [Header("사운드 설정")]
    public AudioSource audioSource;

    [Header("디버그")]
    public bool showGizmos = true;

    public bool isDead = false;

    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public Collider Col { get; private set; }
    public float LastAttackTime { get; set; }

    private IZombieState currentState;

    public readonly int hashIsRun = Animator.StringToHash("isRun");
    public readonly int hashIsCrawling = Animator.StringToHash("isCrawling");
    public readonly int hashAtk = Animator.StringToHash("zombie1Atk");
    public readonly int hashDie = Animator.StringToHash("zombie1Die");
    public readonly int hashFrontDie = Animator.StringToHash("zombieFrontDie");
    public readonly int hashDie3 = Animator.StringToHash("die3");
    public readonly int hashDie4 = Animator.StringToHash("die4");
    public readonly int hashDie5 = Animator.StringToHash("die5");
    public readonly int hashIsWalk = Animator.StringToHash("isWalk");       // 걷기 (Bool)
    public readonly int hashKingDead = Animator.StringToHash("isDead");     // 사망 (Trigger)
    public readonly int hashKingAttack = Animator.StringToHash("isAttack"); // 공격 (Trigger)

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponent<Animator>();
        Col = GetComponent<Collider>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (Agent != null) Agent.enabled = false;
    }

    private void Start()
    {
        meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        originalMaterial = meshRenderer.material;
        HideMyself();
        FindPlayer();
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.Execute(this);
        }
    }

    private void HideMyself()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
    }

    public bool IsBlockedByZombie()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, 1.5f, zombieLayer))
        {
            if (hit.collider.gameObject != gameObject)
            {
                return true;
            }
        }
        return false;
    }

    public void ChangeState(IZombieState newState)
    {
        if (currentState != null)
        {
            currentState.Exit(this);
        }

        currentState = newState;
        currentState.Enter(this);
    }

    public void OnObjectSpawn()
    {
        HideMyself();
        float multiplier = 1.0f;
        if (GameManager.Instance != null)
        {
            multiplier = GameManager.Instance.GetZombieHP_Multiplier();
        }

        maxHealth = Mathf.RoundToInt(defaultMaxHealth * multiplier);
        currentHealth = maxHealth;
        LastAttackTime = -attackCooldown;
        isDead = false;

        if (zombieType == ZombieType.King)
        {
            if (SoundManager.Instance != null && SoundManager.Instance.kingZombieSound != null)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.kingZombieSound);
            }
        }

        if (meshRenderer != null) originColor = meshRenderer.material.color;

        Anim.SetLayerWeight(1, 1f);
        Anim.speed = 1.0f;

        if (Col != null)
        {
            Col.enabled = true;
            Col.isTrigger = false;
        }

        if (Anim != null)
        {
            Anim.Rebind();

            if (zombieType == ZombieType.Explosive)
            {
                Anim.SetBool(hashIsCrawling, false);
            }
            else if (zombieType == ZombieType.King)
            {
                Anim.SetBool(hashIsWalk, false);
            }
            else
            {
                // ★ [스피드 좀비 추가] 일반 좀비와 스피드 좀비는 모두 isRun을 사용
                Anim.SetBool(hashIsRun, false);
            }
        }

        FindPlayer();
        currentState = null;
    }

    public void Initialize(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        if (Agent != null)
        {
            Agent.enabled = true;
            Agent.stoppingDistance = (zombieType == ZombieType.Explosive) ? 0.5f : attackRange - 0.5f;

            // ★ [스피드 좀비 추가] 스피드 좀비는 무리를 비집고 나오도록 회피 우선순위를 높임 (숫자가 작을수록 우선)
            if (zombieType == ZombieType.Speed)
            {
                Agent.avoidancePriority = 10; // 비켜라 내가 먼저 간다!
            }
            else
            {
                Agent.avoidancePriority = 50; // 기본 좀비들
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                Agent.speed = moveSpeed;
                ChangeState(new IdleState());
            }
            else
            {
                Agent.enabled = false;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || currentState is DeadState) return;

        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.isSafeZone) return;
        }

        if (meshRenderer != null)
        {
            StopCoroutine("HitFlashRoutine");
            StartCoroutine("HitFlashRoutine");
        }

        if (Time.time >= lastGlobalHitSoundTime + 0.05f)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.gunHit);
            lastGlobalHitSoundTime = Time.time;
        }

        currentHealth -= damage;
        GameManager.Instance.ShowDamagePopup(transform.position, damage);

        if (currentHealth <= 0)
        {
            if (zombieType == ZombieType.Explosive)
            {
                if (!isDead)
                {
                    StartCoroutine(ExplodeRoutine());
                }
            }
            else
            {
                ChangeState(new DeadState());
            }
        }
        else
        {
            if (currentState is IdleState)
            {
                ChangeState(new ChaseState());
            }
        }
    }

    private IEnumerator ExplodeRoutine()
    {
        isDead = true;

        if (Agent != null && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
        }

        StopCoroutine("HitFlashRoutine");

        GameObject indicator = null;
        if (rangeIndicatorPrefab != null)
        {
            Vector3 spawnPos = transform.position;
            spawnPos.y += 0.2f;

            indicator = Instantiate(rangeIndicatorPrefab, spawnPos, Quaternion.identity);
            float size = explosionRange * 2.0f;
            indicator.transform.localScale = new Vector3(size, 0.1f, size);
        }

        int blinkCount = 5;
        float blinkSpeed = 0.1f;

        for (int i = 0; i < blinkCount; i++)
        {
            if (meshRenderer != null) meshRenderer.material = flashMaterial;
            if (indicator != null) indicator.SetActive(true);

            yield return new WaitForSeconds(blinkSpeed);

            if (meshRenderer != null) meshRenderer.material = originalMaterial;
            if (indicator != null) indicator.SetActive(false);

            yield return new WaitForSeconds(blinkSpeed);
        }

        if (indicator != null) Destroy(indicator);

        Explode();
    }

    private void Explode()
    {
        isDead = true;

        if (explosionEffect != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
            Instantiate(explosionEffect, spawnPos, Quaternion.identity);
        }

        SoundManager.Instance.PlaySFX(SoundManager.Instance.zombieExplosion);

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRange);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                PlayerController pc = col.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(explosionDamage);
                    Debug.Log("플레이어 폭발 데미지 입음!");
                }
            }
        }

        if (Agent != null && Agent.enabled) Agent.enabled = false;
        PoolManager.Instance.ReturnToPool("Zombie", gameObject);
    }

    public void OnHearGunshot(Vector3 playerPos)
    {
        if (isDead) return;

        if (currentState is IdleState)
        {
            ChangeState(new ChaseState());
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        meshRenderer.material = flashMaterial;
        yield return new WaitForSeconds(0.1f);
        meshRenderer.material = originalMaterial;
    }

    public IEnumerator DealDamageWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DealDamageToPlayer();
    }

    public void DealDamageToPlayer()
    {
        if (player == null || isDead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange + 0.5f)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                if (zombieType == ZombieType.Explosive) return;

                if (zombieType == ZombieType.King)
                {
                    pc.TakeDamage(20);
                }
                // ★ [스피드 좀비 추가] 스피드 좀비도 일반 좀비와 동일한 10 데미지
                else if (zombieType == ZombieType.Normal || zombieType == ZombieType.Speed)
                {
                    pc.TakeDamage(10);
                }
            }
        }
    }

    private void DropItem()
    {
        Vector3 finalSpawnPos = transform.position;

        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }
        else
        {
            if (player != null)
            {
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                finalSpawnPos = transform.position + (dirToPlayer * 1.0f);
            }
        }

        finalSpawnPos.y += 1f;
        Quaternion spawnRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

        PoolManager.Instance.SpawnFromPool("BioSample", finalSpawnPos, spawnRotation);
    }

    public void Despawn()
    {
        DropItem();
        if (Agent != null && Agent.enabled) Agent.enabled = false;
        PoolManager.Instance.ReturnToPool("Zombie", gameObject);
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (zombieType == ZombieType.Explosive)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
            Gizmos.DrawSphere(transform.position, explosionRange);
        }
    }
}

// ================= 상태 클래스들 =================

public class IdleState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;

        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else if (zombie.zombieType == ZombieAI.ZombieType.King)
        {
            zombie.Anim.SetBool(zombie.hashIsWalk, false);
        }
        else // ★ [스피드 좀비 추가] Normal과 Speed는 isRun 공유
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);
        }
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        if (dist <= zombie.detectionRange)
        {
            PlayerController pc = zombie.player.GetComponent<PlayerController>();
            if (pc != null && pc.isSafeZone) return;

            zombie.ChangeState(new ChaseState());
        }
    }

    public void Exit(ZombieAI zombie) { }
}

public class ChaseState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh)
        {
            zombie.Agent.isStopped = false;
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
            {
                zombie.Agent.speed = zombie.moveSpeed * 0.6f;
                zombie.Anim.SetBool(zombie.hashIsCrawling, true);
            }
            else if (zombie.zombieType == ZombieAI.ZombieType.King)
            {
                zombie.Anim.SetBool(zombie.hashIsWalk, true);
            }
            // ★ [스피드 좀비 추가] 달리기 속도 배율 적용
            else if (zombie.zombieType == ZombieAI.ZombieType.Speed)
            {
                zombie.Agent.speed = zombie.moveSpeed * zombie.speedMultiplier; // 2배 속도 적용!
                zombie.Anim.speed = zombie.speedMultiplier;
                zombie.Anim.SetBool(zombie.hashIsRun, true);
            }
            else // 일반 좀비
            {
                zombie.Agent.speed = zombie.moveSpeed;
                zombie.Anim.speed = 1.0f;
                zombie.Anim.SetBool(zombie.hashIsRun, true);
            }
        }

        if (SoundManager.Instance != null)
        {
            var sm = SoundManager.Instance;
            System.Collections.Generic.List<AudioClip> chaseClips = new System.Collections.Generic.List<AudioClip>();

            if (sm.zombieChase != null) chaseClips.Add(sm.zombieChase);
            if (sm.zombieChase2 != null) chaseClips.Add(sm.zombieChase2);
            if (sm.zombieChase3 != null) chaseClips.Add(sm.zombieChase3);

            if (chaseClips.Count > 0)
            {
                int randomIndex = Random.Range(0, chaseClips.Count);
                AudioClip selectedClip = chaseClips[randomIndex];

                zombie.audioSource.clip = selectedClip;
                zombie.audioSource.loop = true;

                if (selectedClip == sm.zombieChase)
                {
                    zombie.audioSource.volume = 0.1f;
                }
                else
                {
                    zombie.audioSource.volume = 0.1f;
                }

                zombie.audioSource.Play();
            }
        }
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;
        if (!zombie.Agent.isOnNavMesh) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        PlayerController pc = zombie.player.GetComponent<PlayerController>();
        if (pc != null && pc.isSafeZone)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        if (zombie.zombieType == ZombieAI.ZombieType.Explosive && dist <= 1.5f)
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        bool isBlocked = zombie.IsBlockedByZombie();
        float checkDist = (zombie.zombieType == ZombieAI.ZombieType.Explosive) ? 1.5f : zombie.attackRange;

        if (dist <= checkDist || (dist < 5.0f && isBlocked))
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        zombie.Agent.SetDestination(zombie.player.position);
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;

        zombie.Anim.speed = 1.0f;

        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else if (zombie.zombieType == ZombieAI.ZombieType.King)
        {
            zombie.Anim.SetBool(zombie.hashIsWalk, false);
        }
        else // ★ [스피드 좀비 추가] Normal과 Speed 공유
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);
        }
    }
}

public class AttackState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);
        float stopDistance = 1.5f;

        // 1. 폭발 좀비 자폭 처리
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            if (dist <= 2.0f)
            {
                zombie.TakeDamage(50);
                return;
            }
        }

        bool isBlocked = zombie.IsBlockedByZombie();

        // 2. 공격 사거리 밖이면 무빙 (다가가기)
        if (dist > stopDistance && !isBlocked)
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = false;
                zombie.Agent.SetDestination(zombie.player.position);
            }

            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
                zombie.Anim.SetBool(zombie.hashIsCrawling, true);
            else if (zombie.zombieType == ZombieAI.ZombieType.King)
            {
                zombie.Anim.SetBool(zombie.hashIsWalk, true);
            }
            else // 일반 & 스피드 좀비
            {
                // ★ [스피드 좀비] 무빙 중일 때 애니메이션 속도 적용
                if (zombie.zombieType == ZombieAI.ZombieType.Speed)
                    zombie.Anim.speed = zombie.speedMultiplier;
                else
                    zombie.Anim.speed = 1.0f;

                zombie.Anim.SetBool(zombie.hashIsRun, true);
            }
        }
        else // 제자리에 멈춤
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = true;
                zombie.Agent.velocity = Vector3.zero;
                zombie.Agent.ResetPath();
            }
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
                zombie.Anim.SetBool(zombie.hashIsCrawling, false);
            else if (zombie.zombieType == ZombieAI.ZombieType.King)
            {
                zombie.Anim.SetBool(zombie.hashIsWalk, false);
            }
            else
            {
                zombie.Anim.SetBool(zombie.hashIsRun, false);
            }
        }

        // 플레이어 쪽 바라보기
        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(zombie.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        if (!isBlocked && dist > zombie.attackRange + 0.5f)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        // 3. 실제 공격(타격) 실행 로직
        if (zombie.zombieType == ZombieAI.ZombieType.Normal || zombie.zombieType == ZombieAI.ZombieType.King || zombie.zombieType == ZombieAI.ZombieType.Speed)
        {
            // ★ [스피드 좀비] 쿨다운을 배율만큼 짧게! (예: 2초 -> 1초)
            float currentCooldown = (zombie.zombieType == ZombieAI.ZombieType.Speed) ? (zombie.attackCooldown / zombie.speedMultiplier) : zombie.attackCooldown;

            if (Time.time >= zombie.LastAttackTime + currentCooldown)
            {
                // ★ [스피드 좀비] 공격 애니메이션 속도 빠르게!
                if (zombie.zombieType == ZombieAI.ZombieType.Speed)
                    zombie.Anim.speed = zombie.speedMultiplier;
                else
                    zombie.Anim.speed = 1.0f;

                if (zombie.zombieType == ZombieAI.ZombieType.King)
                {
                    zombie.Anim.SetTrigger(zombie.hashKingAttack);
                }
                else
                {
                    zombie.Anim.SetTrigger(zombie.hashAtk);
                }

                zombie.LastAttackTime = Time.time;

                // ★ [스피드 좀비] 데미지 판정이 들어가는 딜레이도 배율만큼 짧게! (예: 0.5초 -> 0.25초)
                float currentDelay = (zombie.zombieType == ZombieAI.ZombieType.Speed) ? (zombie.attackDelay / zombie.speedMultiplier) : zombie.attackDelay;

                zombie.StartCoroutine(zombie.DealDamageWithDelay(currentDelay));
            }
        }
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = false;

        // ★ 다른 상태(사망, 대기 등)로 넘어갈 때 허우적대지 않도록 1배속으로 원상복구
        zombie.Anim.speed = 1.0f;

        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        else if (zombie.zombieType == ZombieAI.ZombieType.King)
            zombie.Anim.SetBool(zombie.hashIsWalk, false);
        else
            zombie.Anim.SetBool(zombie.hashIsRun, false);
    }
}

public class DeadState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        zombie.isDead = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddZombieKill();
        }

        if (zombie.audioSource != null)
        {
            zombie.audioSource.Stop();
            zombie.audioSource.loop = false;

            if (SoundManager.Instance != null)
            {
                System.Collections.Generic.List<AudioClip> dieClips = new System.Collections.Generic.List<AudioClip>();

                if (SoundManager.Instance.zombieDie != null) dieClips.Add(SoundManager.Instance.zombieDie);
                if (SoundManager.Instance.zombieDie2 != null) dieClips.Add(SoundManager.Instance.zombieDie2);
                if (SoundManager.Instance.zombieDie3 != null) dieClips.Add(SoundManager.Instance.zombieDie3);

                if (dieClips.Count > 0)
                {
                    int randomIndex = Random.Range(0, dieClips.Count);
                    zombie.audioSource.PlayOneShot(dieClips[randomIndex], 1.0f);
                }
            }
        }

        if (zombie.Agent.enabled)
        {
            zombie.Agent.isStopped = true;
            zombie.Agent.ResetPath();
            zombie.Agent.enabled = false;
        }

        if (zombie.Col != null)
        {
            zombie.Col.isTrigger = true;
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnZombieKilled();
        }

        zombie.StartCoroutine(RagdollDeathRoutine(zombie));
    }

    public void Execute(ZombieAI zombie) { }
    public void Exit(ZombieAI zombie) { }

    private IEnumerator RagdollDeathRoutine(ZombieAI zombie)
    {
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else if (zombie.zombieType == ZombieAI.ZombieType.King)
        {
            zombie.Anim.SetBool(zombie.hashIsWalk, false);
            zombie.Anim.SetTrigger(zombie.hashKingDead);
        }
        else
        // ★ [스피드 좀비 추가] 일반 & 스피드 좀비 처리
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);

            int[] deathTriggers = new int[]
            {
                zombie.hashDie,
                zombie.hashFrontDie,
                zombie.hashDie3,
                zombie.hashDie4,
                zombie.hashDie5
            };

            int randomIndex = Random.Range(0, deathTriggers.Length);
            zombie.Anim.SetTrigger(deathTriggers[randomIndex]);
        }

        yield return new WaitForSeconds(zombie.deathAnimationDuration);
        zombie.Despawn();
    }
}