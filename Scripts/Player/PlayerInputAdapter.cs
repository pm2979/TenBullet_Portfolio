using UnityEngine;

/// <summary>
/// 화면 조준 입력을 월드 좌표로 변환해 조준 계산에 전달한다.
/// </summary>
[RequireComponent(typeof(CombatInputReader))]
[DisallowMultipleComponent]
public class PlayerInputAdapter : MonoBehaviour
{
    private CombatInputReader _combatInputReader;
    private readonly AimResolver _aimResolver = new AimResolver();
    private Camera _mainCamera;

    private void Awake()
    {
        _combatInputReader = GetComponent<CombatInputReader>();
    }

    /// <summary>
    /// 현재 화면 조준 위치를 월드 좌표로 변환해 무기와 플레이어의 조준 결과를 계산한다.
    /// </summary>
    public AimResult ResolveAim(Vector2 playerPosition, Vector2 weaponShootPosition)
    {
        TryGetAimWorldPosition(playerPosition, out Vector2 targetWorldPosition);

        return _aimResolver.Resolve(playerPosition, weaponShootPosition, targetWorldPosition);
    }

    /// <summary>
    /// 화면 범위로 제한한 조준 위치를 월드 좌표로 변환한다.
    /// 활성 Main Camera가 없으면 대체 위치를 반환하고 false를 반환한다.
    /// </summary>
    public bool TryGetAimWorldPosition(Vector2 fallbackWorldPosition, out Vector2 aimWorldPosition)
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled) _mainCamera = Camera.main;

        if (_mainCamera == null)
        {
            aimWorldPosition = fallbackWorldPosition;
            return false;
        }

        Vector3 clampedScreenPosition = _combatInputReader.AimScreenPosition;
        clampedScreenPosition.x = Mathf.Clamp(clampedScreenPosition.x, 0f, Screen.width);
        clampedScreenPosition.y = Mathf.Clamp(clampedScreenPosition.y, 0f, Screen.height);

        aimWorldPosition = _mainCamera.ScreenToWorldPoint(clampedScreenPosition);
        return true;
    }
}
