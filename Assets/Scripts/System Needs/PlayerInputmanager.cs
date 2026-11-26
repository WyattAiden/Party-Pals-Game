using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputmanager : MonoBehaviour
{
    [SerializeField] public GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private bool wasdJoined = false;
    private bool arrowJoined = false;
    private readonly HashSet<Gamepad> joinedGamepads = new HashSet<Gamepad>();
    private int nextGamepadSpawn = 2; // spawn points 0 and 1 belong to the keyboard players

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (!wasdJoined && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                var player = PlayerInput.Instantiate(playerPrefab, controlScheme: "WASD", pairWithDevice: Keyboard.current);
                if (spawnPoints.Length > 0)
                {
                    player.transform.position = spawnPoints[0].position;
                }
                wasdJoined = true;
            }

            if (!arrowJoined && Keyboard.current.rightShiftKey.wasPressedThisFrame)
            {
                var player = PlayerInput.Instantiate(playerPrefab, controlScheme: "Arrows", pairWithDevice: Keyboard.current);
                if (spawnPoints.Length > 1)
                {
                    player.transform.position = spawnPoints[1].position;
                }
                arrowJoined = true;
            }
        }

        foreach (var gamePad in Gamepad.all)
        {
            if (joinedGamepads.Contains(gamePad)) continue;

            if (gamePad.buttonSouth.wasPressedThisFrame)
            {
                var player = PlayerInput.Instantiate(playerPrefab,
                    controlScheme: "Gamepad",
                    pairWithDevice: gamePad);

                if (nextGamepadSpawn < spawnPoints.Length)
                {
                    player.transform.position = spawnPoints[nextGamepadSpawn].position;
                }

                nextGamepadSpawn++;
                joinedGamepads.Add(gamePad);
            }
        }
    }
}