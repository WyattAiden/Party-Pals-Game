using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GameSettings
{
    [Header("Player Colors")]
    public static Color[] playerColors = { Color.red, Color.magenta, Color.green, Color.yellow, Color.cyan, Color.orange };

    [Header("Point System")]
    public static int[] scores = new int[4];
    public static int roundsPlayed;
    public static int totalRounds = 3;
    public static string[] mapNames = { "WarLevel", "PirateLevel", "MakerLevel", "CityLevel"};

    public static void AddPlayer(string scheme, InputDevice device)
    {       
        int freeColor = 0;
        while (Players.Exists(p => p.colorIndex == freeColor))
            freeColor++;
        Players.Add(new PlayerSlot { scheme = scheme, device = device, colorIndex = freeColor });
    }

    public static void ResetMatch()
    {

        scores = new int[4];
        roundsPlayed = 0;

    }

    public struct PlayerSlot
    {
        public string scheme;
        public InputDevice device;
        public int colorIndex;
    }


    public static readonly List<PlayerSlot> Players = new List<PlayerSlot>();

    public static Color GetColors(int player)
    {

        return playerColors[Players[player].colorIndex];

    }
}
