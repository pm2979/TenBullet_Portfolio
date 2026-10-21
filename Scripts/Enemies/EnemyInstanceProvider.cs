using System;
using UnityEngine;

/// <summary>PoolManager를 통해 Enemy를 대여·초기화·활성화하고 반환 요청을 연결하는 조립 경계</summary>
// 대여 실패는 구조화된 결과로 반환하며 보상·Room 진행·AI 규칙은 처리하지 않음
public sealed class EnemyInstanceProvider
{
    private readonly PoolManager _poolManager;

    public EnemyInstanceProvider(PoolManager poolManager)
    {
        _poolManager = poolManager;
    }

    /// <summary>Archetype Prefab을 비활성 대여한 뒤 Context 초기화와 반환 구독을 마치고 활성화</summary>
    public EnemyRentResult Rent(EnemyArchetypeSO archetype, EnemySpawnContext spawnContext, Vector3 position, Quaternion rotation)
    {
        return RentWithContextFactory(archetype, _ => spawnContext, position, rotation);
    }

    /// <summary>대여된 Actor로 Context를 만든 뒤 동일한 초기화·활성화 순서를 수행</summary>
    public EnemyRentResult RentWithContextFactory(
        EnemyArchetypeSO archetype,
        Func<EnemyActor, EnemySpawnContext> spawnContextFactory,
        Vector3 position,
        Quaternion rotation)
    {
        if (archetype == null || archetype.EnemyPrefab == null)
        {
            Debug.LogError("EnemyInstanceProvider.Rent: archetype 또는 EnemyPrefab이 null입니다.");
            return EnemyRentResult.Failure(EnemyRentFailureReason.InvalidArchetype);
        }

        if (_poolManager == null)
        {
            Debug.LogError("EnemyInstanceProvider.Rent: PoolManager가 없습니다.");
            return EnemyRentResult.Failure(EnemyRentFailureReason.PoolManagerMissing);
        }

        Component rented = _poolManager.RentComponent(archetype.EnemyPrefab, position, rotation);

        if (rented == null)
        {
            return EnemyRentResult.Failure(EnemyRentFailureReason.PoolRentFailed);
        }

        if (!(rented is EnemyActor actor))
        {
            _poolManager.ReturnComponent(rented);
            return EnemyRentResult.Failure(EnemyRentFailureReason.TypeMismatch);
        }

        if (spawnContextFactory == null)
        {
            return FailInitialization(actor, archetype, "spawnContextFactory가 null입니다.");
        }

        EnemySpawnContext spawnContext;

        try
        {
            spawnContext = spawnContextFactory(actor);
        }
        catch (Exception exception)
        {
            return FailInitialization(actor, archetype, $"SpawnContext 생성에 실패했습니다. {exception}");
        }

        bool isInitialized;

        try
        {
            isInitialized = actor.Initialize(archetype, spawnContext);
        }
        catch (Exception exception)
        {
            // Actor 내부 예외도 일반 초기화 실패와 같은 반환 경계로 처리
            return FailInitialization(actor, archetype, $"Actor 초기화 중 예외가 발생했습니다. {exception}");
        }

        if (!isInitialized)
        {
            // Actor가 남긴 구체적 초기화 오류의 중복 기록 방지
            return ReturnFailedRental(actor);
        }

        actor.ReturnRequested += HandleActorReturnRequested;
        actor.gameObject.SetActive(true);

        return EnemyRentResult.Success(actor);
    }

    // Factory와 예외 실패에 Prefab 문맥을 남긴 뒤 공통 반환 경계로 전달
    private EnemyRentResult FailInitialization(EnemyActor actor, EnemyArchetypeSO archetype, string reason)
    {
        Debug.LogError($"EnemyInstanceProvider.Rent: Prefab '{archetype.EnemyPrefab.name}'의 대여를 취소합니다. {reason}", actor);

        return ReturnFailedRental(actor);
    }

    // 비활성 초기화 실패 객체를 명시적으로 정리해 Pool로 정확히 한 번 반환
    private EnemyRentResult ReturnFailedRental(EnemyActor actor)
    {
        actor.ReturnRequested -= HandleActorReturnRequested;
        actor.PrepareForReturn();
        _poolManager.ReturnComponent(actor);

        return EnemyRentResult.Failure(EnemyRentFailureReason.InitializationFailed);
    }

    /// <summary>Encounter 취소나 Scene 전환으로 생존 Enemy의 강제 반환 요청</summary>
    public void Return(EnemyActor actor, EnemyReturnReason returnReason)
    {
        if (actor == null)
        {
            return;
        }

        actor.Cancel(returnReason);
    }

    // 구독 해제 후 Actor 정리를 재확인하고 PoolManager에 반환해 재진입 방지
    private void HandleActorReturnRequested(EnemyActor actor, EnemyReturnReason reason)
    {
        actor.ReturnRequested -= HandleActorReturnRequested;
        actor.PrepareForReturn();
        _poolManager.ReturnComponent(actor);
    }
}
