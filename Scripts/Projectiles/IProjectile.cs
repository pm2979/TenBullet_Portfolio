using UnityEngine;

/// <summary>
/// 발사 요청으로 초기화할 수 있는 투사체의 공통 계약입니다.
/// </summary>
public interface IProjectile
{
    /// <summary>
    /// 발사 시점에 확정된 요청으로 이번 사용 주기의 상태를 초기화하고 투사체를 활성화합니다.
    /// </summary>
    void Launch(ProjectileLaunchRequest request);

    /// <summary>투사체가 속한 게임 오브젝트를 반환합니다.</summary>
    GameObject GetGameObject();
}
