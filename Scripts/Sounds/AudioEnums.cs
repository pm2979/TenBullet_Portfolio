/// <summary>
/// 사운드의 재생 정책과 Pause 대상을 구분한다.
/// </summary>
public enum SoundCategory
{
    Bgm,
    Sfx,
    Ui
}

/// <summary>
/// 사용자 음량과 Mute를 적용할 AudioMixer Bus를 식별한다.
/// </summary>
public enum AudioBus
{
    Master,
    Bgm,
    Sfx
}
