using UnityEngine;

// 선택한 Implant의 장착 수명과 무기 적중 효과 실행을 관리하는 단일 슬롯
public sealed class WeaponImplantLoadout : MonoBehaviour
{
    [Tooltip("무기 후보 목록에 포함된 경우 이번 런에 장착할 Implant")]
    [SerializeField] private ImplantDataSO _selectedImplant;
    [Tooltip("PoolManager에 등록된 체력 Pickup Prefab")]
    [SerializeField] private GameObject _healthPickupPrefab;
    [Tooltip("체력 Pickup 생성 위치에서 제외할 장애물 Layer")]
    [SerializeField] private LayerMask _pickupObstacleLayerMask;

    private Weapon _equippedWeapon;
    private Health _ownerHealth;
    private ImplantDataSO _equippedImplant;
    private ImplantRuntime _implantRuntime;
    private HealthPickupSpawner _healthPickupSpawner;

    // 무기 후보와 효과 의존성을 검증한 뒤 Implant 장착 및 적중 구독
    public void Equip(Weapon weapon, Health ownerHealth)
    {
        if (_selectedImplant == null)
        {
            return;
        }

        if (_equippedWeapon != null)
        {
            if (_equippedWeapon == weapon && _equippedImplant == _selectedImplant)
            {
                return;
            }

            Logger.LogWarning("WeaponImplantLoadout: 이미 Implant가 장착된 상태라 다른 Weapon/Implant 장착 요청을 거부합니다.", this);
            return;
        }

        if (weapon == null || weapon.weaponDetails == null || !IsCandidate(weapon.weaponDetails.implantCandidates, _selectedImplant))
        {
            Logger.LogWarning($"WeaponImplantLoadout: '{_selectedImplant.name}'은(는) 현재 Weapon의 Implant 후보에 없어 장착하지 않습니다.", this);
            return;
        }

        if (_selectedImplant.EffectKind == ImplantEffectKind.InstantHeal && ownerHealth == null)
        {
            Logger.LogWarning($"WeaponImplantLoadout: '{_selectedImplant.name}'은(는) InstantHeal에 필요한 Health 참조가 없어 장착하지 않습니다.", this);
            return;
        }

        _equippedWeapon = weapon;
        _ownerHealth = ownerHealth;
        _equippedImplant = _selectedImplant;
        _implantRuntime = new ImplantRuntime(_equippedImplant);

        if (_equippedImplant.EffectKind == ImplantEffectKind.SpawnHealthPickup)
        {
            _healthPickupSpawner = new HealthPickupSpawner(
                _healthPickupPrefab, _equippedImplant.HealAmount, _equippedImplant.PickupLifetime,
                _equippedImplant.MaxActivePickup, _pickupObstacleLayerMask);
        }

        _equippedWeapon.Hit += HandleHit;
    }

    // 적중 구독과 발동 상태를 정리하고 생성된 Pickup 전량 반환
    public void Unequip()
    {
        if (_equippedWeapon != null)
        {
            _equippedWeapon.Hit -= HandleHit;
        }

        _implantRuntime?.Clear();
        _healthPickupSpawner?.ReturnAll();
        _equippedWeapon = null;
        _ownerHealth = null;
        _equippedImplant = null;
        _implantRuntime = null;
        _healthPickupSpawner = null;
    }

    private void OnDisable()
    {
        Unequip();
    }

    private static bool IsCandidate(ImplantDataSO[] candidates, ImplantDataSO implantData)
    {
        if (candidates == null)
        {
            return false;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == implantData)
            {
                return true;
            }
        }

        return false;
    }

    // 장착 해제 후 콜백을 차단하고 최초 발동이 승인된 효과만 실행
    private void HandleHit(WeaponHitEvent hitEvent)
    {
        if (_implantRuntime == null || !_implantRuntime.TryActivate(hitEvent))
        {
            return;
        }

        switch (_equippedImplant.EffectKind)
        {
            case ImplantEffectKind.InstantHeal:
                ApplyInstantHeal();
                break;
            case ImplantEffectKind.SpawnHealthPickup:
                _healthPickupSpawner?.TrySpawn(hitEvent.CombatResult.HitPosition);
                break;
        }
    }

    // 유효한 소유자에게 Implant 설정량만큼 즉시 회복 요청
    private void ApplyInstantHeal()
    {
        if (_ownerHealth == null)
        {
            return;
        }

        _ownerHealth.TryRestoreHealth(new HealInfo(_equippedImplant.HealAmount));
    }
}
