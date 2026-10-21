using System.Collections;
using UnityEngine;

/// <summary>
/// 스태미나 변화를 단일 SpriteRenderer의 게이지·경고 애니메이션으로 표현한다.
/// </summary>
[DisallowMultipleComponent]
public class StaminaGauge : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Stamina _stamina;
    [SerializeField] private GameObject _visualRoot;
    [SerializeField] private SpriteRenderer _gaugeRenderer;

    [Header("Stamina Sheet")]
    [Tooltip("0은 최대 Stamina, 마지막 Sprite는 빈 Stamina인 순서로 연결합니다.")]
    [SerializeField] private Sprite[] _staminaSheetSprites;
    [Tooltip("소비 Sheet의 초당 재생 프레임 수입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _staminaAnimationFramesPerSecond = 12f;
    [Tooltip("회복 Sheet의 초당 재생 프레임 수입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _recoveryAnimationFramesPerSecond = 12f;

    [Header("Alarm Sheet")]
    [Tooltip("유효 비용이지만 잔량 부족일 때 같은 Gauge Renderer에 재생할 Sprite 순서입니다.")]
    [SerializeField] private Sprite[] _alarmSheetSprites;
    [Tooltip("부족 Alarm Sheet의 초당 재생 프레임 수입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _alarmAnimationFramesPerSecond = 12f;

    [Header("표시 수명")]
    [Tooltip("최대 Stamina 도달 후 VisualRoot를 숨기기까지의 시간입니다. 플레이 테스트로 확정합니다.")]
    [Min(0f)]
    [SerializeField] private float _hideDelaySeconds;

    private Coroutine _hideCoroutine;
    private Coroutine _normalAnimationCoroutine;
    private Coroutine _alarmAnimationCoroutine;
    private int _displayedStaminaFrameIndex = -1;
    private bool _isSubscribed;
    private bool _isRecoverySequenceActive;
    private bool _isShowingAlarmFrame;
    private bool _hasLoggedInvalidMax;
    private bool _hasLoggedMissingStamina;
    private bool _hasLoggedMissingVisualRoot;
    private bool _hasLoggedMissingGaugeRenderer;
    private bool _hasLoggedInvalidVisualRoot;

    private void OnEnable()
    {
        RenderCurrentStamina();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelHide();
        StopNormalAnimation();
        StopAlarmAnimation();
        _isRecoverySequenceActive = false;
        _displayedStaminaFrameIndex = -1;
        _isShowingAlarmFrame = false;
    }

    // 최신 수치를 정지 프레임에 반영하고 최대치 여부에 따라 표시 수명을 다시 정한다.
    private void RenderCurrentStamina()
    {
        if (_stamina == null)
        {
            LogMissingStamina();
            return;
        }

        RenderStaminaFrame(_stamina.Current, _stamina.Max);
        UpdateVisibility(_stamina.Current, _stamina.Max);
    }

    // 변경 사유와 전후 프레임에 맞춰 소비 또는 회복 구간만 재생한다.
    private void Stamina_Changed(StaminaChange change)
    {
        int previousFrameIndex = CalculateStaminaFrameIndex(change.PreviousStamina, change.MaxStamina);
        int currentFrameIndex = CalculateStaminaFrameIndex(change.CurrentStamina, change.MaxStamina);

        if (change.Reason == StaminaChangeReason.Consumed)
        {
            _isRecoverySequenceActive = false;
            StopNormalAnimation();
            StopAlarmAnimation();
        }

        RenderStaminaFrame(change.CurrentStamina, change.MaxStamina);
        UpdateVisibility(change.CurrentStamina, change.MaxStamina);

        switch (change.Reason)
        {
            case StaminaChangeReason.Consumed:
                PlayNormalAnimation(previousFrameIndex, currentFrameIndex);
                break;

            case StaminaChangeReason.Recovered:
                StartRecoveryAnimation(previousFrameIndex, currentFrameIndex);

                if (IsFullyRecovered(change.CurrentStamina, change.MaxStamina))
                {
                    _isRecoverySequenceActive = false;
                }

                break;

            case StaminaChangeReason.Initialized:
            case StaminaChangeReason.MaxChanged:
                if (IsFullyRecovered(change.CurrentStamina, change.MaxStamina))
                {
                    _isRecoverySequenceActive = false;
                }

                break;
        }
    }

    // 잔량 부족은 게이지 값을 바꾸지 않고 같은 Renderer의 경고 Sheet로 표현한다.
    private void Stamina_ConsumptionRejected(StaminaConsumptionRejected _)
    {
        CancelHide();
        SetVisualRootActive(true);
        StopNormalAnimation();
        PlayAlarmAnimation();
    }

    // 중복 구독 없이 Stamina 변경과 소비 거부만 수신한다.
    private void Subscribe()
    {
        if (_isSubscribed || _stamina == null)
        {
            return;
        }

        _stamina.Changed += Stamina_Changed;
        _stamina.ConsumptionRejected += Stamina_ConsumptionRejected;
        _isSubscribed = true;
    }

    // 비활성화 이후에는 Stamina 이벤트를 받지 않도록 해제한다.
    private void Unsubscribe()
    {
        if (!_isSubscribed || _stamina == null)
        {
            return;
        }

        _stamina.Changed -= Stamina_Changed;
        _stamina.ConsumptionRejected -= Stamina_ConsumptionRejected;
        _isSubscribed = false;
    }

    // 현재량 비율을 가득 찬 첫 프레임부터 빈 마지막 프레임 사이의 인덱스로 변환한다.
    private int CalculateStaminaFrameIndex(float currentStamina, float maxStamina)
    {
        if (_staminaSheetSprites == null || _staminaSheetSprites.Length == 0)
        {
            return -1;
        }

        if (!float.IsFinite(maxStamina) || maxStamina <= 0f)
        {
            if (!_hasLoggedInvalidMax)
            {
                Logger.LogWarning($"{nameof(StaminaGauge)}가 유효하지 않은 Max Stamina({maxStamina})를 받아 빈 Gauge로 표시합니다.", this);
                _hasLoggedInvalidMax = true;
            }

            return _staminaSheetSprites.Length - 1;
        }

        _hasLoggedInvalidMax = false;

        if (!float.IsFinite(currentStamina))
        {
            Logger.LogWarning($"{nameof(StaminaGauge)}가 유효하지 않은 Current Stamina({currentStamina})를 받아 빈 Gauge로 표시합니다.", this);
            return _staminaSheetSprites.Length - 1;
        }

        float emptyRatio = 1f - Mathf.Clamp01(currentStamina / maxStamina);
        return Mathf.RoundToInt(emptyRatio * (_staminaSheetSprites.Length - 1));
    }

    // 정지 프레임이 달라졌거나 경고 화면을 복구할 때만 Sprite를 교체한다.
    private void RenderStaminaFrame(float currentStamina, float maxStamina)
    {
        if (!CanRenderGauge())
        {
            return;
        }

        int nextFrameIndex = CalculateStaminaFrameIndex(currentStamina, maxStamina);
        if (nextFrameIndex < 0)
        {
            return;
        }

        if (_isShowingAlarmFrame || nextFrameIndex == _displayedStaminaFrameIndex)
        {
            return;
        }

        SetStaminaFrame(nextFrameIndex);
    }

    // 최대치가 아니면 즉시 보이고, 최대치면 설정한 지연 뒤에만 VisualRoot를 숨긴다.
    private void UpdateVisibility(float currentStamina, float maxStamina)
    {
        if (!IsFullyRecovered(currentStamina, maxStamina))
        {
            CancelHide();
            SetVisualRootActive(true);
            return;
        }

        ScheduleHide();
    }

    // 유효한 최대치와 현재량이 일치할 때만 숨김을 예약한다.
    private static bool IsFullyRecovered(float currentStamina, float maxStamina)
    {
        return float.IsFinite(currentStamina)
            && float.IsFinite(maxStamina)
            && maxStamina > 0f
            && Mathf.Approximately(currentStamina, maxStamina);
    }

    // 같은 숨김 예약이 중첩되지 않도록 기존 예약을 취소하고 새로 시작한다.
    private void ScheduleHide()
    {
        if (!CanControlVisualRoot() || !_visualRoot.activeSelf)
        {
            return;
        }

        CancelHide();

        if (_hideDelaySeconds <= 0f)
        {
            SetVisualRootActive(false);
            return;
        }

        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    // 지연 중 새 Stamina 이벤트가 없었고 여전히 최대치일 때만 숨긴다.
    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(_hideDelaySeconds);
        _hideCoroutine = null;

        if (_stamina != null && IsFullyRecovered(_stamina.Current, _stamina.Max))
        {
            SetVisualRootActive(false);
        }
    }

    // 진행 중인 숨김 예약을 정리해 다음 이벤트가 즉시 다시 보이게 한다.
    private void CancelHide()
    {
        if (_hideCoroutine == null)
        {
            return;
        }

        StopCoroutine(_hideCoroutine);
        _hideCoroutine = null;
    }

    // 연속 회복 중 첫 변경에서만 회복 구간 재생을 시작해 매 프레임 재시작하지 않게 한다.
    private void StartRecoveryAnimation(int previousFrameIndex, int currentFrameIndex)
    {
        if (_isRecoverySequenceActive)
        {
            return;
        }

        _isRecoverySequenceActive = true;
        PlayNormalAnimation(previousFrameIndex, currentFrameIndex, _recoveryAnimationFramesPerSecond);
    }

    // 소비 또는 회복으로 실제 변한 Sheet 구간만 한 번 재생한다.
    private void PlayNormalAnimation(int startFrameIndex, int endFrameIndex)
    {
        PlayNormalAnimation(startFrameIndex, endFrameIndex, _staminaAnimationFramesPerSecond);
    }

    // 소비·회복 각각에 지정한 FPS로 현재 변경 Sheet 구간만 재생한다.
    private void PlayNormalAnimation(int startFrameIndex, int endFrameIndex, float framesPerSecond)
    {
        StopNormalAnimation();

        if (!CanRenderGauge() || startFrameIndex < 0 || endFrameIndex < 0 || startFrameIndex == endFrameIndex || !IsValidFramesPerSecond(framesPerSecond))
        {
            return;
        }

        _normalAnimationCoroutine = StartCoroutine(PlayNormalAnimationCoroutine(startFrameIndex, endFrameIndex, framesPerSecond));
    }

    // 시작과 끝의 순서에 맞춰 소비는 정방향, 회복은 역방향으로 재생한다.
    private IEnumerator PlayNormalAnimationCoroutine(int startFrameIndex, int endFrameIndex, float framesPerSecond)
    {
        int step = startFrameIndex < endFrameIndex ? 1 : -1;
        float frameDuration = 1f / framesPerSecond;

        for (int frameIndex = startFrameIndex; ; frameIndex += step)
        {
            SetStaminaFrame(frameIndex);
            float elapsed = 0f;

            while (elapsed < frameDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (frameIndex == endFrameIndex)
            {
                break;
            }
        }

        _normalAnimationCoroutine = null;
        RenderCurrentStamina();
    }

    // 진행 중인 정상 Sheet 연출을 중지하고 Coroutine 참조를 정리한다.
    private void StopNormalAnimation()
    {
        if (_normalAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(_normalAnimationCoroutine);
        _normalAnimationCoroutine = null;
    }

    // 부족 Alarm Sheet를 같은 Gauge Renderer에 처음부터 한 번 재생한다.
    private void PlayAlarmAnimation()
    {
        StopAlarmAnimation();

        if (!CanRenderGauge() || _alarmSheetSprites == null || _alarmSheetSprites.Length == 0 || !IsValidFramesPerSecond(_alarmAnimationFramesPerSecond))
        {
            return;
        }

        _alarmAnimationCoroutine = StartCoroutine(PlayAlarmAnimationCoroutine());
    }

    // Alarm 종료 뒤 현재 Stamina의 정지 프레임으로 복귀한다.
    private IEnumerator PlayAlarmAnimationCoroutine()
    {
        float frameDuration = 1f / _alarmAnimationFramesPerSecond;
        _isShowingAlarmFrame = true;

        for (int frameIndex = 0; frameIndex < _alarmSheetSprites.Length; frameIndex++)
        {
            _gaugeRenderer.sprite = _alarmSheetSprites[frameIndex];
            float elapsed = 0f;

            while (elapsed < frameDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        _alarmAnimationCoroutine = null;
        _isShowingAlarmFrame = false;
        RenderCurrentStamina();
    }

    // 현재 Gauge Sheet 프레임을 적용한다.
    private void SetStaminaFrame(int frameIndex)
    {
        _gaugeRenderer.sprite = _staminaSheetSprites[frameIndex];
        _displayedStaminaFrameIndex = frameIndex;
        _isShowingAlarmFrame = false;
    }

    private void StopAlarmAnimation()
    {
        if (_alarmAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(_alarmAnimationCoroutine);
        _alarmAnimationCoroutine = null;
        _isShowingAlarmFrame = false;
    }

    // 0 이하이거나 유한하지 않은 속도는 Coroutine 시작 전에 거부한다.
    private static bool IsValidFramesPerSecond(float framesPerSecond)
    {
        return float.IsFinite(framesPerSecond) && framesPerSecond > 0f;
    }

    // Controller가 아닌 VisualRoot만 켜고 끈다.
    private void SetVisualRootActive(bool isActive)
    {
        if (!CanControlVisualRoot())
        {
            return;
        }

        if (_visualRoot.activeSelf != isActive)
        {
            _visualRoot.SetActive(isActive);
        }
    }

    // 컨트롤러 자신을 숨기는 연결을 막아 비활성화 후 이벤트 수신이 끊기지 않게 한다.
    private bool CanControlVisualRoot()
    {
        if (_visualRoot == null)
        {
            LogMissingVisualRoot();
            return false;
        }

        if (_visualRoot == gameObject)
        {
            if (!_hasLoggedInvalidVisualRoot)
            {
                Logger.LogWarning($"{nameof(StaminaGauge)}의 {nameof(_visualRoot)}는 Controller 자신이 아닌 자식 GameObject여야 합니다.", this);
                _hasLoggedInvalidVisualRoot = true;
            }

            return false;
        }

        return true;
    }

    // Renderer와 기본 Sheet가 모두 연결된 경우에만 게이지를 갱신한다.
    private bool CanRenderGauge()
    {
        if (_gaugeRenderer != null && _staminaSheetSprites != null && _staminaSheetSprites.Length > 0)
        {
            return true;
        }

        if (!_hasLoggedMissingGaugeRenderer)
        {
            Logger.LogWarning($"{nameof(StaminaGauge)}에 {nameof(_gaugeRenderer)} 또는 Stamina Sheet 참조가 없어 표시를 갱신할 수 없습니다.", this);
            _hasLoggedMissingGaugeRenderer = true;
        }

        return false;
    }

    // Inspector 연결 누락은 한 번만 기록한다.
    private void LogMissingStamina()
    {
        if (_hasLoggedMissingStamina)
        {
            return;
        }

        Logger.LogWarning($"{nameof(StaminaGauge)}에 {nameof(_stamina)} 참조가 없어 표시를 갱신할 수 없습니다.", this);
        _hasLoggedMissingStamina = true;
    }

    // Inspector 연결 누락은 한 번만 기록한다.
    private void LogMissingVisualRoot()
    {
        if (_hasLoggedMissingVisualRoot)
        {
            return;
        }

        Logger.LogWarning($"{nameof(StaminaGauge)}에 {nameof(_visualRoot)} 참조가 없어 표시 상태를 갱신할 수 없습니다.", this);
        _hasLoggedMissingVisualRoot = true;
    }
}
