using UnityEngine;

/// <summary>풀링되는 탄피의 비행·회전·착지·정지·퇴장 연출을 관리</summary>
// 충돌 없는 장식 객체이므로 Rigidbody2D 대신 지면 이동과 연출용 높이를 직접 적분
// 연출용 높이는 실제 Z축이 아닌 월드 Y 위치에 더하는 화면상 오프셋으로 표현
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BulletShellView : MonoBehaviour
{
    [Header("착지 연출")]
    [SerializeField, Range(0f, 1f), Tooltip("바닥 접촉 시 수직 속도에 곱하는 감쇠 계수")]
    private float _bounceDamping = 0.4f;
    [SerializeField, Min(0), Tooltip("바닥 접촉 후 허용할 튕김 횟수 (0이면 접촉 즉시 미끄러짐으로 전환)")]
    private int _maxBounceCount = 2;
    [SerializeField, Min(0f), Tooltip("튕김이 끝난 뒤 수평 속도에 적용할 초당 마찰 감속량")]
    private float _groundFriction = 6f;
    [SerializeField, Min(0f), Tooltip("이 값 미만이면 완전히 정지한 것으로 간주하는 수평 속도 크기")]
    private float _stopSpeedThreshold = 0.02f;
    [SerializeField, Range(0f, 1f),
     Tooltip("첫 바닥 접촉 시 지면 이동 속도에 곱하는 유지율 (0이면 완전 정지). " +
             "착지 지점에서 다시 멀어지지 않도록 바운스는 제자리에서만 진행하게 함")]
    private float _groundVelocityRetentionOnLanding;

    [Header("하이라이트")]
    [SerializeField, Tooltip("최고점 부근에서 즉시 스냅할 강조 색상 (Tween 아닌 즉시 대입)")]
    private Color _highlightColor = new(1f, 0.95f, 0.6f, 1f);
    [SerializeField, Min(1), Tooltip("강조 색상을 유지할 프레임 수 (1~2 권장)")]
    private int _highlightFrameCount = 1;

    [Header("정지 후 처리")]
    [SerializeField, Min(0f), Tooltip("완전 정지 후 사라지기 전 페이드아웃 시간 (0이면 즉시 반환)")]
    private float _fadeOutDuration = 0.2f;

    private SpriteRenderer _renderer;
    private Color _baseColor;

    private Vector2 _groundPosition;
    private Vector2 _groundVelocity;
    private float _gravity;
    private float _height;
    private float _heightVelocity;
    private int _bounceCount;
    private bool _isGrounded;
    private bool _isStopped;

    private bool _highlightTriggered;
    private bool _highlightActive;
    private int _highlightFramesRemaining;

    private float _rotationStepDegrees;
    private float _rotationStepInterval;
    private float _rotationTimer;
    private float _rotationSign;
    private float _currentRotationDegrees;

    private float _lifetime;
    private float _stoppedElapsed;
    private bool _isFadingOut;
    private float _fadeElapsed;

    // 프레임 적분 오차를 제거하기 위한 최초 착지 위치 보정값
    private Vector2 _targetLandingPosition;
    private bool _hasLandedOnce;
    private SoundDataSO _landingSound;

    // 주 탄도와 독립적으로 최종 표시 위치에만 더하는 생성 직후 팝 오프셋
    private float _spawnPopHeight;
    private float _spawnPopDuration;
    private float _spawnPopElapsed;
    private float _spawnPopOffset;

    public int SortingOrder
    {
        get => _renderer.sortingOrder;
        set => _renderer.sortingOrder = value;
    }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _baseColor = _renderer.color;
    }

    /// <summary>이전 대여 상태를 초기화하고 전달받은 탄도·회전·수명 설정으로 탄피 연출을 시작</summary>
    public void Launch(
        Vector2 position, Vector2 velocity, float gravity, float initialHeightSpeed, Vector2 landingPosition,
        float rotationStepDegrees, float rotationStepInterval, bool clockwise, float lifetime,
        float spawnPopHeight = 0f, float spawnPopDuration = 0f, SoundDataSO landingSound = null)
    {
        ResetForPool();

        _groundPosition = position;
        _groundVelocity = velocity;
        _gravity = Mathf.Max(0f, gravity);
        _heightVelocity = initialHeightSpeed;
        _height = 0f;
        _targetLandingPosition = landingPosition;
        _landingSound = landingSound;

        _spawnPopHeight = Mathf.Max(0f, spawnPopHeight);
        _spawnPopDuration = Mathf.Max(0f, spawnPopDuration);

        _rotationStepDegrees = Mathf.Abs(rotationStepDegrees);
        _rotationStepInterval = Mathf.Max(0.001f, rotationStepInterval);
        // Unity 2D의 Z축 양의 회전과 반대인 시계 방향 요청을 부호 반전으로 변환
        _rotationSign = clockwise ? -1f : 1f;
        _lifetime = Mathf.Max(0f, lifetime);

        // 대여 시 자동 활성화하지 않는 PoolManager 계약에 따른 명시적 활성화
        gameObject.SetActive(true);

        ApplyTransform();
    }

    private void Update()
    {
        if (_isFadingOut)
        {
            UpdateFadeOut();
            return;
        }

        if (_isStopped)
        {
            _stoppedElapsed += Time.deltaTime;
            if (_stoppedElapsed >= _lifetime)
            {
                BeginFadeOutOrReturn();
            }
            return;
        }

        UpdateBallistics(Time.deltaTime);
        UpdateRotation(Time.deltaTime);
        UpdateHighlight();
        UpdateSpawnPop(Time.deltaTime);
        ApplyTransform();
    }

    // 공중 탄도와 착지 후 마찰을 갱신하고 정지 상태로 전환
    private void UpdateBallistics(float deltaTime)
    {
        if (!_isGrounded)
        {
            bool wasRising = _heightVelocity > 0f;
            _heightVelocity -= _gravity * deltaTime;
            _height += _heightVelocity * deltaTime;
            _groundPosition += _groundVelocity * deltaTime;

            if (wasRising && _heightVelocity <= 0f && !_highlightTriggered)
            {
                TriggerHighlight();
            }

            if (_height <= 0f)
            {
                _height = 0f;
                HandleLanding();
            }

            return;
        }

        _groundVelocity = Vector2.MoveTowards(_groundVelocity, Vector2.zero, _groundFriction * deltaTime);
        _groundPosition += _groundVelocity * deltaTime;

        if (_groundVelocity.sqrMagnitude <= _stopSpeedThreshold * _stopSpeedThreshold)
        {
            _groundVelocity = Vector2.zero;
            _isStopped = true;
        }
    }

    // 최초 착지 위치를 보정하고 설정된 횟수만큼 제자리 바운스 처리
    private void HandleLanding()
    {
        if (!_hasLandedOnce)
        {
            _hasLandedOnce = true;
            // 프레임 적분량과 무관하게 발사 시 계산한 목표 지점으로 스냅
            _groundPosition = _targetLandingPosition;
            // 첫 착지 이후 목표 지점에서 과도하게 멀어지는 현상 방지
            _groundVelocity *= _groundVelocityRetentionOnLanding;
            TryPlayLandingSound();
        }

        if (_bounceCount < _maxBounceCount && Mathf.Abs(_heightVelocity) > 0.01f)
        {
            _heightVelocity = -_heightVelocity * _bounceDamping;
            _bounceCount++;
            return;
        }

        _heightVelocity = 0f;
        _isGrounded = true;
    }

    // 최초 착지에 설정된 사운드를 AudioManager가 준비된 경우에만 재생
    private void TryPlayLandingSound()
    {
        if (_landingSound == null || !AudioManager.HasInstance)
        {
            return;
        }

        AudioManager.Instance.PlaySfx(_landingSound);
    }

    // 누적 시간마다 계단식 회전 각도를 한 단계 갱신
    private void UpdateRotation(float deltaTime)
    {
        if (_rotationStepDegrees <= 0f)
        {
            return;
        }

        _rotationTimer += deltaTime;
        if (_rotationTimer < _rotationStepInterval)
        {
            return;
        }

        _rotationTimer -= _rotationStepInterval;
        _currentRotationDegrees += _rotationStepDegrees * _rotationSign;
    }

    // 주 탄도를 바꾸지 않고 재생 구간 중앙에서 최고점에 도달하는 생성 팝 오프셋 계산
    private void UpdateSpawnPop(float deltaTime)
    {
        if (_spawnPopHeight <= 0f || _spawnPopDuration <= 0f || _spawnPopElapsed >= _spawnPopDuration)
        {
            _spawnPopOffset = 0f;
            return;
        }

        _spawnPopElapsed += deltaTime;
        float t = Mathf.Clamp01(_spawnPopElapsed / _spawnPopDuration);
        _spawnPopOffset = 4f * _spawnPopHeight * t * (1f - t);
    }

    // 최고점 강조를 지정된 프레임 수만큼 유지하도록 예약
    private void TriggerHighlight()
    {
        _highlightTriggered = true;
        _highlightActive = true;
        _highlightFramesRemaining = _highlightFrameCount;
    }

    // 마지막 강조 프레임이 즉시 지워지지 않도록 다음 갱신에서 기본 색상으로 복원
    private void UpdateHighlight()
    {
        if (_highlightFramesRemaining > 0)
        {
            _renderer.color = _highlightColor;
            _highlightFramesRemaining--;
            return;
        }

        if (_highlightActive)
        {
            _renderer.color = _baseColor;
            _highlightActive = false;
        }
    }

    // 설정된 퇴장 시간에 따라 즉시 반환 또는 페이드아웃 시작
    private void BeginFadeOutOrReturn()
    {
        if (_fadeOutDuration <= 0f)
        {
            ReturnToPool();
            return;
        }

        _isFadingOut = true;
        _fadeElapsed = 0f;
    }

    // 기본 알파에서 투명 상태까지 보간한 뒤 풀 반환 처리
    private void UpdateFadeOut()
    {
        _fadeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_fadeElapsed / _fadeOutDuration);

        Color color = _baseColor;
        color.a = Mathf.Lerp(_baseColor.a, 0f, t);
        _renderer.color = color;

        if (t >= 1f)
        {
            ReturnToPool();
        }
    }

    // 지면 위치에 탄도 높이와 생성 팝을 합산하고 누적 회전을 반영
    private void ApplyTransform()
    {
        transform.position = new Vector3(
            _groundPosition.x, _groundPosition.y + _height + _spawnPopOffset, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, _currentRotationDegrees);
    }

    // 정상 완료와 재대여 모두에서 사용할 수 있도록 모든 런타임 상태를 기본값으로 복원
    private void ResetForPool()
    {
        _groundVelocity = Vector2.zero;
        _gravity = 0f;
        _height = 0f;
        _heightVelocity = 0f;
        _bounceCount = 0;
        _isGrounded = false;
        _isStopped = false;

        _highlightTriggered = false;
        _highlightActive = false;
        _highlightFramesRemaining = 0;

        _rotationStepDegrees = 0f;
        _rotationTimer = 0f;
        _currentRotationDegrees = 0f;

        _lifetime = 0f;
        _stoppedElapsed = 0f;
        _isFadingOut = false;
        _fadeElapsed = 0f;

        _targetLandingPosition = Vector2.zero;
        _hasLandedOnce = false;
        _landingSound = null;

        _spawnPopHeight = 0f;
        _spawnPopDuration = 0f;
        _spawnPopElapsed = 0f;
        _spawnPopOffset = 0f;

        _renderer.color = _baseColor;
        transform.localScale = Vector3.one;
    }

    // 런타임 상태를 정리하고 PoolManager 소유 객체는 반환하며 비소유 객체는 비활성화
    private void ReturnToPool()
    {
        ResetForPool();

        if (PoolManager.HasInstance && PoolManager.Instance.ReturnComponent(this))
        {
            return;
        }

        gameObject.SetActive(false);
    }
}
