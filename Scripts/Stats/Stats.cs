using System;
using UnityEngine;

/// <summary>
/// Inspector의 능력치 정의와 캐릭터별 런타임 능력치를 연결한다.
/// 계산은 RuntimeStats에 맡기고 생성과 수명 종료만 책임진다.
/// </summary>
public sealed class Stats : MonoBehaviour
{
    [Tooltip("런타임 능력치를 생성할 때 복사하는 캐릭터 기본 능력치")]
    [SerializeField] private StatsSO _statsDefinition;

    private RuntimeStats _runtimeStats;

    public RuntimeStats RuntimeStats => _runtimeStats;

    /// <summary>
    /// 연결된 정의로 새로운 런타임 능력치를 생성한다.
    /// 풀 재대여처럼 이전 수명이 끝난 객체는 먼저 ResetRuntimeStats로 참조를 정리해야 한다.
    /// </summary>
    public void Initialize()
    {
        if (_statsDefinition == null)
        {
            throw new InvalidOperationException($"{nameof(Stats)}({name}): StatsSO가 설정되지 않았습니다.");
        }

        _runtimeStats = new RuntimeStats();
        _runtimeStats.Initialize(_statsDefinition);
    }

    /// <summary>
    /// 캐릭터 수명이 끝날 때 런타임 능력치 참조를 해제한다.
    /// </summary>
    public void ResetRuntimeStats()
    {
        _runtimeStats = null;
    }
}
