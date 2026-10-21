using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 총알 카메라를 저해상도 RenderTexture로 렌더링 후 Point 필터로 확대 표시
/// 화면비 변경 시 동일 픽셀 높이 기준으로 재생성
/// </summary>
[RequireComponent(typeof(Camera))]
public class TitlePixelCamera : MonoBehaviour
{
    [SerializeField] private RawImage _output;
    [Tooltip("세로 픽셀 수. 작을수록 도트가 커짐")]
    [SerializeField, Min(16)] private int _pixelHeight = 216;

    private Camera _camera;
    private RenderTexture _texture;
    private int _screenWidth;
    private int _screenHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void Update()
    {
        if (Screen.width != _screenWidth || Screen.height != _screenHeight) Rebuild();
    }

    private void OnDisable()
    {
        _camera.targetTexture = null;
        if (_output != null)
        {
            _output.texture = null;
            _output.enabled = false;
        }
        ReleaseTexture();
    }

    private void Rebuild()
    {
        ReleaseTexture();

        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        int width = Mathf.Max(1, Mathf.RoundToInt(_pixelHeight * (float)_screenWidth / Mathf.Max(1, _screenHeight)));

        _texture = new RenderTexture(width, _pixelHeight, 24)
        {
            name = "TitlePixelRT",
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false
        };
        _texture.Create();

        _camera.targetTexture = _texture;
        // 텍스처 미할당 상태의 흰 화면 노출 방지를 위해 씬에서는 비활성 유지
        if (_output != null)
        {
            _output.texture = _texture;
            _output.enabled = true;
        }
    }

    private void ReleaseTexture()
    {
        if (_texture == null) return;
        _texture.Release();
        Destroy(_texture);
        _texture = null;
    }
}
