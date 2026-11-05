using System.Collections.Generic;
using UnityEngine;

// Goes on the player prefab. Handles timed debuffs like the Glue Gun's burn + slow.
// PlayerController multiplies its move speed by SpeedMultiplier.
public class PlayerStatusEffects : MonoBehaviour
{
    [Header("Glued look")]
    [Tooltip("Colour the player is tinted toward while glued, so everyone can see who's stuck.")]
    [SerializeField] private Color glueTint = new Color(1f, 0.85f, 0.35f);
    [Range(0f, 1f)] [SerializeField] private float tintStrength = 0.6f;
    [Tooltip("Seconds between burn damage ticks.")]
    [SerializeField] private float burnTickInterval = 0.5f;

    public float SpeedMultiplier => slowTimer > 0f ? slowMultiplier : 1f;
    public bool IsGlued => slowTimer > 0f;
    public float TurnMultiplier => turnSlowTimer > 0f ? turnMultiplier : 1f;

    private float turnSlowTimer;
    private float turnMultiplier = 1f;

    private Health health;
    private float slowTimer;
    private float slowMultiplier = 1f;

    private float burnTimer;
    private float burnDps;
    private float burnTickTimer;
    private GameObject burnSource;

    private readonly List<(Material mat, Color original)> tinted = new List<(Material, Color)>();

    void Awake() => health = GetComponent<Health>();

    // Re-hits refresh the timers instead of stacking, and keep the strongest slow.
    public void ApplyGlue(float burnDamagePerSecond, float burnDuration, float slowTo, float slowDuration, GameObject source)
    {
        if (health != null && health.IsDead) return;

        if (burnDuration > 0f && burnDamagePerSecond > 0f)
        {
            burnDps = Mathf.Max(burnDps, burnDamagePerSecond);
            burnTimer = Mathf.Max(burnTimer, burnDuration);
            burnSource = source;
        }

        if (slowDuration > 0f)
            ApplySlow(slowTo, slowDuration, glueTint);
    }

    // Generic slow (used by glue, beams...). Re-applying refreshes the timer and keeps the strongest slow.
    // Pass a tint colour so other players can see who is slowed, or null for no tint.
    public void ApplySlow(float slowTo, float duration, Color? tint)
    {
        if (health != null && health.IsDead) return;
        if (duration <= 0f) return;
        slowMultiplier = slowTimer > 0f ? Mathf.Min(slowMultiplier, slowTo) : slowTo;
        slowTimer = Mathf.Max(slowTimer, duration);
        if (tint.HasValue) SetTint(true, tint.Value);
    }

    // Slows how fast the player can turn/aim (e.g. while firing a heavy beam)
    public void ApplyTurnSlow(float turnTo, float duration)
    {
        if (duration <= 0f) return;
        turnMultiplier = turnSlowTimer > 0f ? Mathf.Min(turnMultiplier, turnTo) : turnTo;
        turnSlowTimer = Mathf.Max(turnSlowTimer, duration);
    }

    void Update()
    {
        if (turnSlowTimer > 0f)
        {
            turnSlowTimer -= Time.deltaTime;
            if (turnSlowTimer <= 0f) turnMultiplier = 1f;
        }

        if (burnTimer > 0f)
        {
            burnTimer -= Time.deltaTime;
            burnTickTimer -= Time.deltaTime;
            if (burnTickTimer <= 0f)
            {
                burnTickTimer = burnTickInterval;
                if (health != null) health.TakeDamage(burnDps * burnTickInterval, burnSource);
            }
            if (burnTimer <= 0f) { burnDps = 0f; burnTickTimer = 0f; }
        }

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) { slowMultiplier = 1f; SetTint(false); }
        }
    }

    void SetTint(bool on) => SetTint(on, glueTint);

    void SetTint(bool on, Color color)
    {
        if (on)
        {
            if (tinted.Count > 0) return; // already tinted
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                var mat = r.material; // per-player instance (PlayerColor already uses these)
                if (!mat.HasProperty("_Color")) continue;
                tinted.Add((mat, mat.color));
                mat.color = Color.Lerp(mat.color, color, tintStrength);
            }
        }
        else
        {
            foreach (var (mat, original) in tinted)
                if (mat != null) mat.color = original;
            tinted.Clear();
        }
    }

    void OnDisable()
    {
        slowTimer = 0f; slowMultiplier = 1f; burnTimer = 0f; burnDps = 0f;
        turnSlowTimer = 0f; turnMultiplier = 1f;
        SetTint(false);
    }
}
