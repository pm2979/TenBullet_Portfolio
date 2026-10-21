using System;
using UnityEngine;

/// <summary>
/// Boss Run의 마지막 Combat에서만 활성화되는 Boss Arena 진입 상호작용. Edgar Door/Connection이
/// 아니며(dungeon_system-design.md 2.2, 4.2a), Runtime이 Portal 대상으로 확정한 단 하나의 Room에서만
/// 상호작용을 수락한다. UI·목표·A* 판단은 소유하지 않고 활성화 상태와 전환 요청 중계만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonBossPortal : MonoBehaviour, IInteractable
{
    [Tooltip("Boss Portal 상호작용을 감지하는 Trigger Collider. 참조·Trigger 구성 확인에만 쓰이며, " +
        "실제 활성화는 이 Component가 있는 GameObject 자체의 활성 상태로 제어한다(기본값은 비활성).")]
    [SerializeField] private Collider2D _interactionTrigger;

    private Action _activationRequested;
    private bool _requestInFlight;

    // GameObject가 비활성이면 Physics2D가 Collider를 질의에서 자동으로 제외하므로, 활성화는 Collider
    // 자체가 아니라 이 GameObject의 활성 상태로 제어한다(Prefab 저작 시 기본 비활성으로 둔다).
    public bool IsActive => gameObject.activeInHierarchy;

    // Prefab 구성이 상호작용 감지에 필요한 참조를 모두 갖췄는지 확인한다.
    public bool HasValidConfiguration()
    {
        return _interactionTrigger != null && _interactionTrigger.isTrigger;
    }

    // Runtime이 이 Room을 Boss Portal 대상으로 확정했을 때만 호출해 상호작용을 켠다.
    internal void Activate(Action activationRequested)
    {
        _activationRequested = activationRequested;
        _requestInFlight = false;
        gameObject.SetActive(true);
    }

    // 활성 상태에서 한 번만 전환 요청을 중계한다. 중복 Interact는 무시한다(FR-14).
    public void Interact()
    {
        if (!IsActive || _requestInFlight || _activationRequested == null)
        {
            return;
        }

        _requestInFlight = true;
        _activationRequested.Invoke();
    }
}
