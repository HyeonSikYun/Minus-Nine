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
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
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

        // UI 갱신 (이름, 탄약, 장전중 끄기)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
            UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
            UIManager.Instance.ShowReloading(false);
        }

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun || isReloading) return;

        // 혹시라도 이미 0발인데 클릭했을 때를 위한 방어 코드
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
        // currentAmmo 체크를 루프 조건에 포함
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

        // Shoot() 내부에서 장전을 호출하므로 여기서는 별도 호출 불필요
    }

    private void Shoot()
    {
        // 1. 탄약 감소
        currentAmmo--;

        // UI 갱신
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
        }

        // 2. 발사 로직 (투사체 or 히트스캔)
        if (currentWeapon.useProjectile)
        {
            GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, spawn.rotation);
            if (projectileObj != null)
            {
                Projectile proj = projectileObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.damage = currentWeapon.damage;
                    proj.Launch(spawn.forward);
                }
            }
        }
        else
        {
            FireRaycast();
        }

        // 3. 탄피 배출
        if (currentWeapon.ejectShell) SpawnShell();

        // [핵심 수정] 쏘고 나서 탄약이 0이 되면 즉시 재장전 실행!
        // (단발 무기도 여기서 걸려서 바로 장전됨)
        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadAndSwitch());
        }
    }

    private void FireRaycast()
    {
        Vector3 direction = spawn.forward;
        Ray ray = new Ray(spawn.position, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range))
        {
            endPoint = hit.point;

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null) zombie.TakeDamage(currentWeapon.damage);
            }
            else if (!currentWeapon.useParticle)
            {
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
            }
        }
        else
        {
            endPoint = spawn.position + (direction * currentWeapon.range);
        }

        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, endPoint, 0.05f, currentWeapon.tracerColor, 0.05f);
        }
    }

    private void SpawnShell()
    {
        GameObject shell = PoolManager.Instance.SpawnFromPool("Shell", shellPoint.position, Quaternion.identity);
        if (shell != null)
        {
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // 탄피 튀는 힘 (GunController 인스펙터에 변수가 없으므로 하드코딩 값 유지)
                Vector3 ejectDir = shellPoint.right + Vector3.up * 0.5f;
                rb.AddForce(ejectDir * 5f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 10f);
            }
            StartCoroutine(ReturnShellAfterDelay(shell, 3f));
        }
    }

    private IEnumerator ReturnShellAfterDelay(GameObject shell, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (shell.activeInHierarchy)
        {
            PoolManager.Instance.ReturnToPool("Shell", shell);
        }
    }

    private IEnumerator ReloadAndSwitch()
    {
        // [중요] 이미 장전 중이면 중복 실행 방지 (Shoot와 AutoLoop에서 동시에 부를 수 있으므로)
        if (isReloading) yield break;

        isReloading = true;

        // 연사 중이었다면 코루틴 정지
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

        // UI 표시
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowReloading(true);
        }

        Debug.Log("탄약 소진! 장전 시작");
        yield return new WaitForSeconds(reloadTime);

        // 다음 무기 교체
        int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
        EquipWeapon(nextIndex);

        isReloading = false;
        // EquipWeapon이 ShowReloading(false)를 호출함
    }
}