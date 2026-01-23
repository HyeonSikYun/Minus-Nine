using UnityEngine;
using TMPro; // TextMeshPro 사용 필수

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("좌측 상단: 층수")]
    public TextMeshProUGUI floorText;

    [Header("좌측 하단: 플레이어 체력")]
    public TextMeshProUGUI healthText;

    [Header("우측 하단: 무기 정보")]
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;
    public GameObject reloadingObject; // "Reloading..." 텍스트 혹은 패널 오브젝트

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- 층수 UI 업데이트 ---
    public void UpdateFloor(int floorIndex)
    {
        if (floorText == null) return;

        string floorString = "";

        if (floorIndex < 0)
        {
            // 음수면 지하(B)로 표시 (예: -8 -> B8)
            floorString = $"B{Mathf.Abs(floorIndex)}";
        }
        else if (floorIndex == 0)
        {
            // 0층은 로비(Lobby) 혹은 1F로 처리 (여기서는 Lobby로 표기 예시)
            floorString = "Lobby";
        }
        else
        {
            // 양수면 지상(F)로 표시 (예: 1 -> 1F)
            floorString = $"{floorIndex}F";
        }

        floorText.text = floorString;
    }

    // --- 체력 UI 업데이트 ---
    public void UpdateHealth(int currentHealth)
    {
        if (healthText == null) return;

        // 0보다 작아지면 0으로 고정
        int displayHealth = Mathf.Max(0, currentHealth);
        healthText.text = $"HP {displayHealth}";

        // 체력이 30 이하면 빨간색, 아니면 흰색으로 경고 표시
        if (displayHealth <= 30)
        {
            healthText.color = Color.red;
        }
        else
        {
            healthText.color = Color.white;
        }
    }

    // --- 무기 이름 업데이트 ---
    public void UpdateWeaponName(string name)
    {
        if (weaponNameText != null)
        {
            weaponNameText.text = name;
        }
    }

    // --- 탄약 수 업데이트 ---
    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{current} / {max}";
        }
    }

    // --- 재장전 표시 (깜빡임 등) ---
    public void ShowReloading(bool isReloading)
    {
        if (reloadingObject != null)
        {
            reloadingObject.SetActive(isReloading);
        }

        // 장전 중일 때 탄약 텍스트를 숨기고 싶다면 아래 주석 해제
        // if (ammoText != null) ammoText.gameObject.SetActive(!isReloading);
    }
}