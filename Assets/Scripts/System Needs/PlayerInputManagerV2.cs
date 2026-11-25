using System;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerInputManagerV2 : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    public static event Action<PlayerInput> OnPlayerJoined;


    private void Start()
    {
        var players = GameSettings.Players;

        // Fallback so you can still press Play directly from the game scene
        if (players.Count == 0)
        {
            Debug.LogWarning("No lobby players found, spawning WASD + Arrows for testing.");
            Spawn(0, "WASD", Keyboard.current);
            Spawn(1, "Arrows", Keyboard.current);
            return;
        }

        for (int i = 0; i < players.Count; i++)
            Spawn(i, players[i].scheme, players[i].device);
    }

    private void Spawn(int index, string scheme, InputDevice device)
    {
        var player = PlayerInput.Instantiate(playerPrefab,
            controlScheme: scheme, pairWithDevice: device);

        if (index < spawnPoints.Length)
        {
            player.transform.position = spawnPoints[index].position;
            Physics.SyncTransforms();
        }

        OnPlayerJoined?.Invoke(player);
    }
}