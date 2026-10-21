using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ExitDungeon Hold 입력의 완료(performed) 신호만 한 번 노출하는 좁은 입력 경계.
/// started/canceled, Hold 진행 중 상태는 바깥에 전달하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonExitInputReader : MonoBehaviour
{
    [Tooltip("Hold(1초) Interaction이 설정된 ExitDungeon Button Action.")]
    [SerializeField] private InputActionReference _exitDungeonAction;

    // 외부 PlayerInput 등이 이미 활성화한 Action을 임의로 비활성화하지 않기 위한 소유권 기록
    private bool _ownsAction;

    /// <summary>Hold 완료(performed) 시 정확히 한 번 발행된다.</summary>
    public event Action ExitRequested;

    private void OnEnable()
    {
        if (_exitDungeonAction == null)
        {
            return;
        }

        if (!_exitDungeonAction.action.enabled)
        {
            _exitDungeonAction.action.Enable();
            _ownsAction = true;
        }

        _exitDungeonAction.action.performed += HandlePerformed;
    }

    private void OnDisable()
    {
        if (_exitDungeonAction == null)
        {
            return;
        }

        _exitDungeonAction.action.performed -= HandlePerformed;

        if (_ownsAction)
        {
            _exitDungeonAction.action.Disable();
            _ownsAction = false;
        }
    }

    private void HandlePerformed(InputAction.CallbackContext context)
    {
        ExitRequested?.Invoke();
    }
}
