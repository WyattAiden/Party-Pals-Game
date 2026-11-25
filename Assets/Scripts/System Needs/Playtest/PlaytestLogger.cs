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
/// Turning it on and off (the setting is saved between launches, default ON):
///   - Editor: tick/untick  Party Pals > Playtest Logging  (works during Play Mode too)
///   - In game: press F9 to toggle; a small ON/OFF message shows in the top-left corner
///   - Code:    PlaytestLogger.Enabled = false;
///   - Build launch flags (this launch only, not saved): -noplaytestlog / -playtestlog
/// Turning it off mid-game closes the current session's files cleanly; turning it back on
/// starts a new session folder (players already in a round are picked up).
/// </summary>
public class PlaytestLogger : MonoBehaviour
{
    const string PrefKey = "PartyPals.PlaytestLogging";
    const Key ToggleKey = Key.F9;

    static bool? launchOverride; // set by -noplaytestlog / -playtestlog, never saved

    /// <summary>The on/off switch. Saved between launches. Changing it while the game runs takes effect immediately.</summary>
    public static bool Enabled
    {
        get => launchOverride ?? PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set
        {
            launchOverride = null;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (instance != null) instance.Apply(true);
        }
    }

    /// <summary>True while a session is actually being written to disk.</summary>
    public static bool IsLogging => instance != null && instance.events != null;

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
        public Action<float, GameObject> onDamaged;
        public Action onDied;
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

    string toast;
    float toastUntil;

    // ================= bootstrap =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var args = Environment.GetCommandLineArgs();
        launchOverride = args.Contains("-noplaytestlog") ? false
                       : args.Contains("-playtestlog") ? true
                       : (bool?)null;

        // The object always exists (so the F9 toggle works); it only writes files while Enabled.
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerInputManagerV2.OnPlayerJoined += OnPlayerJoined;
        Apply(false);
        if (!Enabled) Debug.Log($"[Playtest] Logging is OFF (turn on: Party Pals > Playtest Logging, or press {ToggleKey} in game)");
    }

    /// <summary>Starts or stops the session so it matches Enabled.</summary>
    void Apply(bool announce)
    {
        bool want = Enabled;
        if (want && events == null) StartSession();
        else if (!want && events != null) StopSession();
        else return;

        if (announce)
        {
            toast = "Playtest logging " + (IsLogging ? "ON" : "OFF");
            toastUntil = Time.unscaledTime + 2f;
        }
    }

    void StartSession()
    {
        sessionStart = Time.realtimeSinceStartup;
        currentScene = SceneManager.GetActiveScene().name;
        sessionStartClock = DateTime.Now;
        folder = Path.Combine(RootFolder, sessionStartClock.ToString("yyyy-MM-dd_HH-mm-ss", Inv));

        // Fresh numbers for every session
        roundNumber = 0; roundActive = false;
        roundStartTime = lastDeathTime = 0f;
        totals.Clear(); roundResults.Clear();
        roundsFinished = roundsAbandoned = 0;
        nextPositionTime = perfTimer = perfWorstFrame = fpsSum = 0f;
        fpsWorst = float.MaxValue;
        perfFrames = fpsSamples = 0;

        try
        {
            Directory.CreateDirectory(folder);
            events = Open("events.csv", "time,round,scene,event,player,x,z,value,target,detail");
            positions = Open("positions.csv", "time,round,scene,player,x,z,hp");
            perf = Open("performance.csv", "time,round,scene,avg_fps,worst_frame_ms");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Playtest] Could not create log files, logging stays off: " + e.Message);
            CloseFiles();
            folder = null;
            return;
        }

        Debug.Log("[Playtest] Logging this session to " + folder);
        LogEvent("session_start", detail: $"{Application.platform} {Screen.width}x{Screen.height} v{Application.version}");

        // Turned on mid-game: pick up players who already joined
        foreach (var pi in PlayerInput.all) OnPlayerJoined(pi);
    }

    void StopSession()
    {
        if (events == null) return;
        if (roundActive) AbandonRound();
        LogEvent("session_end", value: Now);
        WriteSummary();
        ClearRoundPlayers();
        CloseFiles();
        Debug.Log("[Playtest] Logging stopped. Files saved in " + folder);
    }

    void CloseFiles()
    {
        events?.Dispose(); positions?.Dispose(); perf?.Dispose();
        events = positions = perf = null;
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
        StopSession();
        if (instance == this) instance = null;
    }

    void OnApplicationQuit() => StopSession();

    // ================= scene & round flow =================

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (events == null || mode != LoadSceneMode.Single) return;
        if (roundActive) AbandonRound();
        ClearRoundPlayers();
        currentScene = scene.name;
        LogEvent("scene_loaded", detail: scene.name);
    }

    void OnPlayerJoined(PlayerInput input)
    {
        if (events == null || input == null) return;
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
            info.onDamaged = (amount, source) => OnDamaged(info, amount, source);
            info.onDied = () => OnDied(info);
            info.health.Damaged += info.onDamaged;
            info.health.Died += info.onDied;
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
        {
            foreach (var (action, handler) in p.subs)
                if (action != null) action.performed -= handler;
            if (p.health != null)
            {
                if (p.onDamaged != null) p.health.Damaged -= p.onDamaged;
                if (p.onDied != null) p.health.Died -= p.onDied;
            }
        }
        roundPlayers.Clear();
    }

    /// <summary>Called by RoundManager when a round is decided. winner == null means a draw.</summary>
    public static void RoundEnded(PlayerInput winner)
    {
        if (!IsLogging || !instance.roundActive) return;
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
        if (!IsLogging) return;
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
        var kb = Keyboard.current;
        if (kb != null && kb[ToggleKey].wasPressedThisFrame) Enabled = !Enabled;

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

    void OnGUI()
    {
        if (toast == null || Time.unscaledTime > toastUntil) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(10, 10, 300, 40), toast, style);
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
