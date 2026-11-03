using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundManager : MonoBehaviour
{
    [Header("Round settings")]
    [SerializeField] List<PlayerInput> players;
    [SerializeField] private float WinScreenDelay = 3f;
    [SerializeField] private int MinimumPlayersBeforeWin = 2;
    [SerializeField] private GameObject roundScreen;
    [SerializeField] private TMP_Text roundTitleText;
    [SerializeField] private TMP_Text[] scoreTexts;
    [SerializeField] private float roundScreenTime = 3f;
    [SerializeField] private TMP_Text[] finalScoreText;

    [Header("Win screen")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private GameObject firstSelectedButton;
    [SerializeField] private string menuSceneName = "StartScreen";
    [SerializeField] private Image winnerFace;

    [Header("Points")]
    private List<PlayerInput> aliveBeforeDeath;
    private List<PlayerInput> finalists;

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
        if (aliveBeforeDeath == null) aliveBeforeDeath = new List<PlayerInput>(players);
        var aliveNow = GetAlivePlayers();
        if (aliveNow.Count > 1)
        {
            aliveBeforeDeath = aliveNow;
            return;
        }
        finalists = aliveBeforeDeath;

        roundOver = true;
        StartCoroutine(EndRoundAfterDelay());
    }

    private IEnumerator ShowRoundScreen(string title)
    {
        roundTitleText.text = title;

        FillScoreboard(scoreTexts);

        foreach (var p in players)
        {
            if (p != null) p.DeactivateInput();
        }
        roundScreen.SetActive(true);
        yield return new WaitForSeconds(roundScreenTime);
        NextRound();
    }

    private IEnumerator EndRoundAfterDelay()
    {
        // Wait so the final hit is visible and simultaneous deaths settle
        yield return new WaitForSeconds(WinScreenDelay);

        var alive = GetAlivePlayers();
        PlaytestLogger.RoundEnded(alive.Count == 1 ? alive[0] : null);

        var pointWinners = alive.Count == 1 ? alive : finalists;
        foreach (var winner in pointWinners)
        {
            GameSettings.scores[winner.playerIndex]++;
            Debug.Log("Scores: " + string.Join(",", GameSettings.scores));
        }

        GameSettings.roundsPlayed++;

        string roundMessage = alive.Count == 1 ? $"Player {alive[0].playerIndex + 1} wins the round!" : "Draw";

        if (IsMatchOver())
        {
            int leader = GetLeader();
            winnerFace.color = GameSettings.playerColors[leader];
            ShowWinScreen($"Player {leader + 1} wins!!");
        }
        else
        {
            if (GameSettings.roundsPlayed >= GameSettings.totalRounds)
            {
                roundMessage += "\nTied overall: one more round!";
            }
            StartCoroutine(ShowRoundScreen(roundMessage));
        }
    }

    private void ShowWinScreen(string message)
    {
        // Stop everyone moving and shooting
        foreach (var player in players)
        {
            if (player != null) player.DeactivateInput();
        }

        FillScoreboard(finalScoreText);

        winnerText.text = message;
        winScreen.SetActive(true);

        // Lets a gamepad or keyboard navigate the buttons straight away
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    // Hook these up to the buttons' OnClick events in the Inspector
    //public void PlayAgain()
    // {
    // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    // }

    public void LoadMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    private void FillScoreboard(TMP_Text[] texts)
    {

        for (int i = 0; i < texts.Length; i++)
        {
            bool joined = i < players.Count;
            texts[i].gameObject.SetActive(joined);
            if (joined)
            {
                texts[i].text = $"Player {i + 1}: {GameSettings.scores[i]}";
                texts[i].color = GameSettings.playerColors[i];
            }
        }

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

    private int GetLeader()
    {
        int leader = -1;
        int best = -1;
        bool tie = false;
        for (int i = 0; i < players.Count; i++)
        {
            int s = GameSettings.scores[i];
            if (s > best)
            {
                best = s;
                leader = i;
                tie = false;

            }
            else if (s == best)
            {
                tie = true;
            }
        }
        return tie ? -1 : leader;
    }
    private bool IsMatchOver()
    {
        return GameSettings.roundsPlayed >= GameSettings.totalRounds && GetLeader() != -1;
    }

    private void NextRound()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}