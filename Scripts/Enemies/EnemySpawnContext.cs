using UnityEngine;

/// <summary>Spawn 시 EnemyActor에 주입할 Target·행동 설정·Navigation을 전달</summary>
// Enemy 내부의 Scene 검색과 Navigation 구현 자동 선택을 방지하는 조립 경계
public readonly struct EnemySpawnContext
{
    public readonly Transform Target;
    public readonly EnemyRuntimeConfig RuntimeConfig;

    /// <summary>이번 대여가 사용할 Navigation 구현이며 null이면 경로 이동 불가</summary>
    public readonly IEnemyNavigation Navigation;

    public EnemySpawnContext(
        Transform target, EnemyRuntimeConfig runtimeConfig, IEnemyNavigation navigation = null)
    {
        Target = target;
        RuntimeConfig = runtimeConfig;
        Navigation = navigation;
    }
}
