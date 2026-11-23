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
///   sticks.csv     - both analog sticks, actual speed and facing, 10 times a second per player
///                    (move_x/y, look_x/y = raw stick; speed = how fast the player really moved;
///                     aim_error = degrees between where the right stick points and where the player faces)
///   bookmark_NN.png- screenshot taken when someone presses F10 (also a "bookmark" row in events.csv)
///
///   Extra events.csv rows:
///     pause          - pause menu open / resume / quit / load <scene>
///     press_ignored  - a Shoot/Dash press that did nothing (target = action, detail = reason)
///     dash           - a dash that actually happened
///     hidden         - a player was hidden behind level geometry (value = seconds)
///     offscreen      - a player was off the edge of the screen (value = seconds)
///     weapon_spawn / weapon_despawn / weapon_pickup / weapon_lost - power weapon life cycle
///     damage rows carry "weapon=<name>" when a power weapon did it; falls show as "Fell", War bombs as "Bomb"
///   events.csv also gets two responsiveness events, measured every frame:
///     move_response - ms from pushing the left stick (from rest) until the player is actually moving
///     aim_response  - ms from a right-stick flick (new direction) until the player faces it
///     (value = ms, detail notes "glued"/"timeout" when relevant)
///   summary.txt    - quick human-readable totals, rewritten after every round
///
///   Accuracy = shots that hit someone / shots fired. Burn/bleed ticks (marked "tick" in
///   events.csv) and 0-damage hits don't count, and one shot counts once even if its splash
///   hits the same player twice. FPS stats skip seconds with a freeze over 1 s (editor
///   alt-tab, Play Mode stop) and the second right after a scene loads; freezes are counted separately.
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
    const Key BookmarkKey = Key.F10;
    const float ScreenEdge = 0.05f;          // viewport margin that counts as "near the edge"
    const float MinSpanToLog = 0.3f;         // hidden/offscreen stretches shorter than this aren't written as events
    const float WeaponLingerTime = 4f;       // damage this long after losing a power weapon still counts for it
    const float SameHitWindow = 0.25f;       // repeat damage on the same victim this soon = the same shot (splash + direct)
    const float FreezeFrameSec = 1f;         // a single frame longer than this = a freeze, not real gameplay FPS
    const float SceneLoadGrace = 1.5f;       // seconds after a scene load that FPS stats ignore

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
    const float StickInterval = 0.1f;

    // Responsiveness thresholds
    const float StickRest = 0.2f;        // below this the stick counts as released
    const float StickPushed = 0.5f;      // a deliberate push
    const float MovingSpeed = 2f;        // m/s that counts as "the player is moving"
    const float AimFlickDeg = 30f;       // right-stick direction change that counts as a new aim
    const float AimOnTargetDeg = 10f;    // facing within this many degrees = on target
    const float ResponseTimeout = 1f;    // give up measuring after this long

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

        // Stick / responsiveness tracking
        public InputAction move, look;
        public CharacterController cc;
        public PlayerController pc;
        public PlayerStatusEffects fx;
        public bool moveHeld;                  // left stick currently pushed past StickRest
        public float movePending = -1f;        // time the move push started (-1 = not measuring)
        public bool moveGlued;
        public float aimTargetYaw = float.NaN; // last right-stick direction we measured against
        public float aimPending = -1f;
        public bool aimGlued;

        public PlayerWeaponHandler wh;
        public PlayerOcclusionIndicator occ;
        public float hiddenSince = -1f, offSince = -1f;
        public string lastHitWeapon = "";
        public readonly Dictionary<PlayerInfo, float> lastHitOn = new(); // victim -> time this player last scored a hit on them
    }

    // ---------- whole-session totals per player number ----------
    class Totals
    {
        public string scheme = "";
        public int rounds, wins, kills, deaths, shots, hits;
        public float damageDealt, damageTaken;
        public readonly Dictionary<string, int> presses = new();
        public readonly Dictionary<string, int> killedBy = new();
        public readonly List<float> moveMs = new(), aimMs = new();
        public int moveTimeouts, aimTimeouts;
        public float aliveTime, gluedTime, partialTiltTime, movingTime;
        public float hiddenTime, offscreenTime, edgeTime;
        public int dashes, weaponPickups, weaponKills;
        public float weaponDamage;
        public readonly Dictionary<string, int> ignored = new();
    }

    class WeaponStats { public int spawned, despawned, pickups, shots, kills; public float damage; }
    readonly Dictionary<string, WeaponStats> weaponStats = new();
    int pauses, pauseQuits, bookmarks;
    WeaponStats W(string name) { if (!weaponStats.TryGetValue(name, out var w)) weaponStats[name] = w = new WeaponStats(); return w; }

    StreamWriter events, positions, perf, sticks;
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

    float nextPositionTime, nextStickTime, perfTimer, perfWorstFrame, fpsSum, fpsWorst = float.MaxValue;
    int perfFrames, fpsSamples;
    float roundFpsSum, sceneLoadTime;
    int roundFpsSamples, freezes;

    static bool tickDamage; // true while a damage-over-time tick is being applied

    /// <summary>Apply burn/bleed tick damage so the logger doesn't count it as a shot hitting.</summary>
    public static void TickDamage(Health health, float amount, GameObject source)
    {
        if (health == null) return;
        tickDamage = true;
        try { health.TakeDamage(amount, source); }
        finally { tickDamage = false; }
    }

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
        totals.Clear(); roundResults.Clear(); weaponStats.Clear();
        pauses = pauseQuits = bookmarks = 0;
        roundsFinished = roundsAbandoned = 0;
        nextPositionTime = nextStickTime = perfTimer = perfWorstFrame = fpsSum = 0f;
        fpsWorst = float.MaxValue;
        perfFrames = fpsSamples = 0;
        roundFpsSum = 0f; roundFpsSamples = freezes = 0;
        sceneLoadTime = 0f;

        try
        {
            Directory.CreateDirectory(folder);
            events = Open("events.csv", "time,round,scene,event,player,x,z,value,target,detail");
            positions = Open("positions.csv", "time,round,scene,player,x,z,hp");
            perf = Open("performance.csv", "time,round,scene,avg_fps,worst_frame_ms");
            sticks = Open("sticks.csv", "time,round,scene,player,move_x,move_y,look_x,look_y,speed,facing,aim_target,aim_error,running,dashing,glued,hidden,screen");
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
        events?.Dispose(); positions?.Dispose(); perf?.Dispose(); sticks?.Dispose();
        events = positions = perf = sticks = null;
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
        sceneLoadTime = Now;
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
        info.move = input.actions?.FindAction("Move");
        info.look = input.actions?.FindAction("Look");
        info.cc = input.GetComponent<CharacterController>();
        info.pc = input.GetComponent<PlayerController>();
        info.fx = input.GetComponent<PlayerStatusEffects>();
        info.wh = input.GetComponent<PlayerWeaponHandler>();
        info.occ = input.GetComponent<PlayerOcclusionIndicator>();
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
        string weapon = instance.ActiveWeapon(p, 0.05f);
        if (weapon != "") instance.W(weapon).shots++;
        instance.LogEvent("shot", p, target: weapon);
    }

    /// <summary>Name of the power weapon a player is using (or just lost within 'linger' seconds), else "".</summary>
    string ActiveWeapon(PlayerInfo p, float linger)
    {
        if (p?.wh == null) return "";
        if (p.wh.HasPowerWeapon) return p.wh.FamilyName;
        return Time.time - p.wh.LastWeaponLostTime <= linger ? p.wh.LastWeaponName : "";
    }

    // ---------- hooks called from game scripts ----------

    /// <summary>PauseMenu: "open", "resume", "quit", or "load SceneName".</summary>
    public static void Pause(string what)
    {
        if (!IsLogging) return;
        instance.pauses += what == "open" ? 1 : 0;
        instance.pauseQuits += what == "quit" ? 1 : 0;
        instance.LogEvent("pause", instance.roundPlayers.Find(p => p.id == 1), detail: what);
    }

    /// <summary>A button press that did nothing, and why (cooldown, running, ...).</summary>
    public static void PressIgnored(GameObject who, string action, string reason)
    {
        if (!IsLogging) return;
        var p = instance.FindPlayer(who);
        if (p == null) return;
        var t = instance.T(p.id);
        string key = action + " " + reason;
        t.ignored[key] = t.ignored.TryGetValue(key, out var n) ? n + 1 : 1;
        instance.LogEvent("press_ignored", p, target: action, detail: reason);
    }

    public static void Dashed(GameObject who)
    {
        if (!IsLogging) return;
        var p = instance.FindPlayer(who);
        if (p == null) return;
        instance.T(p.id).dashes++;
        instance.LogEvent("dash", p);
    }

    public static void WeaponSpawned(Component pickup, string weapon)
    {
        if (!IsLogging || pickup == null) return;
        instance.W(weapon).spawned++;
        instance.LogEvent("weapon_spawn", target: weapon, at: pickup.transform.position);
    }

    public static void WeaponDespawned(Component pickup, string weapon)
    {
        if (!IsLogging || pickup == null) return;
        instance.W(weapon).despawned++;
        instance.LogEvent("weapon_despawn", target: weapon, at: pickup.transform.position);
    }

    public static void WeaponPickedUp(GameObject who, string weapon, string variant, int ammo)
    {
        if (!IsLogging) return;
        var p = instance.FindPlayer(who);
        if (p == null) return;
        instance.T(p.id).weaponPickups++;
        instance.W(weapon).pickups++;
        instance.LogEvent("weapon_pickup", p, value: ammo, target: weapon, detail: variant != weapon ? variant : "");
    }

    public static void WeaponLost(GameObject who, string weapon, int ammoLeft, string reason)
    {
        if (!IsLogging) return;
        var p = instance.FindPlayer(who);
        if (p == null) return;
        instance.LogEvent("weapon_lost", p, value: ammoLeft, target: weapon, detail: reason);
    }

    void OnDamaged(PlayerInfo victim, float amount, GameObject source)
    {
        var attacker = source != null ? FindPlayer(source) : null;
        string src = attacker != null ? attacker.Label
                   : source == null ? "unknown"
                   : source.GetComponent<DeathPlane>() != null ? "Fell"
                   : source.GetComponent<DuelShooter.Explosion>() != null ? "Bomb"
                   : CleanName(source.name);
        victim.lastHitBy = src;
        string weapon = attacker != null ? ActiveWeapon(attacker, WeaponLingerTime) : "";
        victim.lastHitWeapon = weapon;
        if (weapon != "" && attacker != victim)
        {
            W(weapon).damage += amount;
            T(attacker.id).weaponDamage += amount;
        }

        bool tick = tickDamage;
        T(victim.id).damageTaken += amount;
        if (attacker != null && attacker != victim)
        {
            var at = T(attacker.id);
            at.damageDealt += amount;
            // A hit = a shot landing: no DoT ticks, no 0-damage splashes, one per shot per victim
            bool sameShot = attacker.lastHitOn.TryGetValue(victim, out var lastHit) && Now - lastHit < SameHitWindow;
            if (!tick && amount > 0f && !sameShot && at.hits < at.shots)
            {
                at.hits++;
                attacker.lastHitOn[victim] = Now;
            }
        }
        LogEvent("damage", victim, value: amount, target: src,
                 detail: "hp=" + (victim.health != null ? victim.health.Current.ToString("0", Inv) : "?")
                         + (weapon != "" ? " weapon=" + weapon : "")
                         + (tick ? " tick" : ""));
    }

    void OnDied(PlayerInfo victim)
    {
        lastDeathTime = Now;
        string killer = string.IsNullOrEmpty(victim.lastHitBy) ? "unknown" : victim.lastHitBy;
        var vt = T(victim.id);
        vt.deaths++;
        vt.killedBy[killer] = vt.killedBy.TryGetValue(killer, out var n) ? n + 1 : 1;

        var k = roundPlayers.Find(p => p.Label == killer);
        if (k != null && k != victim)
        {
            T(k.id).kills++;
            if (victim.lastHitWeapon != "") { T(k.id).weaponKills++; W(victim.lastHitWeapon).kills++; }
        }

        LogEvent("death", victim, value: Now - roundStartTime, target: killer,
                 detail: victim.lastHitWeapon != "" ? "weapon=" + victim.lastHitWeapon : "");
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

        if (kb != null && kb[BookmarkKey].wasPressedThisFrame) Bookmark();

        // Performance: one row per second
        float dt = Time.unscaledDeltaTime;
        perfTimer += dt;
        perfFrames++;
        perfWorstFrame = Mathf.Max(perfWorstFrame, dt);
        if (perfTimer >= PerfInterval)
        {
            float fps = perfFrames / perfTimer;
            perf.WriteLine(Row(F(Now), roundNumber.ToString(Inv), Esc(currentScene), F(fps), F(perfWorstFrame * 1000f)));
            bool froze = perfWorstFrame > FreezeFrameSec;     // editor alt-tab / Play Mode stop / long load
            bool justLoaded = Now - sceneLoadTime < SceneLoadGrace;
            if (froze && Now > 3f) freezes++;
            if (Now > 3f && !froze && !justLoaded && Application.isFocused) // ignore startup, loads and freezes
            {
                fpsSum += fps; fpsSamples++;
                fpsWorst = Mathf.Min(fpsWorst, fps);
                if (roundActive) { roundFpsSum += fps; roundFpsSamples++; }
            }
            perfTimer = 0f; perfFrames = 0; perfWorstFrame = 0f;
            events.Flush(); positions.Flush(); perf.Flush(); sticks.Flush();
        }

        TrackSticks();

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

    // ================= sticks & responsiveness =================

    void TrackSticks()
    {
        if (!roundActive) return;
        if (Time.timeScale == 0f) // paused: nothing can respond, so don't measure
        {
            foreach (var q in roundPlayers) q.movePending = q.aimPending = -1f;
            return;
        }
        float now = Now, dt = Time.deltaTime;
        bool writeRow = now >= nextStickTime;
        if (writeRow) nextStickTime = now + StickInterval;
        float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;

        foreach (var p in roundPlayers)
        {
            if (p.input == null) continue;
            bool dead = p.health != null && p.health.IsDead;
            if (dead)
            {
                p.movePending = p.aimPending = -1f; p.moveHeld = false;
                EndSpan(p, ref p.hiddenSince, "hidden", now); EndSpan(p, ref p.offSince, "offscreen", now);
                continue;
            }

            Vector2 mv = p.move != null ? p.move.ReadValue<Vector2>() : Vector2.zero;
            Vector2 lk = p.look != null ? p.look.ReadValue<Vector2>() : Vector2.zero;
            var vel = p.cc != null ? p.cc.velocity : Vector3.zero;
            float speed = new Vector2(vel.x, vel.z).magnitude;
            float facing = p.input.transform.eulerAngles.y;
            bool glued = p.fx != null && (p.fx.IsGlued || p.fx.TurnMultiplier < 1f);
            bool running = p.pc != null && p.pc.IsRunning;
            bool dashing = p.pc != null && p.pc.IsDashing;

            var t = T(p.id);
            t.aliveTime += dt;
            if (glued) t.gluedTime += dt;
            if (mv.magnitude >= StickRest && mv.magnitude < 0.9f) t.partialTiltTime += dt;
            if (speed >= MovingSpeed) t.movingTime += dt;

            // --- can people see this player? ---
            bool hidden = p.occ != null && p.occ.IsHidden;
            string screen = "in";
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(p.input.transform.position + Vector3.up);
                if (vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) screen = "off";
                else if (vp.x < ScreenEdge || vp.x > 1f - ScreenEdge || vp.y < ScreenEdge || vp.y > 1f - ScreenEdge) screen = "edge";
            }
            if (hidden) { t.hiddenTime += dt; if (p.hiddenSince < 0f) p.hiddenSince = now; }
            else EndSpan(p, ref p.hiddenSince, "hidden", now);
            if (screen == "off") { t.offscreenTime += dt; if (p.offSince < 0f) p.offSince = now; }
            else EndSpan(p, ref p.offSince, "offscreen", now);
            if (screen == "edge") t.edgeTime += dt;

            // --- left stick: push from rest -> actually moving ---
            float mm = mv.magnitude;
            if (!p.moveHeld && mm >= StickPushed)
            {
                p.moveHeld = true;
                if (!dashing && speed < MovingSpeed) { p.movePending = now; p.moveGlued = glued; }
            }
            else if (p.moveHeld && mm < StickRest) { p.moveHeld = false; p.movePending = -1f; }

            if (p.movePending >= 0f)
            {
                float ms = (now - p.movePending) * 1000f;
                if (speed >= MovingSpeed) { FinishResponse(p, "move_response", ms, p.moveGlued, false); p.movePending = -1f; }
                else if (now - p.movePending > ResponseTimeout) { FinishResponse(p, "move_response", ms, p.moveGlued, true); p.movePending = -1f; }
            }

            // --- right stick: new direction -> facing it ---
            float aimTarget = float.NaN, aimErr = float.NaN;
            if (lk.magnitude >= StickPushed)
            {
                aimTarget = Mathf.Repeat(camYaw + Mathf.Atan2(lk.x, lk.y) * Mathf.Rad2Deg, 360f);
                aimErr = Mathf.Abs(Mathf.DeltaAngle(facing, aimTarget));
                bool flick = float.IsNaN(p.aimTargetYaw) || Mathf.Abs(Mathf.DeltaAngle(p.aimTargetYaw, aimTarget)) >= AimFlickDeg;
                if (flick)
                {
                    p.aimTargetYaw = aimTarget;
                    if (aimErr > AimOnTargetDeg) { p.aimPending = now; p.aimGlued = glued; }
                }
            }
            else if (lk.magnitude < StickRest) p.aimTargetYaw = float.NaN;

            if (p.aimPending >= 0f && !float.IsNaN(p.aimTargetYaw))
            {
                float err = Mathf.Abs(Mathf.DeltaAngle(facing, p.aimTargetYaw));
                float ms = (now - p.aimPending) * 1000f;
                if (err <= AimOnTargetDeg) { FinishResponse(p, "aim_response", ms, p.aimGlued, false); p.aimPending = -1f; }
                else if (now - p.aimPending > ResponseTimeout) { FinishResponse(p, "aim_response", ms, p.aimGlued, true); p.aimPending = -1f; }
            }
            else if (float.IsNaN(p.aimTargetYaw)) p.aimPending = -1f;

            if (writeRow)
            {
                sticks.WriteLine(Row(F(now), roundNumber.ToString(Inv), Esc(currentScene), p.Label,
                    F(mv.x), F(mv.y), F(lk.x), F(lk.y), F(speed), F(facing),
                    float.IsNaN(aimTarget) ? "" : F(aimTarget), float.IsNaN(aimErr) ? "" : F(aimErr),
                    running ? "1" : "0", dashing ? "1" : "0", glued ? "1" : "0", hidden ? "1" : "0", screen));
            }
        }
    }

    void EndSpan(PlayerInfo p, ref float since, string type, float now)
    {
        if (since < 0f) return;
        float len = now - since;
        since = -1f;
        if (len >= MinSpanToLog) LogEvent(type, p, value: len);
    }

    void Bookmark()
    {
        bookmarks++;
        string file = $"bookmark_{bookmarks:00}.png";
        try { ScreenCapture.CaptureScreenshot(Path.Combine(folder, file)); }
        catch (Exception e) { Debug.LogWarning("[Playtest] Bookmark screenshot failed: " + e.Message); file = ""; }
        LogEvent("bookmark", value: bookmarks, detail: file);
        toast = $"Bookmark #{bookmarks} saved";
        toastUntil = Time.unscaledTime + 1.5f;
    }

    void FinishResponse(PlayerInfo p, string type, float ms, bool glued, bool timedOut)
    {
        var t = T(p.id);
        if (type == "move_response") { if (timedOut) t.moveTimeouts++; else if (!glued) t.moveMs.Add(ms); }
        else { if (timedOut) t.aimTimeouts++; else if (!glued) t.aimMs.Add(ms); }
        string detail = (glued ? "glued" : "") + (timedOut ? (glued ? " " : "") + "timeout" : "");
        LogEvent(type, p, value: ms, detail: detail);
    }

    static float Median(List<float> v)
    {
        if (v.Count == 0) return float.NaN;
        var s = v.OrderBy(x => x).ToList();
        return s[s.Count / 2];
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

    void LogEvent(string type, PlayerInfo p = null, float value = float.NaN, string target = "", string detail = "", Vector3? at = null)
    {
        if (events == null) return;
        string x = "", z = "";
        if (at.HasValue) { x = F(at.Value.x); z = F(at.Value.z); }
        else if (p != null && p.input != null)
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
            sb.AppendLine($"Average FPS: {fpsSum / fpsSamples:0}   In rounds: {(roundFpsSamples > 0 ? (roundFpsSum / roundFpsSamples).ToString("0", Inv) : "-")}   " +
                          $"Worst second: {fpsWorst:0} FPS   Freezes over {FreezeFrameSec:0} s (not counted): {freezes}");
        if (pauses > 0 || bookmarks > 0)
            sb.AppendLine($"Pause menu opened: {pauses} (quit from it: {pauseQuits})   Bookmarks (F10): {bookmarks}");
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
            if (t.aliveTime > 0f)
            {
                float mMed = Median(t.moveMs), aMed = Median(t.aimMs);
                sb.AppendLine($"      sticks:    move response {(float.IsNaN(mMed) ? "-" : mMed.ToString("0", Inv) + " ms")} (n={t.moveMs.Count}, stuck {t.moveTimeouts}), " +
                              $"aim response {(float.IsNaN(aMed) ? "-" : aMed.ToString("0", Inv) + " ms")} (n={t.aimMs.Count}, missed {t.aimTimeouts}), " +
                              $"glued {100f * t.gluedTime / t.aliveTime:0}% of the time, half-tilting the move stick {100f * t.partialTiltTime / t.aliveTime:0}%");
            }
            if (t.aliveTime > 0f)
                sb.AppendLine($"      visibility: hidden behind walls {100f * t.hiddenTime / t.aliveTime:0}%, off-screen {100f * t.offscreenTime / t.aliveTime:0}%, " +
                              $"near the screen edge {100f * t.edgeTime / t.aliveTime:0}%");
            sb.AppendLine($"      dashes {t.dashes}, power weapons picked up {t.weaponPickups} (damage {t.weaponDamage:0}, kills {t.weaponKills})");
            if (t.ignored.Count > 0)
                sb.AppendLine("      ignored:   " + string.Join(", ", t.ignored.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
            if (t.presses.Count > 0)
                sb.AppendLine("      buttons:   " + string.Join(", ", t.presses.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
        }

        if (weaponStats.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Power weapons");
            foreach (var kv in weaponStats.OrderByDescending(k => k.Value.pickups))
            {
                var w = kv.Value;
                sb.AppendLine($"  {kv.Key,-20} spawned {w.spawned}, picked up {w.pickups}, despawned unused {w.despawned}, " +
                              $"shots {w.shots}, damage {w.damage:0}, kills {w.kills}");
            }
        }

        try { File.WriteAllText(Path.Combine(folder, "summary.txt"), sb.ToString()); }
        catch (Exception e) { Debug.LogWarning("[Playtest] Could not write summary: " + e.Message); }
    }
}
