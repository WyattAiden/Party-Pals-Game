using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
    [SerializeField] List<PlayerInput> players;
    [SerializeField] private float WinScreenDelay = 3f;
    [SerializeField] private int MinimumPlayersBeforeWin = 2;

    [Header("Win screen")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private GameObject firstSelectedButton;
    [SerializeField] private string menuSceneName = "StartScreen";

    private bool roundOver;

    private void OnEnable()
    {
        PlayerInputManagerV2.OnPlayerJoined += OnPlayerJoined;
    }

    private void OnDisable()
    {
        PlayerInputManagerV2.OnPlayerJoined -= OnPlayerJoined;

        foreach (var player in players)
        {
            if (player == null) continue;
            if (player.TryGetComponent(out Health health))
                health.Died -= OnPlayerDied;
        }
    }

    private void OnPlayerJoined(PlayerInput playerInput)
    {
        players.Add(playerInput);
        Debug.Log($"Player joined: {playerInput.name}. Total players: {players.Count}");

        if (playerInput.TryGetComponent(out Health health))
            health.Died += OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        if (roundOver) return;                                // already counting down
        if (players.Count < MinimumPlayersBeforeWin) return;  // solo-testing guard
        if (GetAlivePlayers().Count > 1) return;              // 2+ alive, keep playing

        roundOver = true;
        StartCoroutine(EndRoundAfterDelay());
    }

    private IEnumerator EndRoundAfterDelay()
    {
        // Wait so the final hit is visible and simultaneous deaths settle
        yield return new WaitForSeconds(WinScreenDelay);

        var alive = GetAlivePlayers();
        if (alive.Count == 1)
        {
            ShowWinScreen($"Player {alive[0].playerIndex + 1} wins!");
        }
        else
        {
            Debug.Log("Draw - restarting round");
            PlayAgain();
        }
    }

    private void ShowWinScreen(string message)
    {
        // Stop everyone moving and shooting
        foreach (var player in players)
        {
            if (player != null) player.DeactivateInput();
        }

        winnerText.text = message;
        winScreen.SetActive(true);

        // Lets a gamepad or keyboard navigate the buttons straight away
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    // Hook these up to the buttons' OnClick events in the Inspector
    public void PlayAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    private List<PlayerInput> GetAlivePlayers()
    {
        var alive = new List<PlayerInput>();
        foreach (var player in players)
        {
            if (player == null) continue;
            if (player.TryGetComponent(out Health health) && !health.IsDead)
                alive.Add(player);
        }
        return alive;
    }
}