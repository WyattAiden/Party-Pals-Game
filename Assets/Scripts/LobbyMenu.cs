using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Join indicators")]
    [SerializeField] private Image[] playerIcons;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private InputSystemUIInputModule uIModule;


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

            if (gamepad.startButton.wasPressedThisFrame && GameSettings.Players.Count >= minPlayersToStart && gamepad == GameSettings.Players[0].device)
                StartGame();
        }
    }

    private void OnDisable()
    {
        uIModule.actionsAsset.devices = null;
    }

    private void TryJoin(string scheme, InputDevice device)
    {
        if (GameSettings.Players.Count >= maxPlayers) return;
        if (GameSettings.Players.Exists(p => p.scheme == scheme && p.device == device)) return;

        GameSettings.Players.Add(new GameSettings.PlayerSlot { scheme = scheme, device = device });
        if (GameSettings.Players.Count == 1)
        {
            uIModule.actionsAsset.devices = new[] { device };
        }
  
        UpdateChecks();
    }
    public void LoadLevel(string sceneName)
    {
        if (GameSettings.Players.Count >= minPlayersToStart)
        {

            SceneManager.LoadScene(sceneName);
        }
        else 
        {
            Debug.Log("Not enough players to start the game.");
        }
        
           

        

    }

    public void StartGame()
    {
        if (GameSettings.Players.Count >= minPlayersToStart)
        {

            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.Log("Not enough players to start the game.");

        }
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