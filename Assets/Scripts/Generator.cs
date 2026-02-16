using UnityEngine;
using UnityEngine.InputSystem; // 필수

public class Generator : MonoBehaviour
{
    [Header("상태")]
    public bool isActivated = false;

    [Tooltip("체크하면 게임 매니저에게 알리지 않습니다. (튜토리얼용)")]
    public bool isTutorialGenerator = false;

    [Header("이펙트")]
    public GameObject activeEffect;

    [Header("상호작용 설정")]
    public float holdDuration = 2.0f;
    private float currentHoldTime = 0f;
    private bool playerInRange = false;

    private void Update()
    {
        if (isActivated) return;

        // 플레이어가 범위 안에 있을 때
        if (playerInRange)
        {
            // 1. 키보드 입력 체크 (E키)
            bool isKeyboardHold = Keyboard.current != null && Keyboard.current.eKey.isPressed;

            // 2. 패드 입력 체크 (X버튼 = ButtonWest)
            bool isGamepadHold = Gamepad.current != null && Gamepad.current.buttonWest.isPressed;

            // 둘 중 하나라도 누르고 있다면 진행
            if (isKeyboardHold || isGamepadHold)
            {
                currentHoldTime += Time.deltaTime;

                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateInteractionProgress(currentHoldTime / holdDuration);

                if (currentHoldTime >= holdDuration)
                {
                    SoundManager.Instance.PlaySFX(SoundManager.Instance.generateOn);
                    Activate();
                }
            }
            else
            {
                // 키를 뗐을 때 초기화
                if (currentHoldTime > 0)
                {
                    currentHoldTime = 0f;
                    if (UIManager.Instance != null)
                        UIManager.Instance.UpdateInteractionProgress(0f);
                }
            }
        }
    }

    private void Activate()
    {
        if (isActivated) return;

        isActivated = true;
        currentHoldTime = 0f;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateInteractionProgress(0f);
            UIManager.Instance.ShowInteractionPrompt(false);
        }

        if (activeEffect != null) activeEffect.SetActive(true);

        Debug.Log($"발전기 가동! (튜토리얼 모드: {isTutorialGenerator})");

        if (!isTutorialGenerator)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGeneratorActivated();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            playerInRange = true;
            // 안내 문구 표시 (UIManager가 알아서 패드/키보드 구분해서 띄움)
            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteractionPrompt(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowInteractionPrompt(false);
                UIManager.Instance.UpdateInteractionProgress(0f);
            }
            currentHoldTime = 0f;
        }
    }
}