using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GameSettings
{
    [Header("Player Colors")]
    public static Color[] playerColors = {Color.red, Color.magenta, Color.green, Color.yellow};

    [Header("Point System")]
    public static int[] scores = new int[4];
    public static int roundsPlayed;
    public static int totalRounds = 3;



    public static void ResetMatch()
    {

        scores = new int[4];
        roundsPlayed = 0;

    }

    public struct PlayerSlot
    {
        public string scheme;
        public InputDevice device;
    }

    public static readonly List<PlayerSlot> Players = new List<PlayerSlot>();
}
