using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 1. 상태 인터페이스 정의
public interface IZombieState
{
    void Enter(ZombieAI zombie);   // 상태 진입 시 1회 실행
    void Execute(ZombieAI zombie); // Update에서 계속 실행
    void Exit(ZombieAI zombie);    // 상태 종료 시 1회 실행
}

// 2. ZombieAI (Context) 클래스
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour, IPooledObject
{
    [Header("타겟 설정")]
    public Transform player;

    [Header("AI 설정")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 3.5f;

    [Header("전투 설정")]
    public float attackCooldown = 2f;
    public int maxHealth = 100;
    public int currentHealth;

    [Header("죽음 설정")]
    public float deathAnimationDuration = 3f;

    [Header("디버그")]
    public bool showGizmos = true;

    // 컴포넌트 참조 (상태 클래스들이 접근할 수 있도록 public 혹은 프로퍼티로)
    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public Collider Col { get; private set; }
    public float LastAttackTime { get; set; } // 공격 쿨타임 계산용

    // 상태 관리 변수
    private IZombieState currentState;

    // 애니메이션 해시 (성능 최적화)
    public readonly int hashIsRun = Animator.StringToHash("isRun");
    public readonly int hashAtk = Animator.StringToHash("zombie1Atk");
    public readonly int hashDie = Animator.StringToHash("zombie1Die");

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponent<Animator>();
        Col = GetComponent<Collider>();

        if (Agent != null) Agent.enabled = false; // 초기화 전 비활성화
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.Execute(this);
        }
    }

    // --- 상태 패턴 핵심 메서드 ---
    public void ChangeState(IZombieState newState)
    {
        if (currentState != null)
        {
            currentState.Exit(this);
        }

        currentState = newState;
        currentState.Enter(this);
    }
    // ---------------------------

    public void OnObjectSpawn()
    {
        currentHealth = maxHealth;
        LastAttackTime = -attackCooldown; // 스폰 직후 바로 공격 가능하게

        if (Col != null) Col.enabled = true;
        if (Anim != null)
        {
            Anim.Rebind();
            Anim.SetBool(hashIsRun, false);
        }

        FindPlayer();

        // 주의: 여기서 바로 IdleState로 가지 않음 (Initialize가 호출될 때 함)
        currentState = null;
    }

    // Spawner에서 호출
    public void Initialize(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        if (Agent != null)
        {
            Agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                Agent.speed = moveSpeed;

                // 초기 상태 설정
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
        if (currentState is DeadState) return;

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            ChangeState(new DeadState());
        }
    }

    // 공격 애니메이션 이벤트에서 호출
    public void DealDamageToPlayer()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange)
        {
            Debug.Log("플레이어 피격 처리!");
            // player.GetComponent<PlayerHealth>()?.TakeDamage(10);
        }
    }

    public void Despawn()
    {
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
    }
}

// =========================================================
// 3. 구체적인 상태 클래스들 (파일 분리해도 됨)
// =========================================================

// [대기 상태]
public class IdleState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        // 감지 범위 안에 들어오면 추적 상태로 전환
        if (dist <= zombie.detectionRange)
        {
            zombie.ChangeState(new ChaseState());
        }
    }

    public void Exit(ZombieAI zombie) { }
}

// [추적 상태]
// [추적 상태]
public class ChaseState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh)
        {
            zombie.Agent.isStopped = false;
            zombie.Agent.speed = zombie.moveSpeed;
        }
        zombie.Anim.SetBool(zombie.hashIsRun, true);
    }

    public void Execute(ZombieAI zombie)
    {
        // 플레이어가 사라졌거나 죽었으면 대기 상태로 복귀
        if (zombie.player == null)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        if (!zombie.Agent.isOnNavMesh) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        // 공격 범위 안에 들어오면 공격 상태로 전환
        if (dist <= zombie.attackRange)
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        // =========================================================
        // [수정됨] 거리가 멀어지면 포기하는 코드를 삭제했습니다.
        // 이제 좀비는 플레이어가 공격 범위에 들어올 때까지 영원히 따라갑니다.
        // =========================================================
        /* if (dist > zombie.detectionRange * 1.5f) 
        {
            zombie.ChangeState(new IdleState());
            return;
        }
        */

        // 계속 플레이어 위치로 이동
        zombie.Agent.SetDestination(zombie.player.position);
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }
}

// [공격 상태]
public class AttackState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        // 플레이어 바라보기 (회전)
        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(
                zombie.transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 5f
            );
        }

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        // 플레이어가 공격 범위를 벗어나면 다시 추적
        if (dist > zombie.attackRange)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        // 쿨타임 체크 후 공격
        if (Time.time >= zombie.LastAttackTime + zombie.attackCooldown)
        {
            zombie.Anim.SetTrigger(zombie.hashAtk);
            zombie.LastAttackTime = Time.time;
        }
    }

    public void Exit(ZombieAI zombie) { }
}

// [사망 상태]
public class DeadState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        // Agent 끄기
        if (zombie.Agent.enabled)
        {
            zombie.Agent.isStopped = true;
            zombie.Agent.ResetPath();
            zombie.Agent.enabled = false;
        }

        // 콜라이더 끄기
        if (zombie.Col != null) zombie.Col.enabled = false;

        // 애니메이션
        zombie.Anim.SetBool(zombie.hashIsRun, false);
        zombie.Anim.SetTrigger(zombie.hashDie);

        // 죽음 처리 (Despawn 예약)
        // 코루틴은 MonoBehaviour인 ZombieAI에서 실행해야 함
        zombie.StartCoroutine(DespawnRoutine(zombie));
    }

    public void Execute(ZombieAI zombie)
    {
        // 죽었으니 아무것도 안 함
    }

    public void Exit(ZombieAI zombie) { }

    private IEnumerator DespawnRoutine(ZombieAI zombie)
    {
        yield return new WaitForSeconds(zombie.deathAnimationDuration);
        zombie.Despawn();
    }
}