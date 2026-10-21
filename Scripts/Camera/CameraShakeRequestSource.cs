using UnityEngine;

/// <summary>
/// Player의 전투 사건을 카메라 Shake 이벤트로 변환한다.
/// Impulse 발행과 우선순위 판단은 <see cref="CameraShakeDirector"/>에 위임한다.
/// </summary>
[RequireComponent(typeof(CameraShakeDirector))]
[DisallowMultipleComponent]
public sealed class CameraShakeRequestSource : MonoBehaviour
{
    private CameraShakeDirector _director;
    private Player _player;
    private Health _health;
    private FireWeapon _fireWeapon;
    private bool _isHealthSubscribed;
    private bool _isFireWeaponSubscribed;

    private void Awake()
    {
        _director = GetComponent<CameraShakeDirector>();

        if (_director == null)
        {
            Logger.LogError($"{nameof(CameraShakeRequestSource)}: {nameof(CameraShakeDirector)}를 찾지 못해 컴포넌트를 비활성화합니다.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (_player != null && !_player.gameObject.activeInHierarchy)
        {
            ClearPlayerBinding();
        }

        if (_player == null)
        {
            TryBindPlayer();
        }
    }

    private void OnDisable()
    {
        ClearPlayerBinding();
    }

    private void OnDestroy()
    {
        ClearPlayerBinding();
    }

    private void TryBindPlayer()
    {
        Player player = GameManager.HasInstance ? GameManager.Instance.GetPlayer() : null;

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player == null)
        {
            return;
        }

        _player = player;
        _health = player.health;
        _fireWeapon = player.fireWeapon;

        if (_health != null)
        {
            _health.Changed += HandleHealthChanged;
            _isHealthSubscribed = true;
        }

        if (_fireWeapon != null)
        {
            _fireWeapon.Fired += HandleWeaponFired;
            _isFireWeaponSubscribed = true;
        }
    }

    private void ClearPlayerBinding()
    {
        if (_isHealthSubscribed && _health != null)
        {
            _health.Changed -= HandleHealthChanged;
        }

        if (_isFireWeaponSubscribed && _fireWeapon != null)
        {
            _fireWeapon.Fired -= HandleWeaponFired;
        }

        _player = null;
        _health = null;
        _fireWeapon = null;
        _isHealthSubscribed = false;
        _isFireWeaponSubscribed = false;
    }

    private void HandleHealthChanged(HealthChange change)
    {
        if (change.Reason == HealthChangeReason.Damage && change.Delta < 0)
        {
            _director.RequestShake(CameraShakeEvent.PlayerDamaged);
        }
    }

    // 발사 성공(일반 발사·약점 직접 클릭 공통, 명중과 무관) 1회당 해당 무기의 Shake Profile을 요청한다
    // FireWeapon.Fired는 발사 성공 시 정확히 1회만 발행되므로
    // 중복 Shake가 발생하지 않는다.
    private void HandleWeaponFired(Weapon weapon)
    {
        _director.RequestShake(weapon.weaponDetails.FireShakeProfile);
    }
}
