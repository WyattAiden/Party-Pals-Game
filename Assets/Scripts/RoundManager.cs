using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
    [SerializeField] List<PlayerInput> players;
    [SerializeField] private float WinScreenDelay = 3f;
    [SerializeField] private int MinimumPlayersBeforeWin = 2;

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
            Debug.Log($"Player {alive[0].playerIndex + 1} wins");
        else
            Debug.Log("Draw");
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
