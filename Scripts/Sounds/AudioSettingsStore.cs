using System;
using UnityEngine;

/// <summary>
/// 진행 세이브와 분리된 Audio Volume·Mute 설정을 PlayerPrefs에 저장하고 변경을 통지한다.
/// </summary>
public static class AudioSettingsStore
{
    private const float DefaultVolume = 1f;
    private const bool DefaultMute = false;

    private const string MasterVolumeKey = "audio.master.volume";
    private const string MasterMuteKey = "audio.master.mute";
    private const string BgmVolumeKey = "audio.bgm.volume";
    private const string BgmMuteKey = "audio.bgm.mute";
    private const string SfxVolumeKey = "audio.sfx.volume";
    private const string SfxMuteKey = "audio.sfx.mute";

    public static event Action<AudioBus, float, bool> Changed;

    // 저장값이 없거나 손상되었으면 안전한 기본 Volume을 반환한다.
    public static float LoadVolume(AudioBus bus)
    {
        if (!TryGetKeys(bus, out string volumeKey, out _))
        {
            return DefaultVolume;
        }

        float storedVolume = PlayerPrefs.GetFloat(volumeKey, DefaultVolume);
        if (float.IsNaN(storedVolume) || float.IsInfinity(storedVolume) ||
            storedVolume < 0f || storedVolume > 1f)
        {
            return DefaultVolume;
        }

        return storedVolume;
    }

    // 저장값이 없거나 손상되었으면 Mute 해제 상태를 반환한다.
    public static bool LoadMute(AudioBus bus)
    {
        if (!TryGetKeys(bus, out _, out string muteKey))
        {
            return DefaultMute;
        }

        int storedMute = PlayerPrefs.GetInt(muteKey, DefaultMute ? 1 : 0);
        return storedMute == 0 || storedMute == 1
            ? storedMute == 1
            : DefaultMute;
    }

    // 정규화 Volume을 저장하고 해당 Bus의 현재 설정을 구독자에게 전달한다.
    public static void SaveVolume(AudioBus bus, float normalizedValue)
    {
        if (!TryGetKeys(bus, out string volumeKey, out _))
        {
            Logger.LogWarning($"AudioSettingsStore: 지원하지 않는 AudioBus({bus})의 Volume 저장을 무시합니다.");
            return;
        }

        float safeVolume = float.IsNaN(normalizedValue) || float.IsInfinity(normalizedValue)
            ? DefaultVolume
            : Mathf.Clamp01(normalizedValue);

        PlayerPrefs.SetFloat(volumeKey, safeVolume);
        PlayerPrefs.Save();
        Changed?.Invoke(bus, safeVolume, LoadMute(bus));
    }

    // Mute를 저장하고 해당 Bus의 현재 설정을 구독자에게 전달한다.
    public static void SaveMute(AudioBus bus, bool muted)
    {
        if (!TryGetKeys(bus, out _, out string muteKey))
        {
            Logger.LogWarning($"AudioSettingsStore: 지원하지 않는 AudioBus({bus})의 Mute 저장을 무시합니다.");
            return;
        }

        PlayerPrefs.SetInt(muteKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke(bus, LoadVolume(bus), muted);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Changed = null;
    }

    private static bool TryGetKeys(AudioBus bus, out string volumeKey, out string muteKey)
    {
        switch (bus)
        {
            case AudioBus.Master:
                volumeKey = MasterVolumeKey;
                muteKey = MasterMuteKey;
                return true;
            case AudioBus.Bgm:
                volumeKey = BgmVolumeKey;
                muteKey = BgmMuteKey;
                return true;
            case AudioBus.Sfx:
                volumeKey = SfxVolumeKey;
                muteKey = SfxMuteKey;
                return true;
            default:
                volumeKey = null;
                muteKey = null;
                return false;
        }
    }
}
