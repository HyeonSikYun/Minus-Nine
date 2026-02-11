using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Linq; // [추가] 리스트 정렬을 위해 필요

public enum WeaponType
{
    Rifle,
    Bazooka,
    FlameThrower,
    Shotgun, // [추가]
    Sniper   // [추가]
}

[System.Serializable]
public class WeaponStats
{
    public string weaponName;
    public WeaponType type;
    public int maxAmmo = 30;
    public float fireRate = 0.1f;
    public int damage = 50;
    public float range = 100f;
    public bool isAutomatic = true;

    [Header("샷건 설정 (Shotgun Only)")]
    public int pellets = 6;         // 한 번에 나가는 총알 수
    public float spreadAngle = 15f; // 부채꼴 각도

    [Header("저격총 설정 (Sniper Only)")]
    public int maxPenetration = 3; // 최대 관통 인원 수

    [Header("발사체 설정")]
    public bool useProjectile = false;
    public string projectilePoolTag = "Rocket";

    [Header("이펙트 설정")]
    public bool useTracer = true;
    public Color tracerColor = Color.yellow;
    public bool useParticle = false;
    public ParticleSystem weaponParticle;
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
    public Transform spawn;
    public Transform shellPoint;
    public float reloadTime = 3f;

    private PlayerController playerController;
    private Coroutine shootCoroutine;
    private float lastFireTime;

    [Header("오디오 소스 연결")]
    public AudioSource gunAudioSource;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    private int GetFinalDamage()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalDamageMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.damage * multiplier);
    }

    private int GetFinalMaxAmmo()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalAmmoMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.maxAmmo * multiplier);
    }

    public void RefreshAmmoUI()
    {
        if (UIManager.Instance != null && currentWeapon != null)
        {
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        }
    }

    private void EquipWeapon(int index)
    {
        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
            currentWeapon.weaponParticle.gameObject.SetActive(false);
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];
        currentAmmo = GetFinalMaxAmmo();

        lastFireTime = -currentWeapon.fireRate;

        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
            UIManager.Instance.ShowReloading(false);
        }

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (GameManager.Instance != null && (GameManager.Instance.isUpgradeMenuOpen || GameManager.Instance.isPaused)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!playerController.hasGun || isReloading) return;

        if (currentAmmo <= 0)
        {
            if (context.started) StartCoroutine(ReloadAndSwitch());
            return;
        }

        if (context.started)
        {
            isHoldingTrigger = true;

            // 화염방사기 사운드 루프 처리
            if (currentWeapon.type == WeaponType.FlameThrower)
            {
                if (SoundManager.Instance != null)
                {
                    gunAudioSource.clip = SoundManager.Instance.flameThrower;
                    gunAudioSource.loop = true;
                    gunAudioSource.Play();
                }
            }

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
                if (Time.time >= lastFireTime + currentWeapon.fireRate)
                {
                    Shoot();
                    lastFireTime = Time.time; // 발사 시간 갱신
                }
            }
        }
        else if (context.canceled)
        {
            isHoldingTrigger = false;

            if (currentWeapon.type == WeaponType.FlameThrower)
            {
                gunAudioSource.Stop();
                gunAudioSource.loop = false;
            }
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

        if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }
        shootCoroutine = null;
    }

    private void Shoot()
    {
        currentAmmo--;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        }

        // --- 발사 방향 계산 ---
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gunPlane = new Plane(Vector3.up, spawn.position);
        float distance;
        Vector3 targetPoint = Vector3.zero;

        if (gunPlane.Raycast(ray, out distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        Vector3 baseDirection;
        float distanceToMouse = Vector3.Distance(transform.position, targetPoint);
        float deadZoneRadius = 2.0f;

        if (distanceToMouse < deadZoneRadius)
        {
            baseDirection = spawn.forward;
        }
        else
        {
            baseDirection = (targetPoint - spawn.position).normalized;
        }
        baseDirection.y = 0;
        baseDirection.Normalize();

        // --- 무기 타입별 로직 분기 ---
        if (currentWeapon.useProjectile) // 바주카 등
        {
            FireProjectile(baseDirection);
        }
        else
        {
            switch (currentWeapon.type)
            {
                case WeaponType.Shotgun:
                    FireShotgun(baseDirection);
                    break;
                case WeaponType.Sniper:
                    FireSniper(baseDirection);
                    break;
                case WeaponType.Rifle:
                default:
                    FireRaycast(baseDirection); // 기존 일반 발사
                    if (currentWeapon.type == WeaponType.Rifle && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.Instance.Rifle, 0.1f);
                    }
                    break;
            }
        }

        if (currentWeapon.ejectShell) SpawnShell();

        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadAndSwitch());
        }
    }

    // [기존] 발사체 발사 로직 분리
    private void FireProjectile(Vector3 direction)
    {
        Quaternion fireRotation = Quaternion.LookRotation(direction);
        GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, fireRotation);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(SoundManager.Instance.Bazooka, 0.1f);

        if (projectileObj != null)
        {
            Projectile proj = projectileObj.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.damage = GetFinalDamage();
                proj.Launch(direction);
            }
        }
    }

    // [신규] 샷건 발사 로직
    private void FireShotgun(Vector3 baseDirection)
    {
        // SoundManager에 Shotgun 클립이 있다고 가정하고 없으면 Rifle 소리라도 냄
        if (SoundManager.Instance != null)
        {
            // SoundManager.Instance.Shotgun 이 있다면 교체하세요. 임시로 Rifle 사용 혹은 null 체크
            SoundManager.Instance.PlaySFX(SoundManager.Instance.shotGun, 0.2f);
        }

        for (int i = 0; i < currentWeapon.pellets; i++)
        {
            // -spreadAngle/2 ~ +spreadAngle/2 사이의 랜덤 각도 생성
            float randomAngle = Random.Range(-currentWeapon.spreadAngle / 2f, currentWeapon.spreadAngle / 2f);

            // Y축 기준 회전 쿼터니언 생성
            Quaternion spreadRotation = Quaternion.Euler(0, randomAngle, 0);

            // 기준 방향을 회전시켜 최종 방향 산출
            Vector3 pelletDirection = spreadRotation * baseDirection;

            // 기존 FireRaycast 재사용 (각 펠릿마다 트레이서 생성됨)
            FireRaycast(pelletDirection);
        }
    }

    // [신규] 저격총 관통 발사 로직
    private void FireSniper(Vector3 direction)
    {
        if (SoundManager.Instance != null)
        {
            // SoundManager.Instance.Sniper 가 있다면 교체하세요.
            SoundManager.Instance.PlaySFX(SoundManager.Instance.sniperShot, 0.3f);
        }

        // RaycastAll로 경로상의 모든 물체 검출
        RaycastHit[] hits = Physics.RaycastAll(spawn.position, direction, currentWeapon.range);

        // 거리순 정렬 (가까운 순서대로 맞아야 함)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int hitCount = 0;
        Vector3 finalEndPoint = spawn.position + (direction * currentWeapon.range); // 기본적으로 최대 사거리까지

        foreach (RaycastHit hit in hits)
        {
            // 자기 자신 충돌 방지 (혹시 모를)
            if (hit.collider.gameObject == gameObject) continue;

            // 벽(Environment)에 맞으면 거기서 관통 멈춤
            if (!hit.collider.CompareTag("Enemy") && !hit.collider.isTrigger)
            {
                // 적이 아닌데 Trigger가 아닌(벽 등) 물체에 닿으면 멈춤
                finalEndPoint = hit.point;
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
                break;
            }

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(GetFinalDamage());
                    EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);

                    hitCount++;
                    // 최대 관통 수 도달 시 멈춤
                    if (hitCount >= currentWeapon.maxPenetration)
                    {
                        finalEndPoint = hit.point; // 시각적 효과는 여기까지
                        break;
                    }
                }
            }
        }

        // 저격총은 관통하므로 트레이서를 맨 마지막 지점까지 한 번만 그림
        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, finalEndPoint, 0.05f, currentWeapon.tracerColor, 0.1f);
        }
    }

    // [기존] 일반 단발(라이플) 발사 로직
    private void FireRaycast(Vector3 direction)
    {
        Ray ray = new Ray(spawn.position, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range))
        {
            endPoint = hit.point;

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(GetFinalDamage());
                }
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
        if (isReloading) yield break;
        isReloading = true;

        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

        if (gunAudioSource.isPlaying && currentWeapon.type == WeaponType.FlameThrower)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowReloading(true);
        }
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

        yield return new WaitForSeconds(reloadTime);

        int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
        EquipWeapon(nextIndex);

        isReloading = false;
    }

    public void SetWeaponVisible(bool isVisible)
    {
        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(isVisible);
            }
        }
    }
}