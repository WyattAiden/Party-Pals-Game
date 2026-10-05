using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private TMP_Text playerCountLabel;
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 4;

    private void Start()
    {
        GameSettings.PlayerCount = Mathf.Clamp(GameSettings.PlayerCount, minPlayers, maxPlayers);
        UpdateLabel();
    }

    public void IncreasePlayers() => ChangeCount(1);
    public void DecreasePlayers() => ChangeCount(-1);

    private void ChangeCount(int delta)
    {
        GameSettings.PlayerCount = Mathf.Clamp(GameSettings.PlayerCount + delta, minPlayers, maxPlayers);
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (playerCountLabel != null)
            playerCountLabel.text = GameSettings.PlayerCount.ToString();
    }

    public void StartGame() => SceneManager.LoadScene(gameSceneName);
    public void QuitGame() => Application.Quit();
}