using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 1;

    [Header("Join indicators")]
    [SerializeField] private Image[] playerIcons;
    [SerializeField] private Color emptyColor = Color.gray;

    private void Start()
    {
        GameSettings.Players.Clear();
        UpdateChecks();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                TryJoin("WASD", keyboard);

            if (keyboard.rightShiftKey.wasPressedThisFrame)
                TryJoin("Arrows", keyboard);

            if (keyboard.enterKey.wasPressedThisFrame)
                StartGame();
        }

        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
                TryJoin("Gamepad", gamepad);

            if (gamepad.startButton.wasPressedThisFrame)
                StartGame();
        }
    }

    private void TryJoin(string scheme, InputDevice device)
    {
        if (GameSettings.Players.Count >= maxPlayers) return;
        if (GameSettings.Players.Exists(p => p.scheme == scheme && p.device == device)) return;

        GameSettings.Players.Add(new GameSettings.PlayerSlot { scheme = scheme, device = device });
        UpdateChecks();
    }
    public void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void StartGame()
    {
        if (GameSettings.Players.Count < minPlayersToStart) return;
        SceneManager.LoadScene(gameSceneName);
    }

    private void UpdateChecks()
    {
        for (int i = 0; i <GameSettings.Players.Count; i++)
        {
            bool isJoined = i < GameSettings.Players.Count;
            playerIcons[i].color = isJoined ? GameSettings.playerColors[i] : emptyColor;
        }
    }



}