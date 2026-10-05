using System.Collections.Generic;
using UnityEngine.InputSystem;

public static class GameSettings
{
    public struct PlayerSlot
    {
        public string scheme;
        public InputDevice device;
    }

    public static readonly List<PlayerSlot> Players = new List<PlayerSlot>();
}
