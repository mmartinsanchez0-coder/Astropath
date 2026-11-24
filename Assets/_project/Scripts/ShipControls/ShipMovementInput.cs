using UnityEngine;
using UnityEngine.InputSystem;


public class ShipMovementInput: MonoBehaviour
{
    [SerializeField] ShipInputManager.InputType _inputType = ShipInputManager.InputType.HumanDesktop;

    [Header("Input Actions")]
    [SerializeField] InputActionReference thrustAction;

    public IMovementControls MovementControls { get; private set; }

    void OnEnable()
    {
        if (thrustAction != null && thrustAction.action != null)
        {
            thrustAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (thrustAction != null && thrustAction.action != null)
        {
            thrustAction.action.Disable();
        }
    }
    void Start()
    {
        MovementControls = ShipInputManager.GetInputControls(_inputType);

        if (MovementControls is DesktopMovementControls desktop)
        {
            desktop.BindActions(thrustAction != null ? thrustAction.action : null);
        }
    }   

    void OnDestroy()
    {
        MovementControls = null;
    }
}
