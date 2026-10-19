using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GameSettings
{
    public static Color[] playerColors = {Color.red, Color.magenta, Color.green, Color.yellow}; 
    public struct PlayerSlot
    {
        public string scheme;
        public InputDevice device;
    }

    public static readonly List<PlayerSlot> Players = new List<PlayerSlot>();
}
