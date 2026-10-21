/// <summary>
/// 투사체 생성 성공 여부와 생성된 인스턴스 또는 실패 원인을 함께 전달합니다.
/// </summary>
public readonly struct ProjectileSpawnResult
{
    private ProjectileSpawnResult(bool succeeded, IProjectile projectile, ProjectileSpawnFailure failure)
    {
        Succeeded = succeeded;
        Projectile = projectile;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public IProjectile Projectile { get; }
    public ProjectileSpawnFailure Failure { get; }

    public static ProjectileSpawnResult Success(IProjectile projectile)
    {
        return new ProjectileSpawnResult(true, projectile, ProjectileSpawnFailure.None);
    }

    public static ProjectileSpawnResult Failed(ProjectileSpawnFailure failure)
    {
        return new ProjectileSpawnResult(false, null, failure);
    }
}
