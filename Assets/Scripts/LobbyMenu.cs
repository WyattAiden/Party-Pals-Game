using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LobbyMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 1;

    [Header("Join indicators")]
    [SerializeField] private GameObject wasdCheck;
    [SerializeField] private GameObject arrowsCheck;
    [SerializeField] private GameObject gamepadCheck;
    [SerializeField] private TMP_Text gamepadCountText; // optional, shows "x2" etc.

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

    public void StartGame()
    {
        if (GameSettings.Players.Count < minPlayersToStart) return;
        SceneManager.LoadScene(gameSceneName);
    }

    private void UpdateChecks()
    {
        bool wasd = GameSettings.Players.Exists(p => p.scheme == "WASD");
        bool arrows = GameSettings.Players.Exists(p => p.scheme == "Arrows");
        int gamepads = GameSettings.Players.FindAll(p => p.scheme == "Gamepad").Count;

        if (wasdCheck != null) wasdCheck.SetActive(wasd);
        if (arrowsCheck != null) arrowsCheck.SetActive(arrows);
        if (gamepadCheck != null) gamepadCheck.SetActive(gamepads > 0);

        if (gamepadCountText != null)
            gamepadCountText.text = gamepads > 1 ? $"x{gamepads}" : "";
    }

    public void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

}