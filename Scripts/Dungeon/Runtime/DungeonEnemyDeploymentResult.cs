/// <summary>
/// DungeonEnemyDeployer.Deploy의 성공한 Deployment 또는 구조화된 실패를 전달한다.
/// </summary>
public sealed class DungeonEnemyDeploymentResult
{
    public bool Succeeded { get; }
    public DungeonEnemyDeployment Deployment { get; }
    public DungeonEnemyDeploymentFailure Failure { get; }

    internal static DungeonEnemyDeploymentResult Success(DungeonEnemyDeployment deployment)
    {
        return new DungeonEnemyDeploymentResult(true, deployment, DungeonEnemyDeploymentFailure.None);
    }

    internal static DungeonEnemyDeploymentResult Failed(DungeonEnemyDeploymentFailure failure)
    {
        return new DungeonEnemyDeploymentResult(false, null, failure);
    }

    private DungeonEnemyDeploymentResult(
        bool succeeded, DungeonEnemyDeployment deployment, DungeonEnemyDeploymentFailure failure)
    {
        Succeeded = succeeded;
        Deployment = deployment;
        Failure = failure;
    }
}
