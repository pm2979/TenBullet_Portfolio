using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System 액션을 읽어 이동/조준/발사/회피/패링/상호작용 입력 값을 노출하는 읽기 전용 입력 리더다.
/// Action 활성화·비활성화는 PlayerController가 Player Action Map 단일 Owner로 관리하며,
/// 이 Reader는 Action을 Enable/Disable하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatInputReader : MonoBehaviour
{
    [Header("입력 액션")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _aimAction;
    [SerializeField] private InputActionReference _fireAction;
    [SerializeField] private InputActionReference _dodgeAction;
    [SerializeField] private InputActionReference _parryAction;
    [SerializeField] private InputActionReference _interactAction;

    private InputAction _cachedMoveAction;
    private InputAction _cachedAimAction;
    private InputAction _cachedFireAction;
    private InputAction _cachedDodgeAction;
    private InputAction _cachedParryAction;
    private InputAction _cachedInteractAction;

    public Vector2 Move => ReadValue(_cachedMoveAction);
    public Vector2 AimScreenPosition => ReadValue(_cachedAimAction);
    public bool FireHeld => ReadPressed(_cachedFireAction);
    public bool DodgePressed => ReadWasPressedThisFrame(_cachedDodgeAction);
    public bool ParryPressed => ReadWasPressedThisFrame(_cachedParryAction);
    public bool UseItemPressed => ReadWasPerformedThisFrame(_cachedInteractAction);

    private void Awake()
    {
        _cachedMoveAction = ResolveAction(_moveAction, nameof(_moveAction));
        _cachedAimAction = ResolveAction(_aimAction, nameof(_aimAction));
        _cachedFireAction = ResolveAction(_fireAction, nameof(_fireAction));
        _cachedDodgeAction = ResolveAction(_dodgeAction, nameof(_dodgeAction));
        _cachedParryAction = ResolveAction(_parryAction, nameof(_parryAction));
        _cachedInteractAction = ResolveAction(_interactAction, nameof(_interactAction));
    }

    // Reference 필드 누락과 reference.action 해석 실패(Action 삭제·재생성 등으로 ID가 끊긴 경우)를
    // 모두 여기서 한 번만 진단한다. 이후 프레임 조회 경로(Move 등)는 캐시된 Action의 null 검사만 한다
    private InputAction ResolveAction(InputActionReference reference, string fieldName)
    {
        InputAction action = reference != null ? reference.action : null;

        if (action == null)
        {
            Logger.LogWarning($"CombatInputReader의 {fieldName}이(가) 비어 있거나 연결된 Action을 찾을 수 없습니다.", this);
        }

        return action;
    }

    private static Vector2 ReadValue(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool ReadPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private static bool ReadWasPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private static bool ReadWasPerformedThisFrame(InputAction action)
    {
        return action != null && action.WasPerformedThisFrame();
    }
}
