using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 하드웨어 커서와 UI 소프트웨어 조준점 사이의 표시 방식을 전환하고,
/// 소프트웨어 조준점 사용 시 마우스 위치에 맞춰 위치를 갱신하는 컨트롤러다.
/// 설정·참조가 무효하면 항상 기본 Hardware Cursor로 Fallback한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatCursorController : MonoBehaviour
{
    [SerializeField, Tooltip("커서 표시 방식과 잠금 설정")]
    private CursorSettingsSO _settings;
    [SerializeField, Tooltip("소프트웨어 조준점으로 사용할 UI RectTransform")]
    private RectTransform _softwareReticle;
    [SerializeField, Tooltip("소프트웨어 조준점이 속한 Canvas. 비어 있으면 Software Reticle의 부모에서 자동 탐색한다")]
    private Canvas _canvas;

    private Texture2D _resizedHardwareCursor;

    // 각 Fallback 원인은 Component 수명 동안 한 번만 진단한다(ApplyPresentation 반복 호출에도 중복 로그 없음)
    private bool _loggedMissingSettings;
    private bool _loggedMissingSoftwareReticle;
    private bool _loggedMissingCanvas;
    private bool _loggedMissingMouse;

    private void OnEnable()
    {
        ApplyPresentation();
    }

    private void LateUpdate()
    {
        // 입력과 UI 레이아웃 갱신 뒤에 소프트웨어 조준점 위치를 최종 반영하는 순서.
        // ApplyPresentation에서 이미 유효성을 확인했으므로 여기서는 활성 상태만 확인하고 재검증·로그를 하지 않는다
        if (_softwareReticle == null || !_softwareReticle.gameObject.activeSelf || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        RectTransform parentRect = _softwareReticle.parent as RectTransform;
        // Overlay Canvas는 이벤트 카메라가 없어야 올바르게 좌표 변환되는 Unity UI 규약
        Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPosition, eventCamera, out Vector2 localPosition))
        {
            _softwareReticle.localPosition = localPosition;
        }
    }

    private void OnDisable()
    {
        // 씬 전환 후 숨김 또는 제한 상태가 운영체제 커서에 남지 않도록 기본값 복원
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ReleaseResizedHardwareCursor();

        if (_softwareReticle != null)
        {
            _softwareReticle.gameObject.SetActive(false);
        }
    }

    // 현재 설정을 다시 적용한다. Settings 누락, SoftwareReticle 요구 시 Reticle·Canvas·Mouse 중 하나라도
    // 무효하면 조작 불능을 막기 위해 항상 기본 Hardware Cursor로 Fallback한다
    public void ApplyPresentation()
    {
        ReleaseResizedHardwareCursor();

        if (_settings == null)
        {
            LogFallbackOnce(ref _loggedMissingSettings, "_settings(Cursor Settings)");
            ApplyFallbackPresentation();
            return;
        }

        if (_settings.Presentation != CursorPresentation.SoftwareReticle)
        {
            ApplyHardwarePresentation();
            return;
        }

        if (!TryResolveSoftwareReticle(out Canvas resolvedCanvas))
        {
            ApplyFallbackPresentation();
            return;
        }

        ApplySoftwareReticlePresentation(resolvedCanvas);
    }

    // SoftwareReticle 요구 시 실제로 표시 가능한 상태인지 확인한다. Reticle, 좌표 공간을 제공할 Canvas와
    // 부모 RectTransform, 위치 갱신에 필요한 Mouse 장치를 모두 갖춰야 유효하다
    private bool TryResolveSoftwareReticle(out Canvas resolvedCanvas)
    {
        resolvedCanvas = null;

        if (_softwareReticle == null)
        {
            LogFallbackOnce(ref _loggedMissingSoftwareReticle, "_softwareReticle(Software Reticle)");
            return false;
        }

        resolvedCanvas = _canvas != null ? _canvas : _softwareReticle.GetComponentInParent<Canvas>();

        if (resolvedCanvas == null || !(_softwareReticle.parent is RectTransform))
        {
            LogFallbackOnce(ref _loggedMissingCanvas, "Canvas 또는 Software Reticle의 좌표 공간");
            return false;
        }

        if (Mouse.current == null)
        {
            LogFallbackOnce(ref _loggedMissingMouse, "Mouse 장치");
            return false;
        }

        return true;
    }

    private void ApplySoftwareReticlePresentation(Canvas resolvedCanvas)
    {
        _canvas = resolvedCanvas;

        Cursor.lockState = _settings.ConfineCursor ? CursorLockMode.Confined : CursorLockMode.None;
        Cursor.visible = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        _softwareReticle.gameObject.SetActive(true);
    }

    private void ApplyHardwarePresentation()
    {
        Texture2D hardwareCursor = _settings.HardwareCursor;
        Vector2 hotspot = _settings.Hotspot;
        CursorMode cursorMode = CursorMode.Auto;

        if (hardwareCursor != null)
        {
            int cursorSize = _settings.HardwareCursorSize;
            if (hardwareCursor.width != cursorSize || hardwareCursor.height != cursorSize)
            {
                _resizedHardwareCursor = CreateResizedHardwareCursor(hardwareCursor, cursorSize);
                if (_resizedHardwareCursor != null)
                {
                    hotspot = new Vector2(
                        hotspot.x * cursorSize / hardwareCursor.width,
                        hotspot.y * cursorSize / hardwareCursor.height);
                    hardwareCursor = _resizedHardwareCursor;
                    // Windows 하드웨어 커서의 OS 크기 보정을 우회해 Inspector 픽셀 크기를 그대로 적용
                    cursorMode = CursorMode.ForceSoftware;
                }
            }
        }

        Cursor.lockState = _settings.ConfineCursor ? CursorLockMode.Confined : CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(hardwareCursor, hotspot, cursorMode);

        if (_softwareReticle != null)
        {
            _softwareReticle.gameObject.SetActive(false);
        }
    }

    // 설정 오류로 표시할 수 없는 모든 경우의 공통 안전 상태. Confined보다 조작 가능성을 우선해 lock은 항상 None이다
    private void ApplyFallbackPresentation()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (_softwareReticle != null)
        {
            _softwareReticle.gameObject.SetActive(false);
        }
    }

    private void LogFallbackOnce(ref bool logged, string missingDescription)
    {
        if (logged)
        {
            return;
        }

        Logger.LogWarning($"CombatCursorController: {missingDescription}이(가) 유효하지 않아 기본 Hardware Cursor로 대체합니다.", this);
        logged = true;
    }

    private Texture2D CreateResizedHardwareCursor(Texture2D source, int size)
    {
        if (!source.isReadable)
        {
            Debug.LogError("커서 크기를 조절하려면 텍스처의 Read/Write 설정이 필요합니다.", this);
            return null;
        }

        Color32[] sourcePixels = source.GetPixels32();
        Color32[] resizedPixels = new Color32[size * size];

        // 픽셀 아트의 윤곽과 조준점 대칭을 함께 유지하기 위한 중심 기준 보간
        for (int y = 0; y < size; y++)
        {
            int sourceY = GetSymmetricSourceCoordinate(y, size, source.height);
            int sourceRow = sourceY * source.width;
            int targetRow = y * size;

            for (int x = 0; x < size; x++)
            {
                int sourceX = GetSymmetricSourceCoordinate(x, size, source.width);
                resizedPixels[targetRow + x] = sourcePixels[sourceRow + sourceX];
            }
        }

        Texture2D resizedCursor = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"{source.name} ({size}px)",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        resizedCursor.SetPixels32(resizedPixels);
        resizedCursor.Apply(false, false);
        return resizedCursor;
    }

    private static int GetSymmetricSourceCoordinate(
        int targetCoordinate,
        int targetSize,
        int sourceSize)
    {
        int mirroredTargetCoordinate = Mathf.Min(
            targetCoordinate,
            targetSize - 1 - targetCoordinate);
        float sourceCoordinate =
            (mirroredTargetCoordinate + 0.5f) * sourceSize / targetSize - 0.5f;
        int sampledCoordinate = Mathf.Clamp(
            Mathf.RoundToInt(sourceCoordinate),
            0,
            sourceSize - 1);

        bool useMirroredCoordinate = targetCoordinate >= (targetSize + 1) / 2;
        return useMirroredCoordinate
            ? sourceSize - 1 - sampledCoordinate
            : sampledCoordinate;
    }

    private void ReleaseResizedHardwareCursor()
    {
        if (_resizedHardwareCursor == null)
        {
            return;
        }

        Destroy(_resizedHardwareCursor);
        _resizedHardwareCursor = null;
    }
}
