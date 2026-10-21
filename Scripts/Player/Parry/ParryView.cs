using UnityEngine;

/// <summary>
/// 패링 활성 자세와 성공 효과를 재생한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ParryView : MonoBehaviour
{
    [Tooltip("연출을 연결할 Player의 PlayerParry")]
    [SerializeField] private PlayerParry _playerParry;

    [Space(10)]
    [Header("패링 자세")]
    [Tooltip("활성 기간 동안 순서대로 반복 재생할 FX_Parry_Ready 프레임")]
    [SerializeField] private Sprite[] _readyFrames;

    [Tooltip("자세 루프의 초당 프레임 수. 패링 판정 시간과 무관하게 한 사이클 속도만 결정한다")]
    [Min(0.01f)]
    [SerializeField] private float _readyFrameRate = 16f;

    [Space(10)]
    [Header("패링 성공")]
    [Tooltip("성공 시 1회 재생할 FX_Parry_Success 정의. effectViewPrefab은 PoolManager에 등록되어 있어야 한다")]
    [SerializeField] private EffectDefinitionSO _successEffect;

    [Tooltip("Player 중심에서 공격자 쪽으로 성공 이펙트를 밀어내는 거리(units). 유입 방향이 없으면 적용하지 않는다")]
    [Min(0f)]
    [SerializeField] private float _successEffectOffset = 0.5f;

    private SpriteRenderer _spriteRenderer;
    private bool _isPlayingReady;
    private int _readyFrameIndex;
    private float _readyFrameTimer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        HideReady();
    }

    private void OnEnable()
    {
        if (_playerParry == null)
        {
            Logger.LogWarning($"{nameof(ParryView)}에 {nameof(PlayerParry)}가 연결되지 않아 패링 연출을 재생할 수 없습니다.", this);
            return;
        }

        _playerParry.Started += PlayerParry_Started;
        _playerParry.Succeeded += PlayerParry_Succeeded;
        _playerParry.Ended += PlayerParry_Ended;
    }

    private void OnDisable()
    {
        if (_playerParry != null)
        {
            _playerParry.Started -= PlayerParry_Started;
            _playerParry.Succeeded -= PlayerParry_Succeeded;
            _playerParry.Ended -= PlayerParry_Ended;
        }

        HideReady();
    }

    private void Update()
    {
        if (!_isPlayingReady) return;

        float frameDuration = 1f / _readyFrameRate;
        _readyFrameTimer += Time.deltaTime;

        // 긴 프레임에서도 경과 시간만큼 진행해 자세 루프의 재생 속도를 유지한다.
        while (_readyFrameTimer >= frameDuration)
        {
            _readyFrameTimer -= frameDuration;
            _readyFrameIndex = (_readyFrameIndex + 1) % _readyFrames.Length;
        }

        _spriteRenderer.sprite = _readyFrames[_readyFrameIndex];
    }

    // 패링 시작마다 첫 프레임부터 자세 루프를 재생한다.
    private void PlayerParry_Started()
    {
        if (_readyFrames == null || _readyFrames.Length == 0 || _readyFrameRate <= 0f)
        {
            return;
        }

        _isPlayingReady = true;
        _readyFrameIndex = 0;
        _readyFrameTimer = 0f;
        _spriteRenderer.sprite = _readyFrames[0];
        _spriteRenderer.enabled = true;
    }

    // 성공 효과만 공격자 쪽에 재생하고 자세 정리는 종료 이벤트에 맡긴다.
    private void PlayerParry_Succeeded(Vector2 incomingDirection)
    {
        if (_successEffect == null || _successEffect.effectViewPrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(
            _successEffect.effectViewPrefab, ResolveSuccessEffectPosition(incomingDirection), Quaternion.identity);

        EffectView effectView = rentedComponent as EffectView;

        if (effectView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return;
        }

        effectView.Play(_successEffect);
    }

    // 유입 방향이 없으면 임의의 방향을 만들지 않고 플레이어 중심을 사용한다.
    private Vector3 ResolveSuccessEffectPosition(Vector2 incomingDirection)
    {
        Vector3 playerPosition = _playerParry.transform.position;

        if (incomingDirection == Vector2.zero || _successEffectOffset <= 0f)
        {
            return playerPosition;
        }

        return playerPosition - (Vector3)(incomingDirection.normalized * _successEffectOffset);
    }

    private void PlayerParry_Ended()
    {
        HideReady();
    }

    // 모든 종료 경로에서 재생 상태와 Sprite를 함께 비운다.
    private void HideReady()
    {
        _isPlayingReady = false;
        _readyFrameIndex = 0;
        _readyFrameTimer = 0f;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
            _spriteRenderer.enabled = false;
        }
    }
}
