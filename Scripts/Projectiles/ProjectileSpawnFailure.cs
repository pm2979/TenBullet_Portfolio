/// <summary>
/// 투사체 생성이 완료되지 못한 원인을 구분합니다.
/// </summary>
public enum ProjectileSpawnFailure
{
    /// <summary>생성이 성공했습니다.</summary>
    None,
    /// <summary>풀 관리자가 없거나 요청한 프리팹을 대여할 수 없습니다.</summary>
    PoolUnavailable,
    /// <summary>대여한 컴포넌트가 투사체 계약을 구현하지 않습니다.</summary>
    ComponentTypeMismatch,
    /// <summary>투사체 정의·프리팹이 없거나 사거리가 유효하지 않습니다.</summary>
    InvalidRequest
}
