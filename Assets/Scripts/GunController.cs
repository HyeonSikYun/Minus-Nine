using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class WeaponStats
{
    public string weaponName;
    public int maxAmmo = 30;
    public float fireRate = 0.1f;
    public int damage = 50;
    public float range = 100f;
    public bool isAutomatic = true;

    [Header("발사체 설정")]
    public bool useProjectile = false; // true: 바주카, false: 히트스캔
    public string projectilePoolTag = "Rocket"; // 풀 매니저에 등록된 이름

    [Header("이펙트 설정")]
    public bool useTracer = true; // 라이플용
    public Color tracerColor = Color.yellow;

    public bool useParticle = false; // 화염방사기용
    public ParticleSystem weaponParticle; // 총구에 붙여둔 화염 파티클 시스템

    public bool ejectShell = true;
}

public class GunController : MonoBehaviour
{
    [Header("무기 설정")]
    public List<WeaponStats> weapons;
    private int currentWeaponIndex = 0;
    private WeaponStats currentWeapon;

    [Header("상태")]
    private int currentAmmo;
    private bool isReloading = false;
    private bool isHoldingTrigger = false;

    [Header("필수 할당")]
    public Transform spawn;       // 총구
    public Transform shellPoint;  // 탄피 배출구
    public float reloadTime = 2f;

    // 내부 변수
    private PlayerController playerController;
    private Coroutine shootCoroutine;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (weapons.Count > 0) EquipWeapon(0);
    }

    private void EquipWeapon(int index)
    {
        // 이전 무기의 파티클이 켜져있다면 끄기
        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
            currentWeapon.weaponParticle.gameObject.SetActive(false);
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];
        currentAmmo = currentWeapon.maxAmmo;

        // 새 무기의 파티클 오브젝트 활성화 (쏘지는 않음)
        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop(); // 일단 멈춤 상태
        }

        UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun || isReloading) return;

        // 탄약 부족 시 자동 교체
        if (currentAmmo <= 0)
        {
            if (context.started) StartCoroutine(ReloadAndSwitch());
            return;
        }

        if (context.started)
        {
            isHoldingTrigger = true;

            // 화염방사기: 누르자마자 파티클 재생 시작
            if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
            {
                currentWeapon.weaponParticle.Play();
            }

            if (currentWeapon.isAutomatic)
            {
                if (shootCoroutine == null) shootCoroutine = StartCoroutine(AutoShootRoutine());
            }
            else
            {
                Shoot(); // 단발 (바주카)
            }
        }
        else if (context.canceled)
        {
            isHoldingTrigger = false;

            // 화염방사기: 떼면 파티클 중지
            if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
            {
                currentWeapon.weaponParticle.Stop();
            }

            if (shootCoroutine != null)
            {
                StopCoroutine(shootCoroutine);
                shootCoroutine = null;
            }
        }
    }

    private IEnumerator AutoShootRoutine()
    {
        while (isHoldingTrigger && currentAmmo > 0 && !isReloading)
        {
            Shoot();
            yield return new WaitForSeconds(currentWeapon.fireRate);
        }

        // 탄약이 다 떨어져서 루프를 탈출한 경우 파티클 끄기
        if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }

        shootCoroutine = null;
        if (currentAmmo <= 0) StartCoroutine(ReloadAndSwitch());
    }

    private void Shoot()
    {
        currentAmmo--;
        UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);

        // 1. 발사체 (바주카포) - 풀링 사용
        if (currentWeapon.useProjectile)
        {
            // 풀에서 가져오기
            GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, spawn.rotation);

            if (projectileObj != null)
            {
                Projectile proj = projectileObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.damage = currentWeapon.damage;
                    // Projectile 스크립트의 Launch 함수로 방향과 속도 주입
                    proj.Launch(spawn.forward);
                }
            }
        }
        // 2. 히트스캔 (라이플, 화염방사기)
        else
        {
            FireRaycast();
        }

        // 탄피 배출
        if (currentWeapon.ejectShell) SpawnShell();
    }

    private void FireRaycast()
    {
        // 화염방사기처럼 정확도가 낮아야 하면 spawn.forward에 랜덤 오차를 더해줄 수도 있음
        Vector3 direction = spawn.forward;

        Ray ray = new Ray(spawn.position, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range))
        {
            endPoint = hit.point;

            // 데미지 처리
            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null) zombie.TakeDamage(currentWeapon.damage);
            }
            // 벽 타격 이펙트 (화염방사기는 벽 타격 이펙트가 필요 없을 수도 있음)
            else if (!currentWeapon.useParticle)
            {
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
            }
        }
        else
        {
            endPoint = spawn.position + (direction * currentWeapon.range);
        }

        // [중요] 라이플만 트레이서를 그림. 화염방사기는 안 그림.
        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, endPoint, 0.05f, currentWeapon.tracerColor, 0.05f);
        }
    }

    private void SpawnShell()
    {
        // 기존 탄피 로직 유지...
        GameObject shell = PoolManager.Instance.SpawnFromPool("Shell", shellPoint.position, Quaternion.identity);
        // ... (생략: 위 코드와 동일) ...
    }

    private IEnumerator ReloadAndSwitch()
    {
        isReloading = true;
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);

        // 화염방사기 쏘다가 끊겼으면 파티클 중지
        if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

        UIManager.Instance.ShowReloading(true);
        yield return new WaitForSeconds(reloadTime);

        // 다음 무기
        int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
        EquipWeapon(nextIndex);

        isReloading = false;
        UIManager.Instance.ShowReloading(false);
    }
}