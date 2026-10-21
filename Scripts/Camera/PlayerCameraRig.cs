using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Player를 바인딩하고 조준 오프셋이 반영된 추적 기준점을 갱신한다.
/// Cinemachine은 이 기준점의 실제 카메라 추적을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCameraRig : MonoBehaviour
{
    [Tooltip("vcam이 추적할 CameraFollowAnchor Transform")]
    [SerializeField] private Transform _followAnchor;
    [Tooltip("조준 오프셋과 Shake 수치를 가진 단일 CameraSettingsSO")]
    [SerializeField] private CameraSettingsSO _settings;
    [Tooltip("공간 전환 시 Warp 통지에 사용할 Cinemachine Camera")]
    [SerializeField] private CinemachineCamera _virtualCamera;

    private CameraAimOffset _aimOffset;
    private Player _player;
    private CombatInputReader _combatInputReader;
    private Camera _mainCamera;
    private CameraShakeDirector _shakeDirector;
    private bool _isPlayerBound;
    private bool _hasLoggedMissingMainCamera;

    private void Awake()
    {
        if (_followAnchor == null || _settings == null || _virtualCamera == null)
        {
            Logger.LogError(
                $"{nameof(PlayerCameraRig)}: {nameof(_followAnchor)}, {nameof(_settings)}, {nameof(_virtualCamera)} 중 하나 이상이 연결되지 않아 컴포넌트를 비활성화합니다.",
                this);
            enabled = false;
            return;
        }

        _aimOffset = CreateAimOffset();
        _shakeDirector = GetComponent<CameraShakeDirector>();
    }

    /// <summary>
    /// Player 생성 주체가 탐색 없이 바인딩할 Player를 직접 전달한다.
    /// 중복 호출은 기존 바인딩을 새 Player 기준으로 안전하게 교체한다.
    /// </summary>
    public void Initialize(Player player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        BindPlayer(player);
    }

    private void LateUpdate()
    {
        if (_isPlayerBound && _player == null)
        {
            ClearPlayerBinding();
        }

        if (!_isPlayerBound)
        {
            TryBindPlayer();
        }

        if (!_isPlayerBound)
        {
            return;
        }

        UpdateFollowAnchor();
    }

    /// <summary>
    /// Player를 순간이동한 직후 호출한다. Anchor·오프셋·Cinemachine 추적 상태·진행 중 Shake를 함께 정리한다.
    /// </summary>
    public void SnapToPlayer()
    {
        if (_player == null || _followAnchor == null)
        {
            return;
        }

        _aimOffset?.Reset();
        Vector3 previousAnchorPosition = _followAnchor.position;
        _followAnchor.position = _player.transform.position;
        _virtualCamera.OnTargetObjectWarped(_followAnchor, _followAnchor.position - previousAnchorPosition);
        _shakeDirector?.ClearActiveShake();
    }

    // Initialize가 명시적으로 호출되지 않은 테스트 Scene 등에서만 Player를 탐색한다.
    private void TryBindPlayer()
    {
        Player player = GameManager.HasInstance ? GameManager.Instance.GetPlayer() : null;

        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player != null)
        {
            BindPlayer(player);
        }
    }

    private void BindPlayer(Player player)
    {
        _player = player;
        _combatInputReader = player.GetComponent<CombatInputReader>();
        _isPlayerBound = true;
        _aimOffset = CreateAimOffset();
    }

    private void ClearPlayerBinding()
    {
        _player = null;
        _combatInputReader = null;
        _isPlayerBound = false;
        _aimOffset?.Reset();
    }

    private void UpdateFollowAnchor()
    {
        Vector3 playerPosition = _player.transform.position;

        if (!TryGetMainCamera(out Camera mainCamera) || _combatInputReader == null)
        {
            _aimOffset?.Reset();
            _followAnchor.position = playerPosition;
            return;
        }

        Vector2 aimScreenPosition = ClampToScreen(_combatInputReader.AimScreenPosition);
        Vector2 playerScreenPosition = GetRawScreenPosition(playerPosition, mainCamera);
        Vector2 aimWorldPosition = GetRawWorldPosition(aimScreenPosition, playerPosition.z, mainCamera);

        float worldScreenHeight = GetWorldScreenHeight(mainCamera, playerPosition.z);
        Vector2 aimDirection = aimWorldPosition - (Vector2)playerPosition;
        Vector2 aimOffset = _aimOffset.Update(
            playerScreenPosition,
            aimScreenPosition,
            Screen.height,
            worldScreenHeight,
            aimDirection,
            Time.deltaTime);

        _followAnchor.position = playerPosition + (Vector3)aimOffset;
    }

    private bool TryGetMainCamera(out Camera mainCamera)
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = Camera.main;
        }

        mainCamera = _mainCamera;

        if (mainCamera != null)
        {
            _hasLoggedMissingMainCamera = false;
            return true;
        }

        if (!_hasLoggedMissingMainCamera)
        {
            Logger.LogWarning($"{nameof(PlayerCameraRig)}: Main Camera를 찾지 못해 기본 추적만 유지합니다.", this);
            _hasLoggedMissingMainCamera = true;
        }

        return false;
    }

    private static Vector2 ClampToScreen(Vector2 screenPosition)
    {
        screenPosition.x = Mathf.Clamp(screenPosition.x, 0f, Screen.width);
        screenPosition.y = Mathf.Clamp(screenPosition.y, 0f, Screen.height);
        return screenPosition;
    }

    private static float GetWorldScreenHeight(Camera mainCamera, float targetZ)
    {
        float screenToCameraDistance = Mathf.Abs(targetZ - mainCamera.transform.position.z);
        Vector3 bottom = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, 0f, screenToCameraDistance));
        Vector3 top = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height, screenToCameraDistance));

        return Vector2.Distance(bottom, top);
    }

    // PlayerInputAdapter는 발사·약점 클릭에 최종 렌더링 카메라 좌표를 계속 사용한다.
    // 카메라 오프셋 계산만 vcam Raw 상태로 역변환해 Impulse 보정이 재유입되지 않게 한다.
    private Vector2 GetRawScreenPosition(Vector3 worldPosition, Camera mainCamera)
    {
        Quaternion rawOrientation = _virtualCamera.State.RawOrientation;
        Vector3 localPosition = Quaternion.Inverse(rawOrientation) * (worldPosition - _virtualCamera.State.RawPosition);
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        return new Vector2(
            (localPosition.x / halfWidth + 1f) * Screen.width * 0.5f,
            (localPosition.y / halfHeight + 1f) * Screen.height * 0.5f);
    }

    private Vector2 GetRawWorldPosition(Vector2 screenPosition, float targetZ, Camera mainCamera)
    {
        Quaternion rawOrientation = _virtualCamera.State.RawOrientation;
        Vector3 rawPosition = _virtualCamera.State.RawPosition;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 localOrigin = new Vector3(
            (screenPosition.x / Screen.width * 2f - 1f) * halfWidth,
            (screenPosition.y / Screen.height * 2f - 1f) * halfHeight,
            0f);
        Vector3 rayOrigin = rawPosition + rawOrientation * localOrigin;
        Vector3 rayDirection = rawOrientation * Vector3.forward;

        if (Mathf.Abs(rayDirection.z) < Mathf.Epsilon)
        {
            return _player.transform.position;
        }

        float distance = (targetZ - rayOrigin.z) / rayDirection.z;
        return rayOrigin + rayDirection * distance;
    }

    private CameraAimOffset CreateAimOffset()
    {
        return new CameraAimOffset(
            _settings.MaxAimOffsetScreenRatio,
            _settings.OffsetSaturationScreenRatio,
            _settings.OffsetSmoothTime);
    }
}
