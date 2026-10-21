using UnityEngine;

// 장착된 Implant의 적중별 발동 여부와 Shot별 발동 기록을 관리하는 런타임
public sealed class ImplantRuntime
{
    private readonly ImplantDataSO _implantData;
    private readonly ActivatedShotBuffer _activatedShotBuffer = new ActivatedShotBuffer();

    // 장착 시점의 Implant 정의로 발동 판정 상태 구성
    public ImplantRuntime(ImplantDataSO implantData)
    {
        _implantData = implantData;
    }

    // 유효 적중과 발동 확률을 검사하고 성공한 Shot의 최초 발동만 기록
    public bool TryActivate(in WeaponHitEvent hitEvent)
    {
        if (hitEvent.ShotId == 0)
        {
            return false;
        }

        if (hitEvent.Kind != _implantData.TriggerKind)
        {
            return false;
        }

        if (hitEvent.CombatResult.DamageResult.WasTargetAlreadyDead)
        {
            return false;
        }

        if (_activatedShotBuffer.Contains(hitEvent.ShotId))
        {
            return false;
        }

        float activationChance = _implantData.ActivationChance;

        if (activationChance <= 0f)
        {
            return false;
        }

        if (activationChance < 1f && Random.value >= activationChance)
        {
            return false;
        }

        _activatedShotBuffer.Add(hitEvent.ShotId);
        return true;
    }

    // 누적된 Shot별 발동 기록 초기화
    public void Clear()
    {
        _activatedShotBuffer.Clear();
    }
}
