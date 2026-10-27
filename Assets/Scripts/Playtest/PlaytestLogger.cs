using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Records playtest data to CSV files. No setup needed: it creates itself when the game starts
/// and survives scene changes.
///
/// Output (one folder per session):
///   Editor: <project>/Playtests/<date_time>/
///   Build:  <persistentDataPath>/Playtests/<date_time>/
///
///   events.csv     - spawns, shots, damage, deaths, button presses, round start/end
///   positions.csv  - every player's position twice a second (for heatmaps)
///   performance.csv- average FPS and worst frame for every second
///   summary.txt    - quick human-readable totals, rewritten after every round
///
/// Turn it off with PlaytestLogger.Enabled = false, or launch a build with -noplaytestlog.
/// </summary>
public class PlaytestLogger : MonoBehaviour
{
    public static bool Enabled = true;

    const float PositionInterval = 0.5f;
    const float PerfInterval = 1f;

    static PlaytestLogger instance;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------- per-round player record ----------
    class PlayerInfo
    {
        public PlayerInput input;
        public Health health;
        public int id;                 // 1-based player number
        public string scheme;
        public string lastHitBy = "";
        public readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> subs = new();
        public string Label => "P" + id;
    }

    // ---------- whole-session totals per player number ----------
    class Totals
    {
        public string scheme = "";
        public int rounds, wins, kills, deaths, shots, hits;
        public float damageDealt, damageTaken;
        public readonly Dictionary<string, int> presses = new();
        public readonly Dictionary<string, int> killedBy = new();
    }

    StreamWriter events, positions, perf;
    string folder;
    float sessionStart;
    DateTime sessionStartClock;

    string currentScene = "";
    int roundNumber;
    bool roundActive;
    float roundStartTime, lastDeathTime;
    readonly List<PlayerInfo> roundPlayers = new();
    readonly Dictionary<int, Totals> totals = new();
    readonly List<string> roundResults = new();
    int roundsFinished, roundsAbandoned;

    float nextPositionTime, perfTimer, perfWorstFrame, fpsSum, fpsWorst = float.MaxValue;
    int perfFrames, fpsSamples;

    float Now => Time.realtimeSinceStartup - sessionStart;

    // ================= bootstrap =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!Enabled || instance != null) return;
        if (Environment.GetCommandLineArgs().Contains("-noplaytestlog")) return;

        var go = new GameObject("[PlaytestLogger]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlaytestLogger>();
    }

    public static string RootFolder =>
        Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Playtests")
            : Path.Combine(Application.persistentDataPath, "Playtests");

    void Awake()
    {
        sessionStart = Time.realtimeSinceStartup;
        currentScene = SceneManager.GetActiveScene().name;
        sessionStartClock = DateTime.Now;
        folder = Path.Combine(RootFolder, sessionStartClock.ToString("yyyy-MM-dd_HH-mm-ss", Inv));

        try
        {
            Directory.CreateDirectory(folder);
            events = Open("events.csv", "time,round,scene,event,player,x,z,value,target,detail");
            positions = Open("positions.csv", "time,round,scene,player,x,z,hp");
            perf = Open("performance.csv", "time,round,scene,avg_fps,worst_frame_ms");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Playtest] Could not create log files, logging disabled: " + e.Message);
            enabled = false;
            return;
        }

        Debug.Log("[Playtest] Logging this session to " + folder);
        LogEvent("session_start", detail: $"{Application.platform} {Screen.width}x{Screen.height} v{Application.version}");

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerInputManagerV2.OnPlayerJoined += OnPlayerJoined;
    }

    StreamWriter Open(string file, string header)
    {
        var w = new StreamWriter(Path.Combine(folder, file), false, new UTF8Encoding(false));
        w.WriteLine(header);
        return w;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerInputManagerV2.OnPlayerJoined -= OnPlayerJoined;
        if (instance == this) instance = null;
    }

    void OnApplicationQuit()
    {
        if (events == null) return;
        if (roundActive) AbandonRound();
        LogEvent("session_end", value: Now);
        WriteSummary();
        events.Dispose(); positions.Dispose(); perf.Dispose();
        events = null;
    }

    // ================= scene & round flow =================

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (roundActive) AbandonRound();
        ClearRoundPlayers();
        currentScene = scene.name;
        LogEvent("scene_loaded", detail: scene.name);
    }

    void OnPlayerJoined(PlayerInput input)
    {
        if (!roundActive)
        {
            ClearRoundPlayers();
            currentScene = SceneManager.GetActiveScene().name;
            roundNumber++;
            roundActive = true;
            roundStartTime = lastDeathTime = Now;
            LogEvent("round_start", detail: currentScene);
        }

        var info = new PlayerInfo
        {
            input = input,
            health = input.GetComponent<Health>(),
            id = input.playerIndex + 1,
            scheme = input.currentControlScheme ?? "",
        };
        roundPlayers.Add(info);

        var t = T(info.id);
        t.scheme = info.scheme;
        t.rounds++;

        if (info.health != null)
        {
            info.health.Damaged += (amount, source) => OnDamaged(info, amount, source);
            info.health.Died += () => OnDied(info);
        }

        // Count button presses per action (Shoot, Jump, Pause...). Analog sticks are skipped.
        if (input.actions != null)
        {
            foreach (var action in input.actions)
            {
                if (action.type != InputActionType.Button) continue;
                string actionName = action.name;
                Action<InputAction.CallbackContext> handler = _ => OnPress(info, actionName);
                action.performed += handler;
                info.subs.Add((action, handler));
            }
        }

        LogEvent("player_spawn", info, detail: info.scheme);
    }

    void ClearRoundPlayers()
    {
        // Player 1 can share the original actions asset, which outlives the scene, so unhook explicitly.
        foreach (var p in roundPlayers)
            foreach (var (action, handler) in p.subs)
                if (action != null) action.performed -= handler;
        roundPlayers.Clear();
    }

    /// <summary>Called by RoundManager when a round is decided. winner == null means a draw.</summary>
    public static void RoundEnded(PlayerInput winner)
    {
        if (instance == null || !instance.roundActive) return;
        instance.EndRound(winner);
    }

    void EndRound(PlayerInput winner)
    {
        var w = winner != null ? FindPlayer(winner.gameObject) : null;
        float duration = lastDeathTime - roundStartTime;
        if (w != null) T(w.id).wins++;

        LogEvent("round_end", w, value: duration, target: w != null ? w.Label : "draw",
                 detail: $"players={roundPlayers.Count}");
        roundResults.Add($"  #{roundNumber,-3} {currentScene,-22} {roundPlayers.Count} players   " +
                         $"{(w != null ? w.Label + " won" : "draw"),-8}  {FormatTime(duration)}");
        roundsFinished++;
        roundActive = false;
        WriteSummary();
    }

    void AbandonRound()
    {
        float duration = Now - roundStartTime;
        LogEvent("round_abandoned", value: duration, detail: $"players={roundPlayers.Count}");
        roundResults.Add($"  #{roundNumber,-3} {currentScene,-22} {roundPlayers.Count} players   " +
                         $"{"left early",-8}  {FormatTime(duration)}");
        roundsAbandoned++;
        roundActive = false;
        WriteSummary();
    }

    // ================= gameplay events =================

    /// <summary>Called by PlayerController each time a shot actually fires.</summary>
    public static void Shot(GameObject shooter)
    {
        if (instance == null) return;
        var p = instance.FindPlayer(shooter);
        if (p == null) return;
        instance.T(p.id).shots++;
        instance.LogEvent("shot", p);
    }

    void OnDamaged(PlayerInfo victim, float amount, GameObject source)
    {
        var attacker = source != null ? FindPlayer(source) : null;
        string src = attacker != null ? attacker.Label : source != null ? CleanName(source.name) : "unknown";
        victim.lastHitBy = src;

        T(victim.id).damageTaken += amount;
        if (attacker != null && attacker != victim)
        {
            var at = T(attacker.id);
            at.damageDealt += amount;
            at.hits++;
        }
        LogEvent("damage", victim, value: amount, target: src,
                 detail: "hp=" + (victim.health != null ? victim.health.Current.ToString("0", Inv) : "?"));
    }

    void OnDied(PlayerInfo victim)
    {
        lastDeathTime = Now;
        string killer = string.IsNullOrEmpty(victim.lastHitBy) ? "unknown" : victim.lastHitBy;
        var vt = T(victim.id);
        vt.deaths++;
        vt.killedBy[killer] = vt.killedBy.TryGetValue(killer, out var n) ? n + 1 : 1;

        var k = roundPlayers.Find(p => p.Label == killer);
        if (k != null && k != victim) T(k.id).kills++;

        LogEvent("death", victim, value: Now - roundStartTime, target: killer);
    }

    void OnPress(PlayerInfo p, string actionName)
    {
        if (p.input == null) return;
        var t = T(p.id);
        t.presses[actionName] = t.presses.TryGetValue(actionName, out var n) ? n + 1 : 1;
        LogEvent("input", p, detail: actionName);
    }

    // ================= sampling =================

    void Update()
    {
        if (events == null) return;

        // Performance: one row per second
        float dt = Time.unscaledDeltaTime;
        perfTimer += dt;
        perfFrames++;
        perfWorstFrame = Mathf.Max(perfWorstFrame, dt);
        if (perfTimer >= PerfInterval)
        {
            float fps = perfFrames / perfTimer;
            perf.WriteLine(Row(F(Now), roundNumber.ToString(Inv), Esc(currentScene), F(fps), F(perfWorstFrame * 1000f)));
            if (Now > 3f) // ignore the loading spike at startup
            {
                fpsSum += fps; fpsSamples++;
                fpsWorst = Mathf.Min(fpsWorst, fps);
            }
            perfTimer = 0f; perfFrames = 0; perfWorstFrame = 0f;
            events.Flush(); positions.Flush(); perf.Flush();
        }

        // Positions: twice a second for living players
        if (roundActive && Now >= nextPositionTime)
        {
            nextPositionTime = Now + PositionInterval;
            foreach (var p in roundPlayers)
            {
                if (p.input == null || (p.health != null && p.health.IsDead)) continue;
                var pos = p.input.transform.position;
                positions.WriteLine(Row(F(Now), roundNumber.ToString(Inv), Esc(currentScene), p.Label,
                    F(pos.x), F(pos.z), p.health != null ? F(p.health.Current) : ""));
            }
        }
    }

    // ================= helpers =================

    PlayerInfo FindPlayer(GameObject go)
    {
        if (go == null) return null;
        var input = go.GetComponentInParent<PlayerInput>();
        return input == null ? null : roundPlayers.Find(p => p.input == input);
    }

    Totals T(int id)
    {
        if (!totals.TryGetValue(id, out var t)) totals[id] = t = new Totals();
        return t;
    }

    void LogEvent(string type, PlayerInfo p = null, float value = float.NaN, string target = "", string detail = "")
    {
        if (events == null) return;
        string x = "", z = "";
        if (p != null && p.input != null)
        {
            var pos = p.input.transform.position;
            x = F(pos.x); z = F(pos.z);
        }
        events.WriteLine(Row(F(Now), roundNumber.ToString(Inv), Esc(currentScene), type,
            p != null ? p.Label : "", x, z, float.IsNaN(value) ? "" : F(value), Esc(target), Esc(detail)));
    }

    static string Row(params string[] cells) => string.Join(",", cells);
    static string F(float v) => v.ToString("0.###", Inv);
    static string Esc(string s) =>
        string.IsNullOrEmpty(s) ? "" : s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    // "BarbedWireFence_36_v1 (18)" -> "BarbedWireFence", "CannonBall(Clone)" -> "CannonBall"
    static string CleanName(string n)
    {
        n = n.Replace("(Clone)", "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*\(\d+\)\s*$", "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"(_\d+)?(_v\d+)?$", "");
        return n.Trim();
    }
    static string FormatTime(float s) => $"{(int)(s / 60)}m {(int)(s % 60):00}s";

    void WriteSummary()
    {
        if (folder == null) return;
        var sb = new StringBuilder();
        sb.AppendLine($"Party Pals playtest - {sessionStartClock.ToString("yyyy-MM-dd HH:mm", Inv)}");
        sb.AppendLine($"Session length: {FormatTime(Now)}   Rounds finished: {roundsFinished}   Left early: {roundsAbandoned}");
        if (fpsSamples > 0)
            sb.AppendLine($"Average FPS: {fpsSum / fpsSamples:0}   Worst second: {fpsWorst:0} FPS");
        sb.AppendLine();
        sb.AppendLine("Rounds");
        foreach (var r in roundResults) sb.AppendLine(r);
        sb.AppendLine();
        sb.AppendLine("Players");
        foreach (var kv in totals.OrderBy(k => k.Key))
        {
            var t = kv.Value;
            string acc = t.shots > 0 ? (100f * t.hits / t.shots).ToString("0", Inv) + "%" : "-";
            sb.AppendLine($"  P{kv.Key} ({t.scheme}): rounds {t.rounds}, wins {t.wins}, kills {t.kills}, deaths {t.deaths}, " +
                          $"shots {t.shots}, accuracy {acc}, damage dealt {t.damageDealt:0}, damage taken {t.damageTaken:0}");
            if (t.killedBy.Count > 0)
                sb.AppendLine("      killed by: " + string.Join(", ", t.killedBy.OrderByDescending(k => k.Value).Select(k => $"{k.Key} x{k.Value}")));
            if (t.presses.Count > 0)
                sb.AppendLine("      buttons:   " + string.Join(", ", t.presses.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
        }

        try { File.WriteAllText(Path.Combine(folder, "summary.txt"), sb.ToString()); }
        catch (Exception e) { Debug.LogWarning("[Playtest] Could not write summary: " + e.Message); }
    }
}
