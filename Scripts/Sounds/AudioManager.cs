using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 전역 AudioMixer 설정, BGM 전환, 재사용 SFX Voice를 관리한다.
/// </summary>
public sealed class AudioManager : MonoSingleton<AudioManager>
{
    private const float MutedDecibels = -80f;
    private const string MasterVolumeParameter = "masterVolume";
    private const string BgmVolumeParameter = "musicVolume";
    private const string SfxVolumeParameter = "soundsVolume";

    [Header("Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioMixerGroup _musicOutputGroup;
    [SerializeField] private AudioMixerGroup _soundsOutputGroup;

    [Space]
    [Header("BGM")]
    [SerializeField] private AudioSource _bgmSource;
    [Tooltip("BGM Fade Out과 Fade In 각각에 적용할 시간(초)")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.75f;

    [Space]
    [Header("SFX Voice")]
    [SerializeField, Min(1)] private int _globalMaxVoices = 24;
    [SerializeField, Min(0)] private int _criticalReservedVoices = 4;

    private readonly HashSet<SoundDataSO> _warnedInvalidSounds = new HashSet<SoundDataSO>();
    private readonly HashSet<SoundDataSO> _warnedMissingDistanceContext = new HashSet<SoundDataSO>();
    private readonly HashSet<AudioBus> _warnedMissingMixerParameters = new HashSet<AudioBus>();
    private readonly List<SfxVoice> _sfxVoices = new List<SfxVoice>();

    private SoundDataSO _currentBgm;
    private SoundDataSO _requestedBgm;
    private Coroutine _bgmTransition;
    private bool _warnedNullBgm;
    private bool _warnedNullSfx;
    private bool _isBgmConfigured;
    private bool _isSfxConfigured;
    private bool _isGameplayPaused;
    private Transform _listenerAnchor;
    private Transform _sfxVoiceRoot;

    private float _masterVolume = 1f;
    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;
    private bool _masterMuted;
    private bool _bgmMuted;
    private bool _sfxMuted;

    protected override void Awake()
    {
        base.Awake();

        if (!HasInstance || Instance != this)
        {
            return;
        }

        ValidateAndConfigureReferences();
        LoadAndApplySettings();
        AudioSettingsStore.Changed += HandleAudioSettingsChanged;
    }

    private void Update()
    {
        ReleaseCompletedSfxVoices();
    }

    protected override void OnDestroy()
    {
        AudioSettingsStore.Changed -= HandleAudioSettingsChanged;

        if (_bgmTransition != null)
        {
            StopCoroutine(_bgmTransition);
            _bgmTransition = null;
        }

        StopAllSfxVoices();

        base.OnDestroy();
    }

    private void OnValidate()
    {
        _fadeDuration = Mathf.Max(0f, _fadeDuration);
        _globalMaxVoices = Mathf.Max(1, _globalMaxVoices);
        _criticalReservedVoices = Mathf.Clamp(
            _criticalReservedVoices,
            0,
            _globalMaxVoices);
    }

    /// <summary>
    /// 같은 SoundDataSO는 유지하고 새 BGM은 Fade Out 후 교체하여 Fade In한다.
    /// </summary>
    public void PlayBgm(SoundDataSO sound)
    {
        if (!_isBgmConfigured)
        {
            return;
        }

        if (!TryGetBgmClip(sound, out AudioClip clip))
        {
            return;
        }

        if (_requestedBgm == sound)
        {
            return;
        }

        if (_bgmTransition != null)
        {
            StopCoroutine(_bgmTransition);
            _bgmTransition = null;
        }

        _requestedBgm = sound;

        if (_currentBgm == sound && _bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            _bgmTransition = StartCoroutine(FadeCurrentBgmIn(sound));
            return;
        }

        _bgmTransition = StartCoroutine(TransitionBgm(sound, clip));
    }

    /// <summary>
    /// SFX/UI SoundDataSO를 재사용 AudioSource로 재생한다. Voice가 부족하면 탈취하지 않고 생략한다.
    /// </summary>
    public void PlaySfx(SoundDataSO sound)
    {
        PlaySfx(sound, null);
    }

    /// <summary>
    /// 거리 감쇠 대상 SFX의 X/Y 발생 위치를 받아 재생 시작 시 한 번만 거리 배율을 계산한다.
    /// pitchMultiplier는 SoundDataSO의 랜덤 Pitch에 곱해 재생 길이를 줄이는 호출부 전용 배율이다.
    /// </summary>
    public void PlaySfx(SoundDataSO sound, Vector2? worldPosition, float pitchMultiplier = 1f)
    {
        if (!_isSfxConfigured || !TryValidateSfx(sound))
        {
            return;
        }

        if (_isGameplayPaused && sound.Category == SoundCategory.Sfx)
        {
            return;
        }

        ReleaseCompletedSfxVoices();

        float distanceMultiplier = 1f;
        if (sound.Category == SoundCategory.Sfx && sound.UseDistanceAttenuation)
        {
            if (!TryGetDistanceMultiplier(sound, worldPosition, out distanceMultiplier))
            {
                return;
            }
        }

        bool isProtected = sound.Category == SoundCategory.Ui || sound.IsCritical;
        CountActiveVoices(sound, out int activeVoices, out int activeGeneralVoices, out int activeSameSoundVoices);
        if (!CanRentVoice(
                _globalMaxVoices,
                _criticalReservedVoices,
                activeVoices,
                activeGeneralVoices,
                activeSameSoundVoices,
                sound.MaxConcurrent,
                isProtected))
        {
            return;
        }

        AudioClip clip = SelectRandomClip(sound);
        float pitch = SelectRandomPitch(sound.PitchRange) * SanitizePitchMultiplier(pitchMultiplier);
        SfxVoice voice = RentSfxVoice();
        if (voice == null)
        {
            return;
        }

        voice.Sound = sound;
        voice.Category = sound.Category;
        voice.IsProtected = isProtected;
        voice.IsActive = true;
        voice.Source.clip = clip;
        voice.Source.volume = sound.BaseVolume * distanceMultiplier;
        voice.Source.pitch = pitch;
        voice.Source.Play();
    }

    /// <summary>
    /// 거리 감쇠 기준 Transform을 명시적으로 갱신하며 null로 해제할 수 있다.
    /// </summary>
    public void SetListenerAnchor(Transform anchor)
    {
        _listenerAnchor = anchor;
    }

    /// <summary>
    /// Gameplay Pause 중 모든 SFX Voice를 반환하고 신규 SFX를 차단한다. UI와 BGM은 유지한다.
    /// </summary>
    public void SetGameplayPaused(bool paused)
    {
        _isGameplayPaused = paused;
        if (paused)
        {
            StopGameplaySfx();
        }
    }

    /// <summary>
    /// Gameplay Scene 이탈이나 Stage 전환 전에 SFX Voice만 정지·반환한다.
    /// </summary>
    public void StopGameplaySfx()
    {
        for (int i = 0; i < _sfxVoices.Count; i++)
        {
            SfxVoice voice = _sfxVoices[i];
            if (voice.IsActive && voice.Category == SoundCategory.Sfx)
            {
                ReleaseSfxVoice(voice);
            }
        }
    }

    /// <summary>
    /// 지정 Bus의 런타임 Volume을 Mixer에 적용하며 영구 저장은 수행하지 않는다.
    /// </summary>
    public void SetVolume(AudioBus bus, float normalizedValue)
    {
        float safeVolume = float.IsNaN(normalizedValue) || float.IsInfinity(normalizedValue)
            ? 1f
            : Mathf.Clamp01(normalizedValue);

        switch (bus)
        {
            case AudioBus.Master:
                _masterVolume = safeVolume;
                break;
            case AudioBus.Bgm:
                _bgmVolume = safeVolume;
                break;
            case AudioBus.Sfx:
                _sfxVolume = safeVolume;
                break;
            default:
                Logger.LogWarning($"AudioManager: 지원하지 않는 AudioBus({bus})의 Volume 적용을 무시합니다.", this);
                return;
        }

        ApplyMixerBus(bus);
    }

    /// <summary>
    /// 지정 Bus의 런타임 Mute를 Mixer에 적용하며 저장된 Volume은 유지한다.
    /// </summary>
    public void SetMute(AudioBus bus, bool muted)
    {
        switch (bus)
        {
            case AudioBus.Master:
                _masterMuted = muted;
                break;
            case AudioBus.Bgm:
                _bgmMuted = muted;
                break;
            case AudioBus.Sfx:
                _sfxMuted = muted;
                break;
            default:
                Logger.LogWarning($"AudioManager: 지원하지 않는 AudioBus({bus})의 Mute 적용을 무시합니다.", this);
                return;
        }

        ApplyMixerBus(bus);
    }

    private void ValidateAndConfigureReferences()
    {
        _globalMaxVoices = Mathf.Max(1, _globalMaxVoices);
        _criticalReservedVoices = Mathf.Clamp(
            _criticalReservedVoices,
            0,
            _globalMaxVoices);

        bool hasMixer = _audioMixer != null;

        if (!hasMixer)
        {
            Logger.LogWarning("AudioManager: AudioMixer 참조가 없어 재생 요청을 처리하지 않습니다.", this);
        }

        bool hasValidMusicGroup = _musicOutputGroup != null;
        if (_musicOutputGroup == null)
        {
            Logger.LogWarning("AudioManager: Music AudioMixerGroup 참조가 없어 BGM 요청을 처리하지 않습니다.", this);
        }
        else if (hasMixer && _musicOutputGroup.audioMixer != _audioMixer)
        {
            Logger.LogWarning("AudioManager: Music AudioMixerGroup이 지정된 AudioMixer에 속하지 않습니다.", this);
            hasValidMusicGroup = false;
        }

        bool hasBgmSource = _bgmSource != null;
        if (_bgmSource == null)
        {
            Logger.LogWarning("AudioManager: BGM AudioSource 참조가 없어 BGM 요청을 처리하지 않습니다.", this);
        }

        bool hasValidSoundsGroup = _soundsOutputGroup != null;
        if (_soundsOutputGroup == null)
        {
            Logger.LogWarning("AudioManager: Sounds AudioMixerGroup 참조가 없어 SFX/UI 요청을 처리하지 않습니다.", this);
        }
        else if (hasMixer && _soundsOutputGroup.audioMixer != _audioMixer)
        {
            Logger.LogWarning("AudioManager: Sounds AudioMixerGroup이 지정된 AudioMixer에 속하지 않습니다.", this);
            hasValidSoundsGroup = false;
        }

        _isBgmConfigured = hasMixer && hasValidMusicGroup && hasBgmSource;
        _isSfxConfigured = hasMixer && hasValidSoundsGroup;

        if (_isBgmConfigured)
        {
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.pitch = 1f;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.panStereo = 0f;
            _bgmSource.outputAudioMixerGroup = _musicOutputGroup;
        }
    }

    private void LoadAndApplySettings()
    {
        _masterVolume = AudioSettingsStore.LoadVolume(AudioBus.Master);
        _masterMuted = AudioSettingsStore.LoadMute(AudioBus.Master);
        _bgmVolume = AudioSettingsStore.LoadVolume(AudioBus.Bgm);
        _bgmMuted = AudioSettingsStore.LoadMute(AudioBus.Bgm);
        _sfxVolume = AudioSettingsStore.LoadVolume(AudioBus.Sfx);
        _sfxMuted = AudioSettingsStore.LoadMute(AudioBus.Sfx);

        ApplyMixerBus(AudioBus.Master);
        ApplyMixerBus(AudioBus.Bgm);
        ApplyMixerBus(AudioBus.Sfx);
    }

    private void HandleAudioSettingsChanged(AudioBus bus, float volume, bool muted)
    {
        switch (bus)
        {
            case AudioBus.Master:
                _masterVolume = volume;
                _masterMuted = muted;
                break;
            case AudioBus.Bgm:
                _bgmVolume = volume;
                _bgmMuted = muted;
                break;
            case AudioBus.Sfx:
                _sfxVolume = volume;
                _sfxMuted = muted;
                break;
            default:
                return;
        }

        ApplyMixerBus(bus);
    }

    private void ApplyMixerBus(AudioBus bus)
    {
        if (_audioMixer == null)
        {
            return;
        }

        GetBusState(bus, out float volume, out bool muted, out string parameterName);
        float decibels = ToDecibels(volume, muted);

        if (!_audioMixer.SetFloat(parameterName, decibels) && _warnedMissingMixerParameters.Add(bus))
        {
            Logger.LogWarning($"AudioManager: AudioMixer 노출 파라미터 '{parameterName}'을 찾지 못했습니다.", this);
        }
    }

    private void GetBusState(AudioBus bus, out float volume, out bool muted, out string parameterName)
    {
        switch (bus)
        {
            case AudioBus.Master:
                volume = _masterVolume;
                muted = _masterMuted;
                parameterName = MasterVolumeParameter;
                break;
            case AudioBus.Bgm:
                volume = _bgmVolume;
                muted = _bgmMuted;
                parameterName = BgmVolumeParameter;
                break;
            default:
                volume = _sfxVolume;
                muted = _sfxMuted;
                parameterName = SfxVolumeParameter;
                break;
        }
    }

    private bool TryGetBgmClip(SoundDataSO sound, out AudioClip clip)
    {
        clip = null;

        if (sound == null)
        {
            if (!_warnedNullBgm)
            {
                _warnedNullBgm = true;
                Logger.LogWarning("AudioManager: null BGM 요청을 무시합니다.", this);
            }

            return false;
        }

        if (sound.Category != SoundCategory.Bgm)
        {
            WarnInvalidSoundOnce(sound, "BGM 요청에는 Category=Bgm인 SoundDataSO가 필요합니다.");
            return false;
        }

        if (!sound.TryValidate(out string validationWarning))
        {
            WarnInvalidSoundOnce(sound, validationWarning);
            return false;
        }

        clip = sound.Clips[0];
        return true;
    }

    private bool TryValidateSfx(SoundDataSO sound)
    {
        if (sound == null)
        {
            if (!_warnedNullSfx)
            {
                _warnedNullSfx = true;
                Logger.LogWarning("AudioManager: null SFX/UI 요청을 무시합니다.", this);
            }

            return false;
        }

        if (sound.Category != SoundCategory.Sfx && sound.Category != SoundCategory.Ui)
        {
            WarnInvalidSoundOnce(sound, "SFX 요청에는 Category=Sfx 또는 Ui인 SoundDataSO가 필요합니다.");
            return false;
        }

        if (!sound.TryValidate(out string validationWarning))
        {
            WarnInvalidSoundOnce(sound, validationWarning);
            return false;
        }

        return true;
    }

    private bool TryGetDistanceMultiplier(
        SoundDataSO sound,
        Vector2? worldPosition,
        out float distanceMultiplier)
    {
        distanceMultiplier = 0f;

        if (_listenerAnchor == null || !worldPosition.HasValue)
        {
            if (_warnedMissingDistanceContext.Add(sound))
            {
                Logger.LogWarning(
                    $"AudioManager: 거리 감쇠 SoundDataSO '{sound.name}' 요청을 무시합니다. " +
                    "Listener Anchor와 발생 위치가 모두 필요합니다.",
                    sound);
            }

            return false;
        }

        Vector3 anchorPosition = _listenerAnchor.position;
        Vector2 listenerPosition = new Vector2(anchorPosition.x, anchorPosition.y);
        distanceMultiplier = CalculateDistanceMultiplier(
            listenerPosition,
            worldPosition.Value,
            sound.MinDistance,
            sound.MaxDistance);
        return distanceMultiplier > 0f;
    }

    private void WarnInvalidSoundOnce(SoundDataSO sound, string reason)
    {
        if (_warnedInvalidSounds.Add(sound))
        {
            Logger.LogWarning($"AudioManager: SoundDataSO '{sound.name}' 요청을 무시합니다. {reason}", sound);
        }
    }

    private IEnumerator TransitionBgm(SoundDataSO sound, AudioClip clip)
    {
        if (_bgmSource.isPlaying)
        {
            yield return FadeVolume(0f);
        }

        if (_requestedBgm != sound)
        {
            yield break;
        }

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.pitch = 1f;
        _bgmSource.volume = 0f;
        _currentBgm = sound;
        _bgmSource.Play();

        yield return FadeVolume(sound.BaseVolume);

        if (_requestedBgm == sound)
        {
            _bgmTransition = null;
        }
    }

    private IEnumerator FadeCurrentBgmIn(SoundDataSO sound)
    {
        yield return FadeVolume(sound.BaseVolume);

        if (_requestedBgm == sound)
        {
            _bgmTransition = null;
        }
    }

    private IEnumerator FadeVolume(float targetVolume)
    {
        float startVolume = _bgmSource.volume;
        if (_fadeDuration <= 0f)
        {
            _bgmSource.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / _fadeDuration);
            _bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, ratio);
            yield return null;
        }

        _bgmSource.volume = targetVolume;
    }

    private SfxVoice RentSfxVoice()
    {
        for (int i = 0; i < _sfxVoices.Count; i++)
        {
            if (!_sfxVoices[i].IsActive)
            {
                return _sfxVoices[i];
            }
        }

        if (_sfxVoices.Count >= _globalMaxVoices)
        {
            return null;
        }

        if (_sfxVoiceRoot == null)
        {
            GameObject rootObject = new GameObject("SFX Voices");
            _sfxVoiceRoot = rootObject.transform;
            _sfxVoiceRoot.SetParent(transform, false);
        }

        GameObject voiceObject = new GameObject($"SFX Voice {_sfxVoices.Count + 1}");
        voiceObject.transform.SetParent(_sfxVoiceRoot, false);
        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.pitch = 1f;
        source.spatialBlend = 0f;
        source.panStereo = 0f;
        source.outputAudioMixerGroup = _soundsOutputGroup;

        SfxVoice voice = new SfxVoice(source);
        _sfxVoices.Add(voice);
        return voice;
    }

    private void CountActiveVoices(
        SoundDataSO sound,
        out int activeVoices,
        out int activeGeneralVoices,
        out int activeSameSoundVoices)
    {
        activeVoices = 0;
        activeGeneralVoices = 0;
        activeSameSoundVoices = 0;

        for (int i = 0; i < _sfxVoices.Count; i++)
        {
            SfxVoice voice = _sfxVoices[i];
            if (!voice.IsActive)
            {
                continue;
            }

            activeVoices++;
            if (!voice.IsProtected)
            {
                activeGeneralVoices++;
            }

            if (voice.Sound == sound)
            {
                activeSameSoundVoices++;
            }
        }
    }

    private void ReleaseCompletedSfxVoices()
    {
        for (int i = 0; i < _sfxVoices.Count; i++)
        {
            SfxVoice voice = _sfxVoices[i];
            if (voice.IsActive && !voice.Source.isPlaying)
            {
                ReleaseSfxVoice(voice);
            }
        }
    }

    private static void ReleaseSfxVoice(SfxVoice voice)
    {
        voice.Source.Stop();
        voice.Source.clip = null;
        voice.Source.volume = 1f;
        voice.Source.pitch = 1f;
        voice.Sound = null;
        voice.Category = default;
        voice.IsProtected = false;
        voice.IsActive = false;
    }

    private void StopAllSfxVoices()
    {
        for (int i = 0; i < _sfxVoices.Count; i++)
        {
            if (_sfxVoices[i].IsActive)
            {
                ReleaseSfxVoice(_sfxVoices[i]);
            }
        }
    }

    private static bool CanRentVoice(
        int globalMaxVoices,
        int criticalReservedVoices,
        int activeVoices,
        int activeGeneralVoices,
        int activeSameSoundVoices,
        int maxConcurrent,
        bool isProtected)
    {
        if (globalMaxVoices <= 0 || activeVoices >= globalMaxVoices)
        {
            return false;
        }

        if (maxConcurrent > 0 && activeSameSoundVoices >= maxConcurrent)
        {
            return false;
        }

        int reservedVoices = Mathf.Clamp(criticalReservedVoices, 0, globalMaxVoices);
        int generalCapacity = globalMaxVoices - reservedVoices;
        return isProtected || activeGeneralVoices < generalCapacity;
    }

    private static float CalculateDistanceMultiplier(
        Vector2 listenerPosition,
        Vector2 sourcePosition,
        float minDistance,
        float maxDistance)
    {
        float distance = Vector2.Distance(listenerPosition, sourcePosition);
        if (distance <= minDistance)
        {
            return 1f;
        }

        if (distance >= maxDistance)
        {
            return 0f;
        }

        return 1f - ((distance - minDistance) / (maxDistance - minDistance));
    }

    private static AudioClip SelectRandomClip(SoundDataSO sound)
    {
        int clipIndex = Random.Range(0, sound.Clips.Count);
        return sound.Clips[clipIndex];
    }

    // 호출부가 잘못된 배율을 넘겨도 Voice의 Pitch가 0 이하나 NaN이 되지 않게 보정한다.
    private static float SanitizePitchMultiplier(float pitchMultiplier)
    {
        if (!float.IsFinite(pitchMultiplier) || pitchMultiplier <= 0f)
        {
            return 1f;
        }

        return pitchMultiplier;
    }

    private static float SelectRandomPitch(Vector2 pitchRange)
    {
        if (Mathf.Approximately(pitchRange.x, pitchRange.y))
        {
            return pitchRange.x;
        }

        return Random.Range(pitchRange.x, pitchRange.y);
    }

    private static float ToDecibels(float normalizedValue, bool muted)
    {
        if (muted || normalizedValue <= 0f)
        {
            return MutedDecibels;
        }

        return 20f * Mathf.Log10(normalizedValue);
    }

    private sealed class SfxVoice
    {
        public SfxVoice(AudioSource source)
        {
            Source = source;
        }

        public AudioSource Source { get; }
        public SoundDataSO Sound { get; set; }
        public SoundCategory Category { get; set; }
        public bool IsProtected { get; set; }
        public bool IsActive { get; set; }
    }
}
