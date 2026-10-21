using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 물리 범위에서 상호작용 후보를 수집하고 가장 가까운 대상을 사용한다.
/// </summary>
[RequireComponent(typeof(Player))]
[DisallowMultipleComponent]
public class PlayerInteractionSensor : MonoBehaviour
{
    [Tooltip("상호작용 가능한 대상을 탐색할 반경")]
    [FormerlySerializedAs("interactionRadius")]
    [SerializeField] private float _interactionRadius = 2f;

    [Tooltip("상호작용 탐색에 포함할 레이어")]
    [FormerlySerializedAs("interactionLayerMask")]
    [SerializeField] private LayerMask _interactionLayerMask = ~0;

    // 매 호출 할당을 피하기 위해 탐지 결과를 고정 크기 버퍼에 보관한다.
    private const int MaxCandidates = 16;

    private readonly Collider2D[] _colliderBuffer = new Collider2D[MaxCandidates];
    private readonly List<InteractionCandidate> _candidateBuffer = new List<InteractionCandidate>(MaxCandidates);
    private readonly List<IInteractable> _useableBuffer = new List<IInteractable>(MaxCandidates);
    private readonly InteractionSelector _interactionSelector = new InteractionSelector();
    private ContactFilter2D _contactFilter;

    private Player _player;

    private void Awake()
    {
        _player = GetComponent<Player>();

        // 트리거 포함 여부는 프로젝트의 전역 Physics2D 설정을 따른다.
        _contactFilter.SetLayerMask(_interactionLayerMask);
        _contactFilter.useTriggers = Physics2D.queriesHitTriggers;
    }

    /// <summary>
    /// 범위 안에서 가장 가까운 유효 대상을 사용하고 성공 여부를 반환한다.
    /// </summary>
    public bool TryUseNearestInteractable()
    {
        Vector2 originPosition = _player.GetPlayerPosition();

        int colliderCount = Physics2D.OverlapCircle(originPosition, _interactionRadius, _contactFilter, _colliderBuffer);

        _candidateBuffer.Clear();
        _useableBuffer.Clear();

        for (int i = 0; i < colliderCount; i++)
        {
            IInteractable useable = _colliderBuffer[i].GetComponent<IInteractable>();

            _useableBuffer.Add(useable);
            _candidateBuffer.Add(new InteractionCandidate(_colliderBuffer[i].transform.position, useable != null));
        }

        int selectedIndex = _interactionSelector.SelectNearestIndex(originPosition, _interactionRadius, _candidateBuffer);

        if (selectedIndex < 0) return false;

        _useableBuffer[selectedIndex].Interact();
        return true;
    }
}
