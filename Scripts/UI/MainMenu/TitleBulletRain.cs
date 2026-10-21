using UnityEngine;

/// <summary>
/// 타이틀 배경 연출: 3D 총알이 천천히 회전하며 낙하
/// 화면 아래로 벗어난 총알은 상단으로 재배치하여 무한 반복
/// </summary>
public class TitleBulletRain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Camera _camera;
    [Tooltip("지정 시 모델 원본 머티리얼 대신 사용 (도트 스타일 셰이더 등)")]
    [SerializeField] private Material _materialOverride;

    [Header("Spawn")]
    [Tooltip("동시에 표시할 총알 수")]
    [SerializeField, Min(1)] private int _count = 45;
    [Tooltip("카메라로부터의 배치 거리 범위 (깊이감)")]
    [SerializeField] private Vector2 _depthRange = new(3f, 18f);
    [Tooltip("모델 원본 크기와 무관하게 맞출 총알 길이 (월드 단위)")]
    [SerializeField, Min(0.01f)] private float _targetLength = 0.5f;
    [Tooltip("개별 총알 크기 배율 범위")]
    [SerializeField] private Vector2 _scaleJitter = new(0.85f, 1.15f);

    [Header("Motion")]
    [Tooltip("낙하 속도 범위 (월드 단위/초)")]
    [SerializeField] private Vector2 _fallSpeed = new(0.2f, 0.6f);
    [Tooltip("회전 속도 범위 (도/초)")]
    [SerializeField] private Vector2 _spinSpeed = new(10f, 40f);
    [Tooltip("좌우 흔들림 속도")]
    [SerializeField, Min(0f)] private float _drift = 0.1f;
    [Tooltip("일시정지(timeScale 0) 중에도 연출 유지")]
    [SerializeField] private bool _useUnscaledTime = true;

    private sealed class Bullet
    {
        public Transform Pivot;
        public float Depth;
        public float Speed;
        public float Spin;
        public float Phase;
        public Vector3 Axis;
    }

    private Bullet[] _bullets;
    private float _margin;
    private float _tanHalfFov;

    private void Start()
    {
        if (_camera == null) _camera = Camera.main;
        if (_bulletPrefab == null || _camera == null)
        {
            Debug.LogWarning("TitleBulletRain: 총알 프리팹 또는 카메라 미지정으로 연출 비활성화", this);
            enabled = false;
            return;
        }

        _tanHalfFov = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _margin = _targetLength * 2f;
        Spawn();
    }

    private void Spawn()
    {
        // 모델 원본 크기·피벗 편차 보정용 1회 측정
        // 임포트 회전 유지를 위해 위치만 원점으로 이동
        GameObject probe = Instantiate(_bulletPrefab);
        probe.transform.position = Vector3.zero;
        Renderer[] renderers = probe.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("TitleBulletRain: 총알 프리팹에 Renderer가 없음", this);
            DestroyImmediate(probe);
            enabled = false;
            return;
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Vector3 prefabScale = probe.transform.localScale;
        Quaternion prefabRotation = probe.transform.localRotation;
        float length = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float normalize = length > 0f ? _targetLength / length : 1f;
        Vector3 centerOffset = -bounds.center * normalize;
        // 원점 위치로 1프레임 노출되는 것을 막기 위한 즉시 제거
        DestroyImmediate(probe);

        _bullets = new Bullet[_count];
        for (int i = 0; i < _count; i++)
        {
            // 회전 중심을 모델 중앙에 두기 위한 부모 피벗
            Transform pivot = new GameObject($"Bullet_{i}").transform;
            pivot.SetParent(transform, false);
            pivot.localScale = Vector3.one * Random.Range(_scaleJitter.x, _scaleJitter.y);

            GameObject model = Instantiate(_bulletPrefab, pivot);
            foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
            if (_materialOverride != null) ApplyMaterialOverride(model);
            model.transform.localScale = prefabScale * normalize;
            model.transform.localPosition = centerOffset;
            model.transform.localRotation = prefabRotation;

            Bullet bullet = new() { Pivot = pivot };
            Respawn(bullet, true);
            _bullets[i] = bullet;
        }
    }

    private void ApplyMaterialOverride(GameObject model)
    {
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = _materialOverride;
            r.sharedMaterials = materials;
        }
    }

    private void Respawn(Bullet bullet, bool anywhere)
    {
        bullet.Depth = Random.Range(_depthRange.x, _depthRange.y);
        float halfHeight = HalfHeight(bullet.Depth);
        float halfWidth = halfHeight * _camera.aspect;
        float x = Random.Range(-halfWidth, halfWidth) * 1.1f;
        float y = anywhere ? Random.Range(-halfHeight, halfHeight) : halfHeight + _margin;

        Transform cam = _camera.transform;
        bullet.Pivot.SetPositionAndRotation(
            cam.position + cam.forward * bullet.Depth + cam.right * x + cam.up * y,
            Random.rotation);
        bullet.Speed = Random.Range(_fallSpeed.x, _fallSpeed.y);
        bullet.Spin = Random.Range(_spinSpeed.x, _spinSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        bullet.Axis = Random.onUnitSphere;
        bullet.Phase = Random.Range(0f, 100f);
    }

    private float HalfHeight(float depth) => depth * _tanHalfFov;

    private void Update()
    {
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float time = _useUnscaledTime ? Time.unscaledTime : Time.time;
        Transform cam = _camera.transform;
        Vector3 down = -cam.up;
        Vector3 right = cam.right;
        Vector3 camPos = cam.position;

        for (int i = 0; i < _bullets.Length; i++)
        {
            Bullet bullet = _bullets[i];
            Transform pivot = bullet.Pivot;

            Vector3 move = down * bullet.Speed + right * (Mathf.Sin(time * 0.5f + bullet.Phase) * _drift);
            pivot.position += move * dt;
            pivot.Rotate(bullet.Axis, bullet.Spin * dt, Space.World);

            float localY = Vector3.Dot(pivot.position - camPos, cam.up);
            if (localY < -(HalfHeight(bullet.Depth) + _margin)) Respawn(bullet, false);
        }
    }
}
