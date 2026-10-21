using UnityEngine;

/// <summary>
/// 기존 풀에서 투사체를 대여하고 공통 발사 계약으로 초기화합니다.
/// </summary>
public sealed class ProjectileSpawner
{
    /// <summary>
    /// 유효한 요청만 풀에 전달하며, 대여한 컴포넌트가 투사체가 아니면 즉시 반환하고 실패 원인을 알립니다.
    /// </summary>
    public ProjectileSpawnResult TrySpawn(GameObject prefab, ProjectileLaunchRequest request)
    {
        if (request.Definition == null || prefab == null || request.Range <= 0f)
        {
            return ProjectileSpawnResult.Failed(ProjectileSpawnFailure.InvalidRequest);
        }

        if (!PoolManager.HasInstance)
        {
            return ProjectileSpawnResult.Failed(ProjectileSpawnFailure.PoolUnavailable);
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(prefab, request.SpawnPosition, Quaternion.identity);

        if (rentedComponent == null)
        {
            return ProjectileSpawnResult.Failed(ProjectileSpawnFailure.PoolUnavailable);
        }

        IProjectile projectile = rentedComponent as IProjectile;

        if (projectile == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return ProjectileSpawnResult.Failed(ProjectileSpawnFailure.ComponentTypeMismatch);
        }

        projectile.Launch(request);

        return ProjectileSpawnResult.Success(projectile);
    }
}
