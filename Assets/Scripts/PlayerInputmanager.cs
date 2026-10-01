using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputmanager : MonoBehaviour
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private bool wasdJoined = false;
    private bool arrowJoined = false;
    private bool gamepadJoined = false;

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (!wasdJoined && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            var player = PlayerInput.Instantiate(playerPrefab, controlScheme:"WASD", pairWithDevice: Keyboard.current);

            if (spawnPoints.Length > 0)
            {
                player.transform.position = spawnPoints[0].position;
            }
            wasdJoined = true;
        }
        if (!arrowJoined && Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            var player = PlayerInput.Instantiate(playerPrefab, controlScheme:"Arrows", pairWithDevice: Keyboard.current);

            if (spawnPoints.Length > 1)
            {
                player.transform.position = spawnPoints[1].position;
            }
            arrowJoined = true;
        }


        foreach (var gamePad in Gamepad.all)
        {
            if (gamePad.buttonSouth.wasPressedThisFrame && !gamepadJoined)
            {
                PlayerInput.Instantiate(playerPrefab, 
                    controlScheme: "Gamepad", 
                    pairWithDevice: gamePad);
                gamepadJoined = true;
            }
        }

    }


}
