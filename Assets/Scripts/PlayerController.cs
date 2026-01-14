using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed;
    public bool isPc;

    [Header("Gun")]
    public bool hasGun = false;
    [SerializeField] private GunController gunController;

    private float verticalVelocity; // 중력용 변수 추가
    private float gravity = -9.81f;
    private Vector2 move, mouseLook, joystickLook;
    private Vector3 rotationTarget;
    private Animator anim;
    private CharacterController charCon;

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

        // GunController가 자식 오브젝트에 있다면 자동으로 찾기
        if (gunController == null)
        {
            gunController = GetComponentInChildren<GunController>();
        }
    }

    void Update()
    {
        UpdateGunState();
        ApplyGravity(); // 중력 적용
        UpdateMovement();
    }

    private void ApplyGravity()
    {
        if (charCon.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // 땅에 붙어있도록
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
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

        // Y축에 중력 추가
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

        // Y축에 중력 추가
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