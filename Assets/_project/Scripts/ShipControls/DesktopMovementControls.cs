using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class DesktopMovementControls: MovementControlsBase
{
    const float MousePixelsForFullDeflection = 15f;

     private InputAction _thrustAction;

     public void BindActions(InputAction thrustAction)
     {
         _thrustAction = thrustAction;
     }

     public override float YawAmount
    {
        get
        {
            if (Mouse.current == null)
            {
                return 0f;
            }
            return Mathf.Clamp(Mouse.current.delta.ReadValue().x / MousePixelsForFullDeflection, -1f, 1f);
            
        }
    }

    public override float PitchAmount
    {
        get
        {
            if (Mouse.current == null)
            {
                return 0f;
            }
            return -Mathf.Clamp(Mouse.current.delta.ReadValue().y / MousePixelsForFullDeflection, -1f, 1f);
        }
    }

    public override float RollAmount
    {
        get
        {
            if (Keyboard.current == null)
            {
                return 0f;
            }

            float q = Keyboard.current.qKey.isPressed ? 1f : 0f;
            float e = Keyboard.current.eKey.isPressed ? 1f : 0f;
            return q - e;
        }
    }

    public override float ThrustAmount
    {
        get
        {
            return _thrustAction?.ReadValue<float>() ?? 0f;
        }
    }
    
} 
