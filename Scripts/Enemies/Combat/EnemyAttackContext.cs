using UnityEngine;

/// <summary>공격 한 건의 Definition과 시전자 정보를 Action에 전달하는 실행 Context</summary>
public readonly struct EnemyAttackContext
{
    public readonly EnemyAttackDefinitionSO Definition;
    public readonly Transform Caster;
    public readonly Faction SourceFaction;
    public readonly Component Attacker;

    public EnemyAttackContext(
        EnemyAttackDefinitionSO definition, Transform caster, Faction sourceFaction, Component attacker)
    {
        Definition = definition;
        Caster = caster;
        SourceFaction = sourceFaction;
        Attacker = attacker;
    }
}
