using UnityEngine;

// Implant의 발동 조건과 효과 설정을 보관하는 정의 데이터
[CreateAssetMenu(fileName = "ImplantData_", menuName = "Scriptable Objects/Implants/ImplantData")]
public sealed class ImplantDataSO : ScriptableObject
{
    [Tooltip("이 Implant가 반응하는 무기 적중 종류")]
    [SerializeField] private WeaponHitKind _triggerKind;
    [Tooltip("발동 조건 충족 시 실행할 효과 종류")]
    [SerializeField] private ImplantEffectKind _effectKind;
    [Tooltip("즉시 회복 또는 체력 Pickup 접촉 시 적용할 체력 회복량")]
    [SerializeField] private int _healAmount = 1;
    [Tooltip("발동 조건 충족 시 효과가 실행될 확률(0~1)")]
    [SerializeField, Range(0f, 1f)] private float _activationChance = 1f;
    [Tooltip("체력 Pickup이 유지되는 시간(초)")]
    [SerializeField] private float _pickupLifetime = 3f;
    [Tooltip("이 Implant가 동시에 유지할 수 있는 체력 Pickup 수")]
    [SerializeField] private int _maxActivePickup = 20;

    public WeaponHitKind TriggerKind => _triggerKind;
    public ImplantEffectKind EffectKind => _effectKind;
    public int HealAmount => _healAmount;
    public float ActivationChance => _activationChance;
    public float PickupLifetime => _pickupLifetime;
    public int MaxActivePickup => _maxActivePickup;
}
