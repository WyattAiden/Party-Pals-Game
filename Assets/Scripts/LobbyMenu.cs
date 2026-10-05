using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LobbyMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private TMP_Text lobbyLabel;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 1;

    private void Start()
    {
        GameSettings.Players.Clear();
        UpdateLabel();
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
        UpdateLabel();
    }

    public void StartGame()
    {
        if (GameSettings.Players.Count < minPlayersToStart) return;
        SceneManager.LoadScene(gameSceneName);
    }

    private void UpdateLabel()
    {
        if (lobbyLabel == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Space = WASD   |   Right Shift = Arrows   |   A = Gamepad");
        sb.AppendLine();

        for (int i = 0; i < maxPlayers; i++)
        {
            sb.AppendLine(i < GameSettings.Players.Count
                ? $"Player {i + 1}: {GameSettings.Players[i].scheme}  (joined)"
                : $"Player {i + 1}: waiting...");
        }

        sb.AppendLine();
        sb.AppendLine("Press Enter / Start (or click Start) to begin");
        lobbyLabel.text = sb.ToString();
    }
}