using UnityEngine;

// 무기 한 종류의 발사 능력치와 표현 설정을 제공
[CreateAssetMenu(fileName = "WeaponData_", menuName = "Scriptable Objects/Weapons/WeaponData")]
public class WeaponDataSO : ScriptableObject
{
    [Space(5)]
    [Header("무기 기본 정보")]
    public string weaponName;
    [Tooltip("콜라이더 동기화에 사용할 Physics Shape가 포함된 무기 스프라이트")]
    public Sprite weaponSprite;

    [Space(5)]
    [Header("무기 설정")]
    public Vector3 weaponShootPosition;
    public ProjectileDataSO projectileDefinition;
    public EffectDefinitionSO weaponShootSpriteEffect;
    [Tooltip("발사 시 배출할 탄피 효과, 비워두면 배출 생략")]
    public BulletShellEffectSO weaponBulletShellEffect;

    [Space(5)]
    [Header("무기 사운드")]
    [Tooltip("발사 성공 직후 재생할 사운드, 비워두면 재생 생략")]
    public SoundDataSO weaponFireSound;
    [Tooltip("다음 발사 전까지 재생할 후속 사운드, 비워두면 재생 생략")]
    public SoundDataSO weaponPostFireSound;
    [Range(0f, 0.99f)]
    [Tooltip("최종 발사 간격 중 후속 사운드 시작 시점 비율, 0.7은 70% 지점")]
    public float weaponPostFireSoundTiming = 0.7f;

    [Space(5)]
    [Header("무기 동작 값")]
    [Tooltip("발사 사이의 기본 간격(초), 0.2는 초당 5발")]
    public float weaponFireRate = 0.2f;
    [Tooltip("새 연속 입력의 첫 발에 필요한 선충전 시간(초)")]
    public float weaponPrechargeTime = 0f;
    [Tooltip("플레이어가 발사 성공 시 소비할 체력, 다른 소유자에게는 미적용")]
    public int healthCostPerFire = 1;

    [Space(5)]
    [Header("투사체 기본값")]
    [Tooltip("투사체 하나의 기본 피해량, RuntimeStats 보정 전 값")]
    public int weaponDamage = 1;
    [Tooltip("투사체의 기본 사거리, RuntimeStats 보정 전 값")]
    public float weaponRange = 20f;
    [Tooltip("발사당 생성할 투사체 수, 최소 1로 보정")]
    public int weaponProjectileCount = 1;
    [Tooltip("다발 발사의 전체 분산 각도(도), 최소 0으로 보정")]
    public float weaponSpreadAngle = 0f;
    [Tooltip("다발 발사 시 투사체 생성 최소 간격(초)")]
    public float weaponSpawnIntervalMin = 0f;
    [Tooltip("다발 발사 시 투사체 생성 최대 간격(초)")]
    public float weaponSpawnIntervalMax = 0f;
    [Tooltip("투사체의 기본 이동 속도, RuntimeStats 보정 전 값")]
    public float weaponProjectileSpeed = 20f;
    [Tooltip("적중 시 적용할 넉백 속도, 0 이하면 적용 생략")]
    public float weaponKnockbackSpeed = 5f;
    [Tooltip("넉백 유지 시간(초), 0 이하면 적용 생략")]
    public float weaponKnockbackDuration = 0.5f;

    [Space(5)]
    [Header("발사 반동 및 이동 효과")]
    [Tooltip("발사 성공 시 무기 스프라이트가 후퇴하는 거리, 0이면 적용 생략")]
    public float weaponVisualRecoilDistance = 0f;
    [Tooltip("시각 반동이 기준 위치로 복귀하는 데 걸리는 시간(초)")]
    public float weaponVisualRecoilRecoveryTime = 0.1f;
    [Tooltip("발사 성공 시 플레이어에게 적용할 이동 반동 초기 속도, 0이면 적용 생략")]
    public float weaponMovementRecoilSpeed = 0f;
    [Tooltip("이동 반동 속도가 0으로 감쇠하기까지 걸리는 고정 지속 시간(초)")]
    public float weaponMovementRecoilDuration = 0f;
    [Tooltip("발사 후 일반 이동 속도 배율, 1은 감속 없음이며 RuntimeStats 보정 전 값")]
    public float weaponFiringMoveSpeedMultiplier = 1f;
    [Tooltip("발사 후 이동 감속 유지 시간(초), 0이면 적용 생략")]
    public float weaponMoveSlowdownDuration = 0f;

    [Space(5)]
    [Header("발사 카메라 흔들림")]
    [Tooltip("발사 성공 시 적용할 카메라 흔들림, 진폭이 0이면 적용 생략")]
    [SerializeField] private CameraShakeProfile _fireShakeProfile = new(Vector2.zero, 0f, CameraShakeStrength.Light);

    public CameraShakeProfile FireShakeProfile => _fireShakeProfile;

    [Space(5)]
    [Header("임플란트 후보")]
    [Tooltip("무기 장착 시 선택 가능한 임플란트 후보, 최대 3개")]
    public ImplantDataSO[] implantCandidates;

    private void OnValidate()
    {
        ValidateImplantCandidates();

        if (weaponProjectileCount < 1)
        {
            weaponProjectileCount = 1;
        }

        if (weaponSpreadAngle < 0f)
        {
            weaponSpreadAngle = 0f;
        }

        if (weaponSpawnIntervalMin < 0f)
        {
            weaponSpawnIntervalMin = 0f;
        }

        if (weaponSpawnIntervalMax < weaponSpawnIntervalMin)
        {
            weaponSpawnIntervalMax = weaponSpawnIntervalMin;
        }

        if (weaponVisualRecoilDistance < 0f)
        {
            weaponVisualRecoilDistance = 0f;
        }

        if (weaponVisualRecoilRecoveryTime < 0f)
        {
            weaponVisualRecoilRecoveryTime = 0f;
        }

        if (weaponMovementRecoilSpeed < 0f)
        {
            weaponMovementRecoilSpeed = 0f;
        }

        if (weaponMovementRecoilDuration < 0f)
        {
            weaponMovementRecoilDuration = 0f;
        }

        weaponFiringMoveSpeedMultiplier = Mathf.Clamp01(weaponFiringMoveSpeedMultiplier);

        if (weaponMoveSlowdownDuration < 0f)
        {
            weaponMoveSlowdownDuration = 0f;
        }

        weaponPostFireSoundTiming = Mathf.Clamp(weaponPostFireSoundTiming, 0f, 0.99f);
    }

    // 임플란트 후보 수 제한
    private const int MaxImplantCandidateCount = 3;

    // 후보를 최대 3개로 보정하고 비어 있거나 중복된 구성을 경고
    private void ValidateImplantCandidates()
    {
        if (implantCandidates == null || implantCandidates.Length == 0)
        {
            Logger.LogWarning($"WeaponDataSO '{name}': implantCandidates가 비어 있습니다. Implant 미장착 상태로 동작합니다.", this);
            return;
        }

        if (implantCandidates.Length > MaxImplantCandidateCount)
        {
            Logger.LogWarning($"WeaponDataSO '{name}': implantCandidates는 최대 {MaxImplantCandidateCount}개까지만 허용되어 {MaxImplantCandidateCount}개로 보정합니다.", this);
            System.Array.Resize(ref implantCandidates, MaxImplantCandidateCount);
        }

        for (int i = 0; i < implantCandidates.Length; i++)
        {
            if (implantCandidates[i] == null)
            {
                continue;
            }

            for (int j = i + 1; j < implantCandidates.Length; j++)
            {
                if (implantCandidates[i] == implantCandidates[j])
                {
                    Logger.LogWarning($"WeaponDataSO '{name}': implantCandidates에 '{implantCandidates[i].name}'이(가) 중복 등록되어 있습니다.", this);
                }
            }
        }
    }
}

