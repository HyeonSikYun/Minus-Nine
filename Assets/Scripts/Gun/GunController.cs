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

    [Header("모델 및 발사 위치 연결 (필수)")]
    public GameObject weaponModel; // 1. 이 무기의 3D 모델 (켜고 끌 대상)
    public Transform muzzlePoint;  // 2. 이 무기의 총구 위치 (총알 나가는 곳)
    public Transform shellEjectPoint;

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

    [Header("머즐 이펙트 (Muzzle Flash)")]
    public bool useMuzzleFlash = true;      // 이펙트 사용 여부
    public string muzzleFlashTag = "MuzzleFlash_Rifle";
}

public class GunController : MonoBehaviour
{
    [Header("무기 설정")]
    public List<WeaponStats> weapons;
    private int currentWeaponIndex = 0;
    private WeaponStats currentWeapon;
    private int[] weaponAmmoList;
    private bool[] isWeaponUnlocked;
    private int nextUnlockIndex = 2; //

    [Header("상태")]
    //private int currentAmmo;
    private bool isReloading = false;
    private bool isHoldingTrigger = false;
    private bool isSwitching = false;

    [Header("필수 할당")]
    //public Transform spawn;
    //public Transform shellPoint;
    public float reloadTime = 3f;
    private Transform currentMuzzlePoint;

    public PlayerController playerController;
    private Coroutine shootCoroutine;
    private float lastFireTime;

    [Header("오디오 소스 연결")]
    public AudioSource gunAudioSource;

    [Header("충돌 설정")]
    public LayerMask obstacleLayer;

    private void Start()
    {
        playerController = GetComponent<PlayerController>();

        // [수정] 데이터 초기화 및 1, 2번 무기 해금
        int count = weapons.Count;
        weaponAmmoList = new int[count];
        isWeaponUnlocked = new bool[count];

        for (int i = 0; i < count; i++)
        {
            // 탄약 꽉 채우기 & 일단 다 잠금
            weaponAmmoList[i] = GetFinalMaxAmmo(weapons[i]);
            isWeaponUnlocked[i] = false;
        }

        // 1번(Index 0), 2번(Index 1)만 해제
        if (count >= 1) isWeaponUnlocked[0] = true;
        if (count >= 2) isWeaponUnlocked[1] = true;

        nextUnlockIndex = 2; // 다음 해금될 무기 번호

        if (playerController != null && playerController.hasGun)
        {
            if (weapons.Count > 0)
            {
                EquipWeapon(0);
            }
        }
        else
        {
            // 총이 없다면(튜토리얼 등) -> 모든 모델 숨기기
            HideAllWeapons();
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;
        if (isReloading || isSwitching) return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;

            if (scrollY > 0) // 휠 올림 (다음 무기)
            {
                SwitchToNextWeapon();
            }
            else if (scrollY < 0) // 휠 내림 (이전 무기)
            {
                SwitchToPreviousWeapon();
            }
        }

        if (Gamepad.current != null)
        {
            // RB (Right Bumper) -> 다음 무기
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                SwitchToNextWeapon();
            }

            // LB (Left Bumper) -> 이전 무기
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                SwitchToPreviousWeapon();
            }
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 잠금 해제된 무기만 교체 가능
        if (keyboard.digit1Key.wasPressedThisFrame) TrySwitchWeapon(0);
        if (keyboard.digit2Key.wasPressedThisFrame) TrySwitchWeapon(1);
        if (keyboard.digit3Key.wasPressedThisFrame) TrySwitchWeapon(2);
        if (keyboard.digit4Key.wasPressedThisFrame) TrySwitchWeapon(3);
        if (keyboard.digit5Key.wasPressedThisFrame) TrySwitchWeapon(4);
    }

    private int GetFinalDamage()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalDamageMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.damage * multiplier);
    }

    private int GetFinalMaxAmmo()
    {
        if (currentWeapon == null) return 0;
        return GetFinalMaxAmmo(currentWeapon);
    }

    public void RefreshAmmoUI()
    {
        if (UIManager.Instance != null && currentWeapon != null)
        {
            int current = weaponAmmoList[currentWeaponIndex];
            UIManager.Instance.UpdateAmmo(current, GetFinalMaxAmmo());
        }
    }

    private void EquipWeapon(int index)
    {
        if (gunAudioSource != null)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }

        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
            currentWeapon.weaponParticle.gameObject.SetActive(false);
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponModel != null)
            {
                if (i == index)
                {
                    weapons[i].weaponModel.SetActive(true); // 선택된 것만 켜기
                }
                else
                {
                    weapons[i].weaponModel.SetActive(false); // 나머지는 끄기
                }
            }
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];

        if (currentWeapon.muzzlePoint != null)
        {
            currentMuzzlePoint = currentWeapon.muzzlePoint;
        }
        else
        {
            Debug.LogError($"{currentWeapon.weaponName}에 Muzzle Point가 연결되지 않았습니다!");
            currentMuzzlePoint = transform; // 비상시 내 위치 사용
        }

        lastFireTime = -currentWeapon.fireRate;

        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop();
        }

        //if (UIManager.Instance != null)
        //{
        //    UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
        //    UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        //    UIManager.Instance.ShowReloading(false);
        //}

        RefreshUI(); // [수정] UI 갱신 함수 호출로 변경

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        Debug.Log($"[실행 중] 현재 이 코드는 '{gameObject.name}' 오브젝트에서 실행되고 있습니다.");

        if (playerController == null)
        {
            Debug.LogError($"🚨 [검거 완료] 범인은 바로 '{gameObject.name}' 입니다! 이 오브젝트에 붙은 GunController를 삭제하세요!");
            return; // 더 이상 실행하지 않고 멈춤
        }

        if (GameManager.Instance != null && (GameManager.Instance.isUpgradeMenuOpen || GameManager.Instance.isPaused)) return;
        //if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!playerController.hasGun || isReloading || isSwitching) return;

        if (weaponAmmoList[currentWeaponIndex] <= 0)
        {
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
        while (isHoldingTrigger && weaponAmmoList[currentWeaponIndex] > 0 && !isReloading)
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

    private void PlayMuzzleFlash()
    {
        if (!currentWeapon.useMuzzleFlash) return;
        if (string.IsNullOrEmpty(currentWeapon.muzzleFlashTag)) return;

        // [수정] 회전값 보정: 총구 회전값 * 90도 회전 (Y축 기준)
        // 만약 반대로 나가면 -90 으로 바꿔보세요.
        Quaternion fixRotation = currentMuzzlePoint.rotation * Quaternion.Euler(0, -90, 0);

        // 수정된 회전값(fixRotation)으로 소환
        GameObject flash = PoolManager.Instance.SpawnFromPool(
            currentWeapon.muzzleFlashTag,
            currentMuzzlePoint.position,
            fixRotation
        );

        if (flash != null)
        {
            StartCoroutine(ReturnMuzzleFlash(flash, 0.1f));
        }
    }

    private IEnumerator ReturnMuzzleFlash(GameObject flashObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (flashObj.activeInHierarchy)
        {
            PoolManager.Instance.ReturnToPool(currentWeapon.muzzleFlashTag, flashObj);
        }
    }

    private void Shoot()
    {
        // 1. 플레이어 상태 체크
        if (playerController != null && playerController.isDead) return;

        weaponAmmoList[currentWeaponIndex]--;

        RefreshUI();
        PlayMuzzleFlash();

        // =================================================================
        // ★ [핵심 수정] 발사 방향(Direction) 계산 로직 분리
        // =================================================================
        Vector3 baseDirection = Vector3.forward;

        // [CASE 1] 패드 사용 중: 총구가 바라보는 방향 그대로 발사
        if (GameManager.Instance != null && GameManager.Instance.isUsingGamepad)
        {
            // PlayerController가 이미 오른쪽 스틱 입력에 따라 몸을 돌려놓은 상태입니다.
            // 따라서 복잡한 계산 없이 그냥 총구의 앞방향(forward)을 쓰면 됩니다.
            baseDirection = currentMuzzlePoint.forward;
        }
        // [CASE 2] 마우스 사용 중: 기존 로직 (Raycast로 커서 위치 계산)
        else
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane gunPlane = new Plane(Vector3.up, currentMuzzlePoint.position);
            float distance;
            Vector3 targetPoint = Vector3.zero;

            if (gunPlane.Raycast(ray, out distance))
            {
                targetPoint = ray.GetPoint(distance);
            }

            float distanceToMouse = Vector3.Distance(transform.position, targetPoint);
            float deadZoneRadius = 3.0f; // 마우스가 너무 가까우면 정면 발사

            if (distanceToMouse < deadZoneRadius)
            {
                baseDirection = currentMuzzlePoint.forward;
            }
            else
            {
                baseDirection = (targetPoint - currentMuzzlePoint.position).normalized;
            }
        }

        // [공통 보정] 높이 오차 제거 (땅으로 박히지 않게)
        baseDirection.y = 0;
        baseDirection.Normalize();
        // =================================================================

        // --- 무기 타입별 로직 분기 (기존 유지) ---
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
                case WeaponType.FlameThrower:
                    FireFlameThrower(baseDirection);
                    break;
                case WeaponType.Rifle:
                default:
                    FireRaycast(baseDirection);
                    if (currentWeapon.type == WeaponType.Rifle && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.Instance.Rifle, 0.1f);
                    }
                    break;
            }
        }

        if (currentWeapon.ejectShell) SpawnShell();

        if (weaponAmmoList[currentWeaponIndex] <= 0)
        {
            HandleWeaponDepleted();
        }
    }

    // [기존] 발사체 발사 로직 분리
    private void FireProjectile(Vector3 direction)
    {
        Quaternion fireRotation = Quaternion.LookRotation(direction);
        GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, currentMuzzlePoint.position, fireRotation);

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
        if (SoundManager.Instance != null)
        {
            // SoundManager에 Shotgun 클립이 없다면 Rifle 등 다른 것으로 대체
            SoundManager.Instance.PlaySFX(SoundManager.Instance.shotGun, 0.2f);
        }

        for (int i = 0; i < currentWeapon.pellets; i++)
        {
            // 부채꼴 범위 내 랜덤 각도 생성
            float randomAngle = Random.Range(-currentWeapon.spreadAngle / 2f, currentWeapon.spreadAngle / 2f);

            // Y축 기준 회전
            Quaternion spreadRotation = Quaternion.Euler(0, randomAngle, 0);

            // 최종 방향 계산
            Vector3 pelletDirection = spreadRotation * baseDirection;

            // 수정된 FireRaycast를 재사용하여 발사 처리
            FireRaycast(pelletDirection);
        }
    }

    // [신규] 저격총 관통 발사 로직
    private void FireSniper(Vector3 direction)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.sniperShot, 0.3f);
        }

        // [핵심] 발사 시작점을 뒤로 당김 (근접 버그 해결)
        Vector3 fireOrigin = currentMuzzlePoint.position - (direction * 1.5f);
        float checkRange = currentWeapon.range + 1.5f;

        // 경로상의 모든 물체 검출
        RaycastHit[] hits = Physics.RaycastAll(fireOrigin, direction, checkRange);

        // 거리순 정렬 (가까운 순서대로)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int hitCount = 0;
        // 시각적 끝점은 기본적으로 최대 사거리
        Vector3 finalEndPoint = currentMuzzlePoint.position + (direction * currentWeapon.range);

        foreach (RaycastHit hit in hits)
        {
            // A. 나 자신(플레이어) 무시
            if (hit.collider.gameObject == gameObject) continue;

            // B. 트리거 무시 (아이템 등)
            if (hit.collider.isTrigger) continue;

            // 유효한 충돌 지점 갱신 (벽이나 적)
            // 너무 가까워서(0.5f 이내) 총구보다 뒤면, 총구 위치로 보정 (시각적 어색함 방지)
            if (hit.distance < 0.5f) finalEndPoint = currentMuzzlePoint.position;
            else finalEndPoint = hit.point;

            // 벽(Environment)에 맞으면 관통 멈춤
            if (!hit.collider.CompareTag("Enemy"))
            {
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
                break; // 벽에 막힘
            }

            // 적중 처리
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
                        break;
                    }
                }
            }
        }

        // 저격총 트레이서 (총구 위치 ~ 최종 충돌 지점)
        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(currentMuzzlePoint.position, finalEndPoint, 0.05f, currentWeapon.tracerColor, 0.1f);
        }
    }

    // [기존] 일반 단발(라이플) 발사 로직
    private void FireRaycast(Vector3 direction)
    {
        // [핵심] 발사 시작점을 뒤로 당김
        Vector3 fireOrigin = currentMuzzlePoint.position - (direction * 1.5f);
        float checkRange = currentWeapon.range + 1.5f;

        // RaycastAll로 변경하여 나 자신을 통과해서 검사
        RaycastHit[] hits = Physics.RaycastAll(fireOrigin, direction, checkRange);

        // 거리순 정렬
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 endPoint = currentMuzzlePoint.position + (direction * currentWeapon.range);

        foreach (RaycastHit hit in hits)
        {
            // A. 나 자신 무시
            if (hit.collider.gameObject == gameObject) continue;
            // B. 트리거 무시
            if (hit.collider.isTrigger) continue;

            // 유효 충돌 발생
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
                // 벽/바닥 적중 이펙트
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
            }

            // 라이플은 관통 안 하므로 첫 유효타에서 종료
            break;
        }

        // 트레이서 그리기 (시작점은 항상 실제 총구 위치)
        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(currentMuzzlePoint.position, endPoint, 0.05f, currentWeapon.tracerColor, 0.05f);
        }
    }

    private void SpawnShell()
    {
        // 1. 현재 무기에 탄피 배출구가 설정되어 있는지 확인
        if (currentWeapon.shellEjectPoint == null) return;

        // 2. 탄피 생성 (위치는 무기별 shellEjectPoint 사용)
        GameObject shell = PoolManager.Instance.SpawnFromPool("Shell", currentWeapon.shellEjectPoint.position, currentWeapon.shellEjectPoint.rotation);

        if (shell != null)
        {
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // 배출구의 오른쪽(Right) + 위쪽(Up) 방향으로 튕겨 나감
                Vector3 ejectDir = currentWeapon.shellEjectPoint.right + Vector3.up * 0.5f;

                // 랜덤성 추가 (더 자연스럽게)
                ejectDir += Random.insideUnitSphere * 0.2f;

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

    //private IEnumerator ReloadAndSwitch()
    //{
    //    if (isReloading) yield break;
    //    isReloading = true;

    //    if (shootCoroutine != null) StopCoroutine(shootCoroutine);
    //    if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

    //    if (gunAudioSource.isPlaying && currentWeapon.type == WeaponType.FlameThrower)
    //    {
    //        gunAudioSource.Stop();
    //        gunAudioSource.loop = false;
    //    }

    //    if (UIManager.Instance != null)
    //    {
    //        UIManager.Instance.ShowReloading(true);
    //    }
    //    if (SoundManager.Instance != null)
    //        SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

    //    yield return new WaitForSeconds(reloadTime);

    //    int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
    //    EquipWeapon(nextIndex);

    //    isReloading = false;
    //}

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

    // [신규] 무기 교체 시도 (잠겨있거나 탄약 없으면 실패)
    private void TrySwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count) return;
        if (!isWeaponUnlocked[index]) return; // 잠겨있음
        if (weaponAmmoList[index] <= 0) return; // 탄약 없음
        if (index == currentWeaponIndex) return;

        EquipWeapon(index);
    }

    // [신규] 탄약 소진 시 다음 무기 해금 및 교체 로직
    // [수정] 탄약 소진 시 로직 (순서대로 해금 및 즉시 교체)
    private void HandleWeaponDepleted()
    {
        // 1. 다 쓴 무기 정리 (이펙트, 소리 끄기)
        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }
        if (gunAudioSource != null)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }
        isHoldingTrigger = false;
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        Debug.Log($"{currentWeapon.weaponName} 탄약 소진! 무기를 잠급니다.");

        // 2. 현재 무기 잠금 (확실하게 잠금)
        isWeaponUnlocked[currentWeaponIndex] = false;

        // 3. [핵심] 다음 해금할 무기 가져오기
        // nextUnlockIndex는 Start()에서 이미 2로 설정되어 있고, 
        // 무기가 바뀔 때마다 계속 다음 순번을 가리키고 있습니다.
        int unlockTargetIndex = nextUnlockIndex;

        // 방어 코드: 만약 해금하려는 게 이미 열려있다면(꼬임 방지), 
        // 닫혀있는 걸 찾을 때까지 뒤로 넘어감
        int safetyCount = 0;
        while (isWeaponUnlocked[unlockTargetIndex] && safetyCount < weapons.Count)
        {
            unlockTargetIndex = (unlockTargetIndex + 1) % weapons.Count;
            safetyCount++;
        }

        // 4. 새 무기 해금 및 탄약 충전
        isWeaponUnlocked[unlockTargetIndex] = true;
        weaponAmmoList[unlockTargetIndex] = GetFinalMaxAmmo(weapons[unlockTargetIndex]);
        Debug.Log($"새로운 무기 해제: {weapons[unlockTargetIndex].weaponName}");

        // 5. [중요] 다음 해금 순서 미리 갱신해두기
        // 이번에 unlockTargetIndex를 열었으니, 그 다음 번호부터 검사해서 잠긴 걸 찾음
        int tempNextIndex = (unlockTargetIndex + 1) % weapons.Count;
        safetyCount = 0;
        // 잠겨있는 무기가 나올 때까지 계속 다음으로 넘김
        while (isWeaponUnlocked[tempNextIndex] && safetyCount < weapons.Count)
        {
            tempNextIndex = (tempNextIndex + 1) % weapons.Count;
            safetyCount++;
        }
        nextUnlockIndex = tempNextIndex; // 찾은 값을 저장

        // 6. [해결책] "새로 해금된 무기"로 즉시 교체!
        // 예전에는 '사용 가능한 아무거나'를 찾았지만, 이제는 unlockTargetIndex로 바로 바꿉니다.
        StartCoroutine(AutoSwitchRoutine(unlockTargetIndex));
    }

    // [신규] 자동 교체 딜레이
    private IEnumerator AutoSwitchRoutine(int targetIndex)
    {
        isSwitching = true;

        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        isHoldingTrigger = false;

        if (playerController != null)
        {
            playerController.PlayWeaponChangeAnim();
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(true);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

        yield return new WaitForSeconds(2f); // 교체 시간 (reloadTime보다 짧게)

        EquipWeapon(targetIndex);

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(false);

        isSwitching = false;
    }

    // [신규] UI 갱신 헬퍼
    private void RefreshUI()
    {
        // 1. UIManager가 없으면 중단
        if (UIManager.Instance == null) return;

        // 2. [핵심 수정] 중요 데이터들이 초기화되었는지 확인 (하나라도 없으면 중단)
        // 이 부분이 없어서 에러가 났던 겁니다.
        if (weaponAmmoList == null || weapons == null || currentWeapon == null || isWeaponUnlocked == null)
        {
            // 아직 데이터가 로드되지 않음 (Start/Awake 순서 문제 방지)
            return;
        }

        // 3. 인덱스 범위 초과 방지 (안전장치)
        if (currentWeaponIndex < 0 || currentWeaponIndex >= weaponAmmoList.Length) return;

        // -------------------------------------------------------
        // 실제 로직 실행
        // -------------------------------------------------------
        int current = weaponAmmoList[currentWeaponIndex];
        int max = GetFinalMaxAmmo(weapons[currentWeaponIndex]);

        UIManager.Instance.UpdateAmmo(current, max);
        UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);

        // 슬롯 UI 갱신
        UIManager.Instance.UpdateWeaponSlots(isWeaponUnlocked, currentWeaponIndex, nextUnlockIndex);
    }

    // [신규] 인자 받는 GetFinalMaxAmmo 오버로딩
    private int GetFinalMaxAmmo(WeaponStats weapon)
    {
        // 1. 현재 글로벌 배율 가져오기
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalAmmoMultiplier : 1.0f;

        // 2. 현재 몇 강인지 계산 (0.2 단위로 증가한다고 가정)
        // 예: 1.2배 -> 1강, 1.4배 -> 2강, 1.6배 -> 3강...
        // Mathf.RoundToInt를 써서 소수점 오차를 깔끔하게 없앱니다.
        int upgradeLevel = Mathf.RoundToInt((multiplier - 1.0f) / 0.2f);

        // 3. 무기 종류에 따라 계산법 다르게 적용
        if (weapon.type == WeaponType.Bazooka ||
            weapon.type == WeaponType.Sniper ||
            weapon.type == WeaponType.Shotgun)
        {
            // [방식 A] 덧셈 방식: 무조건 1강당 1발씩 추가 (샷건은 1발씩 늘어나는 게 밸런스상 좋을 수 있음)
            // 바주카(1발) -> 1강(2발), 2강(3발), 3강(4발)...
            // 스나이퍼(3발) -> 1강(4발), 2강(5발)...
            return weapon.maxAmmo + upgradeLevel;
        }
        else
        {
            // [방식 B] 곱셈 방식: 라이플, 화염방사기는 %로 늘어나는 게 이득
            // 올림(Ceil)을 써서 최소 증가량 보장
            return Mathf.CeilToInt(weapon.maxAmmo * multiplier);
        }
    }

    // [신규] 모든 무기 모델을 강제로 끄는 함수 (맨손 상태)
    public void HideAllWeapons()
    {
        if (weapons == null) return;

        foreach (var weapon in weapons)
        {
            if (weapon.weaponModel != null)
            {
                weapon.weaponModel.SetActive(false);
            }
            if (weapon.weaponParticle != null)
            {
                weapon.weaponParticle.gameObject.SetActive(false);
            }
        }

        // 현재 무기 정보도 초기화 (안 하면 쏠 수 있음)
        //currentWeaponIndex = -1; // 인덱스는 놔두더라도
        currentWeapon = null;    // 무기 데이터는 비워야 안전함
    }

    // [신규] 외부(PlayerController)에서 총 먹었을 때 호출할 함수
    public void EquipStartingWeapon()
    {
        // 0번(기본 무기) 장착
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    // [신규] 다음 무기로 교체 (휠 올림)
    private void SwitchToNextWeapon()
    {
        int nextIndex = currentWeaponIndex;
        // 최대 무기 개수만큼 반복하며 찾음
        for (int i = 0; i < weapons.Count; i++)
        {
            nextIndex = (nextIndex + 1) % weapons.Count; // 인덱스 증가 및 순환 (0->1->2->0)

            // 해금되었고 & 탄약이 있고 & 현재 무기가 아니라면 교체
            if (isWeaponUnlocked[nextIndex] && weaponAmmoList[nextIndex] > 0 && nextIndex != currentWeaponIndex)
            {
                TrySwitchWeapon(nextIndex);
                return;
            }
        }
    }

    // [신규] 이전 무기로 교체 (휠 내림)
    private void SwitchToPreviousWeapon()
    {
        int prevIndex = currentWeaponIndex;
        // 최대 무기 개수만큼 반복하며 찾음
        for (int i = 0; i < weapons.Count; i++)
        {
            prevIndex--;
            if (prevIndex < 0) prevIndex = weapons.Count - 1; // 인덱스 감소 및 순환 (0->2->1->0)

            // 해금되었고 & 탄약이 있고 & 현재 무기가 아니라면 교체
            if (isWeaponUnlocked[prevIndex] && weaponAmmoList[prevIndex] > 0 && prevIndex != currentWeaponIndex)
            {
                TrySwitchWeapon(prevIndex);
                return;
            }
        }
    }

    // [신규] 화염방사기 전용 발사 로직 (두꺼운 판정 + 근접 보정)
    private void FireFlameThrower(Vector3 direction)
    {
        // 중복 타격 방지를 위한 목록 (HashSet은 똑같은 게 들어오면 알아서 무시함)
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        // 1. [초근접] 내 주변 2m 긁어오기 (OverlapSphere)
        // 이걸 추가해야 겹쳐있는 좀비도 감지됩니다.
        Collider[] closeColliders = Physics.OverlapSphere(currentMuzzlePoint.position, 0.3f);
        foreach (var col in closeColliders)
        {
            hitTargets.Add(col.gameObject);
        }

        // 2. [원거리] 기존 화염방사 발사 (SphereCast)
        Vector3 fireOrigin = currentMuzzlePoint.position - (direction * 0.5f);
        float flameRadius = 1.5f;
        RaycastHit[] rayHits = Physics.SphereCastAll(fireOrigin, flameRadius, direction, currentWeapon.range);

        foreach (var hit in rayHits)
        {
            hitTargets.Add(hit.collider.gameObject);
        }

        // 3. 통합된 타겟 처리
        foreach (GameObject target in hitTargets)
        {
            if (target == gameObject) continue; // 나 자신
            if (target.CompareTag("Enemy"))
            {
                // 거리 계산 (콜라이더의 중심점 기준)
                float distToTarget = Vector3.Distance(currentMuzzlePoint.position, target.transform.position);

                // ★ 거리 1.5m 이상일 때만 벽 검사 (가까우면 무조건 통과)
                if (distToTarget > 1.5f)
                {
                    Vector3 dirToTarget = (target.transform.position - currentMuzzlePoint.position).normalized;

                    // 벽 체크
                    if (Physics.Raycast(currentMuzzlePoint.position, dirToTarget, distToTarget, obstacleLayer))
                    {
                        continue; // 벽에 막힘
                    }
                }

                // 데미지 주기
                ZombieAI zombie = target.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(GetFinalDamage());
                }
            }
        }
    }

    // [디버그용] 범위 시각화
    private void OnDrawGizmos()
    {
        if (currentWeapon == null || currentMuzzlePoint == null) return;

        // 화염방사기일 때만 그림
        if (currentWeapon.type == WeaponType.FlameThrower)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f); // 주황색 반투명

            // 로직과 동일한 시작점 계산 (총구 뒤 0.5m)
            // 주의: direction을 알 수 없으므로 총구의 정면(forward)을 기준으로 그림
            Vector3 fireOrigin = currentMuzzlePoint.position - (currentMuzzlePoint.forward * 0.5f);
            float flameRadius = 1.5f; // 로직에 쓴 반지름과 맞춰주세요

            // 1. 시작점 구체
            Gizmos.DrawSphere(fireOrigin, flameRadius);

            // 2. 끝점 구체
            Vector3 endPosition = fireOrigin + (currentMuzzlePoint.forward * currentWeapon.range);
            Gizmos.DrawSphere(endPosition, flameRadius);

            // 3. 연결선 (원기둥 느낌을 위해)
            Gizmos.DrawLine(fireOrigin + Vector3.up * flameRadius, endPosition + Vector3.up * flameRadius);
            Gizmos.DrawLine(fireOrigin - Vector3.up * flameRadius, endPosition - Vector3.up * flameRadius);
            Gizmos.DrawLine(fireOrigin + Vector3.right * flameRadius, endPosition + Vector3.right * flameRadius);
            Gizmos.DrawLine(fireOrigin - Vector3.right * flameRadius, endPosition - Vector3.right * flameRadius);
        }
        else if (currentWeapon.type == WeaponType.Rifle || currentWeapon.type == WeaponType.Sniper)
        {
            // [참고] 라이플/스나이퍼는 얇은 선으로 표시
            Gizmos.color = Color.red;
            Vector3 fireOrigin = currentMuzzlePoint.position - (currentMuzzlePoint.forward * 0.5f);
            Vector3 endPosition = fireOrigin + (currentMuzzlePoint.forward * currentWeapon.range);
            Gizmos.DrawLine(fireOrigin, endPosition);
        }
    }
}