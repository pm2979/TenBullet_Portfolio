using UnityEngine;

/// <summary>
/// 플랫폼 커서와 UI 조준점 사이의 표시 전략을 구분하는 enum이다.
/// </summary>
public enum CursorPresentation
{
    Hardware = 0,
    SoftwareReticle = 1
}

/// <summary>
/// 씬과 무관하게 동일한 커서 정책(표시 방식, 텍스처, 잠금 여부 등)을 공유하기 위한 설정 데이터다.
/// </summary>
[CreateAssetMenu(fileName = "CursorSettings", menuName = "Top Down Combat/Cursor Settings")]
public sealed class CursorSettingsSO : ScriptableObject
{
    private const int MinHardwareCursorSize = 8;
    private const int MaxHardwareCursorSize = 128;

    [SerializeField, Tooltip("하드웨어 커서 또는 UI 소프트웨어 조준점 사용 여부")]
    private CursorPresentation _presentation = CursorPresentation.SoftwareReticle;
    [SerializeField, Tooltip("하드웨어 커서 모드에서 사용할 텍스처")]
    private Texture2D _hardwareCursor;
    [SerializeField]
    [Range(MinHardwareCursorSize, MaxHardwareCursorSize)]
    [Tooltip("하드웨어 커서의 출력 크기(px)")]
    private int _hardwareCursorSize = 32;
    [SerializeField, Tooltip("하드웨어 커서 클릭 기준점")]
    private Vector2 _hotspot;
    [SerializeField, Tooltip("게임 창 안으로 커서를 제한할지 여부")]
    private bool _confineCursor = true;

    public CursorPresentation Presentation => _presentation;
    public Texture2D HardwareCursor => _hardwareCursor;
    public int HardwareCursorSize => Mathf.Clamp(
        _hardwareCursorSize,
        MinHardwareCursorSize,
        MaxHardwareCursorSize);
    public Vector2 Hotspot => _hotspot;
    public bool ConfineCursor => _confineCursor;
}
