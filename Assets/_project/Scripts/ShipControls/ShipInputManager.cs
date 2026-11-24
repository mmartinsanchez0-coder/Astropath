using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class ShipInputManager : MonoBehaviour
{
    
    public enum InputType
    {
        HumanDesktop,
        HumanMobile,
        Bot
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public static IMovementControls GetInputControls(InputType inputType)
    {
        return inputType switch
        {
            InputType.HumanDesktop => new DesktopMovementControls(),
            InputType.HumanMobile => null,
            InputType.Bot => null,
            _ => throw new ArgumentOutOfRangeException(nameof(inputType), inputType, null)
        };
    }

}
