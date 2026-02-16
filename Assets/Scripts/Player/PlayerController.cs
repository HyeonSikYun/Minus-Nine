using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Movement")]
    public float speed = 5f;
    // public bool isPc; // [삭제] 대신 isGamepad 사용
    private bool isGamepad = false; // [추가] 현재 패드 사용 중인지 체크

    [Header("Gun")]
    public bool hasGun = false;
    [SerializeField] private GunController gunController;

    [Header("Push Prevention")]
    [SerializeField] private float maxPushForce = 2f;
    [SerializeField] private LayerMask pushableLayer;

    [Header("Sound")]
    public float stepRate = 0.4f;
    private float nextStepTime = 0f;

    public bool isSafeZone = false;
    private float verticalVelocity;
    private float gravity = -9.81f;

    // 입력 값 저장 변수
    private Vector2 moveInput;
    private Vector2 lookInput; // 마우스 좌표 or 스틱 방향

    private Vector3 rotationTarget;
    private Animator anim;
    private CharacterController charCon;
    private Vector3 pushForce = Vector3.zero;
    public bool isDead = false;
    private PlayerDamageEffect damageEffect;

    // 1. 이동 입력 (통합)
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // 2. [핵심 수정] 조준 입력 (마우스/패드 통합 처리)
    // Input Actions에서 "Look" 액션에 Mouse Position과 Gamepad Right Stick을 모두 바인딩해야 함
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();

        // 입력 장치가 마우스인지 패드인지 감지하여 모드 전환
        if (context.control.device is Gamepad)
        {
            isGamepad = true;
        }
        else if (context.control.device is Mouse)
        {
            isGamepad = false;
        }
    }

    // (기존 분리된 함수들은 삭제하거나 OnLook으로 통합됨)
    // public void OnMouseLook... (삭제)
    // public void OnJoystickLook... (삭제)

    private void Start()
    {
        anim = GetComponent<Animator>();
        charCon = GetComponent<CharacterController>();
        damageEffect = GetComponent<PlayerDamageEffect>();

        if (gunController == null)
            gunController = GetComponentInChildren<GunController>();

        if (GameManager.Instance != null && (GameManager.Instance.currentFloor >= -8 || GameManager.Instance.isRetry))
        {
            hasGun = true;
            anim.SetBool("gunReady", true);
            if (gunController != null) gunController.EquipStartingWeapon();
        }
        else
        {
            hasGun = false;
            anim.SetBool("gunReady", false);
            if (gunController != null) gunController.HideAllWeapons();
        }

        isDead = false;
        // isPc = true; // [삭제]
        currentHealth = maxHealth;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
        }
    }

    void Update()
    {
        if (isDead) return;

        UpdateGunState();
        ApplyGravity();
        UpdateMovement();
        ApplyPushForce();
        HandleFootstep();
    }

    private void HandleFootstep()
    {
        if (charCon.isGrounded && moveInput.magnitude > 0.1f && Time.time >= nextStepTime)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.footStep);
            }
            nextStepTime = Time.time + stepRate;
        }
    }

    public void AcquireGun()
    {
        hasGun = true;
        if (AchiManager.Instance != null) AchiManager.Instance.UnlockAchi(1);
        if (anim != null) anim.SetBool("gunReady", true);
        if (gunController != null) gunController.EquipStartingWeapon();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetWeaponUIVisible(true);
            if (gunController != null) gunController.RefreshAmmoUI();
        }
    }

    // --- 체력 관련 함수 ---
    public bool IsHealthFull() { return currentHealth >= maxHealth; }
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        if (HealthSystem.Instance != null) HealthSystem.Instance.HealDamage(amount);
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;
        currentHealth -= damage;

        if (HealthSystem.Instance != null) HealthSystem.Instance.TakeDamage(damage);
        if (damageEffect != null) damageEffect.OnTakeDamage();

        if (currentHealth <= 0)
        {
            Debug.Log("플레이어 사망!");
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        if (charCon != null) charCon.enabled = false;
        if (anim != null) anim.SetTrigger("Dead");
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDead();
    }

    private float GetFinalSpeed()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalMoveSpeedMultiplier : 1.0f;
        return speed * multiplier;
    }

    private void ApplyGravity()
    {
        if (charCon.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
        else verticalVelocity += gravity * Time.deltaTime;
    }

    private void ApplyPushForce()
    {
        if (pushForce.magnitude > 0.01f)
        {
            charCon.Move(pushForce * Time.deltaTime);
            pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 5f);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Enemy") || hit.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            Vector3 pushDir = transform.position - hit.transform.position;
            pushDir.y = 0;
            pushDir = pushDir.normalized;
            pushForce += pushDir * maxPushForce;
            pushForce = Vector3.ClampMagnitude(pushForce, maxPushForce);
        }
    }

    private void UpdateGunState()
    {
        anim.SetBool("gunReady", hasGun);
    }

    // [수정] 이동 및 회전 통합 처리
    private void UpdateMovement()
    {
        // 1. 회전 처리 (마우스 vs 패드 자동 분기)
        UpdateRotation();

        // 2. 이동 벡터 계산
        // 카메라는 보통 45도 틀어져 있으므로, 입력값을 카메라 기준으로 변환
        Vector3 targetVector = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;

        // 애니메이션을 위한 로컬 이동 벡터 (회전된 몸 기준 앞/뒤/좌/우)
        Vector3 localMove = transform.InverseTransformDirection(movement);

        // 애니메이션 갱신
        UpdateAnimation(localMove.x, localMove.z, movement.magnitude > 0.01f);

        // 실제 캐릭터 이동
        movement.y = verticalVelocity;
        charCon.Move(movement * GetFinalSpeed() * Time.deltaTime);
    }

    // [삭제] UpdateMouseAim, movePlayer, movePlayerWithAim 등은 UpdateMovement와 UpdateRotation으로 통합됨

    private void UpdateRotation()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        // ==========================================
        // [CASE A] 게임 패드 사용 시 (스틱 방향 회전)
        // ==========================================
        if (isGamepad)
        {
            // 데드존 처리 (스틱을 조금이라도 밀었을 때)
            if (lookInput.sqrMagnitude > 0.01f)
            {
                // 입력값(x, y)를 3D 방향(x, 0, z)으로 변환
                Vector3 lookDir = new Vector3(lookInput.x, 0, lookInput.y);

                // 캐릭터 회전
                // (탑다운 슈팅의 경우 카메라 회전에 영향을 받지 않는 절대 방향이 더 직관적일 수 있음)
                // 만약 카메라 기준 회전이 필요하다면 아래 주석 해제:
                // lookDir = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * lookDir;

                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 0.15f);
                }
            }
        }
        // ==========================================
        // [CASE B] 마우스 사용 시 (레이캐스트 좌표 회전)
        // ==========================================
        else
        {
            Ray ray = Camera.main.ScreenPointToRay(lookInput); // OnLook에서 받은 마우스 좌표 사용
            Plane playerPlane = new Plane(Vector3.up, transform.position);
            float distance = 0f;

            if (playerPlane.Raycast(ray, out distance))
            {
                rotationTarget = ray.GetPoint(distance);

                var lookPos = rotationTarget - transform.position;
                lookPos.y = 0f; // 높이 고정

                if (lookPos != Vector3.zero)
                {
                    var targetRotation = Quaternion.LookRotation(lookPos);
                    float dist = lookPos.magnitude;

                    // 거리가 가까우면 즉시 회전, 멀면 부드럽게
                    if (dist < 3.0f)
                    {
                        transform.rotation = targetRotation;
                    }
                    else
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
                    }
                }
            }
        }
    }

    private void UpdateAnimation(float moveX, float moveY, bool isMoving)
    {
        anim.SetFloat("MoveX", moveX);
        anim.SetFloat("MoveY", moveY);
        anim.SetBool("isMoving", isMoving);
    }

    public void PlayWeaponChangeAnim()
    {
        if (anim != null)
        {
            anim.SetTrigger("isChange");
        }
    }
}