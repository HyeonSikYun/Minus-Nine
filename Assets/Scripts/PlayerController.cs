using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Movement")]
    public float speed;
    public bool isPc;

    [Header("Gun")]
    public bool hasGun = false;
    [SerializeField] private GunController gunController;

    [Header("Push Prevention")]
    [SerializeField] private float maxPushForce = 2f; // 최대 밀림 힘
    [SerializeField] private LayerMask pushableLayer; // 밀 수 있는 레이어 (좀비 등)

    private float verticalVelocity;
    private float gravity = -9.81f;
    private Vector2 move, mouseLook, joystickLook;
    private Vector3 rotationTarget;
    private Animator anim;
    private CharacterController charCon;
    private Vector3 pushForce = Vector3.zero; // 누적된 밀림 힘

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        mouseLook = context.ReadValue<Vector2>();
    }

    public void OnJoystickLook(InputAction.CallbackContext context)
    {
        joystickLook = context.ReadValue<Vector2>();
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        charCon = GetComponent<CharacterController>();

        if (gunController == null)
        {
            gunController = GetComponentInChildren<GunController>();
        }

        // [추가] 체력 초기화 및 UI 갱신
        currentHealth = maxHealth;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
        }
    }

    void Update()
    {
        UpdateGunState();
        ApplyGravity();
        UpdateMovement();
        ApplyPushForce(); // 밀림 힘 적용
    }

    // [추가] 데미지 처리 함수 (좀비가 호출함)
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        // UI 갱신
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
        }

        if (currentHealth <= 0)
        {
            Debug.Log("플레이어 사망!");
            // 여기에 게임 오버 로직 추가 (예: GameManager.Instance.GameOver())
        }
    }

    private void ApplyGravity()
    {
        if (charCon.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void ApplyPushForce()
    {
        // 밀림 힘을 감쇠시키면서 적용
        if (pushForce.magnitude > 0.01f)
        {
            charCon.Move(pushForce * Time.deltaTime);
            pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 5f);
        }
    }

    // CharacterController의 OnControllerColliderHit으로 충돌 처리
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 좀비와 충돌했을 때
        if (hit.gameObject.CompareTag("Enemy") || hit.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            // 밀림 방향 계산 (좀비 -> 플레이어)
            Vector3 pushDir = transform.position - hit.transform.position;
            pushDir.y = 0; // Y축 밀림 방지
            pushDir = pushDir.normalized;

            // 현재 밀림 힘에 추가 (최대값 제한)
            pushForce += pushDir * maxPushForce;
            pushForce = Vector3.ClampMagnitude(pushForce, maxPushForce);
        }
    }

    private void UpdateGunState()
    {
        anim.SetBool("gunReady", hasGun);
    }

    private void UpdateMovement()
    {
        if (isPc)
        {
            UpdateMouseAim();
            movePlayerWithAim();
        }
        else
        {
            if (joystickLook.x == 0 && joystickLook.y == 0)
            {
                movePlayer();
            }
            else
            {
                movePlayerWithAim();
            }
        }
    }

    private void UpdateMouseAim()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(mouseLook);
        if (Physics.Raycast(ray, out hit))
        {
            rotationTarget = hit.point;
        }
    }

    public void movePlayer()
    {
        Vector3 targetVector = new Vector3(move.x, 0f, move.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;

        UpdateAnimation(move.x, move.y, movement.magnitude > 0.01f);

        if (movement != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.15f);
        }

        movement.y = verticalVelocity;
        charCon.Move(movement * speed * Time.deltaTime);
    }

    public void movePlayerWithAim()
    {
        UpdateRotation();

        Vector3 targetVector = new Vector3(move.x, 0f, move.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;
        Vector3 localMove = transform.InverseTransformDirection(movement);

        UpdateAnimation(localMove.x, localMove.z, movement.magnitude > 0.01f);

        movement.y = verticalVelocity;
        charCon.Move(movement * speed * Time.deltaTime);
    }

    private void UpdateRotation()
    {
        if (isPc)
        {
            var lookPos = rotationTarget - transform.position;
            lookPos.y = 0f;

            if (lookPos != Vector3.zero)
            {
                var rotation = Quaternion.LookRotation(lookPos);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.15f);
            }
        }
        else
        {
            Vector3 aimDir = new Vector3(joystickLook.x, 0f, joystickLook.y);
            if (aimDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimDir), 0.15f);
            }
        }
    }

    private void UpdateAnimation(float moveX, float moveY, bool isMoving)
    {
        anim.SetFloat("MoveX", moveX);
        anim.SetFloat("MoveY", moveY);
        anim.SetBool("isMoving", isMoving);
    }
}