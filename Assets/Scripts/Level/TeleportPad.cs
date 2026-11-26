using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pad that sends any player who steps on it to a RANDOM other pad.
/// By default it picks from every other TeleportPad in the level; fill in the Destinations
/// list to limit it to specific pads instead.
///
/// - Only players (anything with a CharacterController) are teleported.
/// - After arriving, a player must step OFF the destination pad before it can send them back,
///   so nobody ping-pongs between pads.
/// - Pads glow in their own colour (set per pad) so linked pairs are easy to read.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TeleportPad : MonoBehaviour
{
    [Header("Destinations")]
    [Tooltip("Pads this one can send players to (one is picked at random). Leave empty to use every other pad in the level.")]
    [SerializeField] private TeleportPad[] destinations = new TeleportPad[0];

    [Header("Timing")]
    [Tooltip("Seconds a player has to wait after teleporting before ANY pad can teleport them again.")]
    [SerializeField] private float playerCooldown = 0.75f;

    [Header("Look")]
    [Tooltip("Colour of this pad's glow and beam. Give each linked pair its own colour.")]
    [SerializeField] private Color padColor = new Color(0.2f, 0.9f, 1f);
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private Renderer beamRenderer;
    [SerializeField] private float glowIntensity = 2.5f;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float pulseSpeed = 3f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip teleportSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

    private static readonly List<TeleportPad> allPads = new List<TeleportPad>();
    void OnEnable() { if (!allPads.Contains(this)) allPads.Add(this); }
    void OnDisable() { allPads.Remove(this); }

    // Every pad this one could send a player to right now
    private List<TeleportPad> GetCandidates()
    {
        var list = new List<TeleportPad>();
        IEnumerable<TeleportPad> source = (destinations != null && destinations.Length > 0) ? destinations : (IEnumerable<TeleportPad>)allPads;
        foreach (var pad in source)
            if (pad != null && pad != this && pad.isActiveAndEnabled && !list.Contains(pad)) list.Add(pad);
        return list;
    }

    // Players that just arrived here and haven't stepped off yet
    private readonly HashSet<CharacterController> arrivals = new HashSet<CharacterController>();
    // Shared across all pads: last time each player was teleported
    private static readonly Dictionary<CharacterController, float> lastTeleportTime = new Dictionary<CharacterController, float>();

    private MaterialPropertyBlock block;
    private float flash; // 1 right after a teleport, fades to 0

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int TintId = Shader.PropertyToID("_TintColor");

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        block = new MaterialPropertyBlock();
        ApplyColors(0f);
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<CharacterController>();
        if (player == null || !player.enabled) return;        // not a player, or dead
        if (arrivals.Contains(player)) return;                // just arrived here, must step off first

        if (lastTeleportTime.TryGetValue(player, out float last) && Time.time - last < playerCooldown) return;

        var candidates = GetCandidates();
        if (candidates.Count == 0) return;
        TeleportPlayer(player, candidates[Random.Range(0, candidates.Count)]);
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<CharacterController>();
        if (player != null) arrivals.Remove(player);
    }

    private void TeleportPlayer(CharacterController player, TeleportPad destination)
    {
        Transform t = player.transform;

        // Keep the player's height above the pad so they land standing on the destination
        float heightAbovePad = t.position.y - transform.position.y;
        Vector3 target = destination.transform.position + Vector3.up * Mathf.Max(heightAbovePad, 0.05f);

        destination.arrivals.Add(player);
        lastTeleportTime[player] = Time.time;

        // CharacterController overrides transform changes while enabled, so toggle it
        player.enabled = false;
        t.position = target;
        player.enabled = true;
        Physics.SyncTransforms();

        Flash();
        destination.Flash();

        if (teleportSound != null)
        {
            AudioSource.PlayClipAtPoint(teleportSound, transform.position, soundVolume);
            AudioSource.PlayClipAtPoint(teleportSound, destination.transform.position, soundVolume);
        }
    }

    public void Flash() => flash = 1f;

    void Update()
    {
        // Clear the "must step off first" lock once a player is no longer on this pad.
        // (Checked by position, because Unity skips OnTriggerExit if a player is moved/disabled while on the pad.)
        if (arrivals.Count > 0)
        {
            Bounds padBounds = GetComponent<Collider>().bounds;
            arrivals.RemoveWhere(p => p == null || !p.enabled || !padBounds.Intersects(p.bounds));
        }

        if (glowRenderer != null) glowRenderer.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 3f);
        ApplyColors(flash);
    }

    private void ApplyColors(float flashAmount)
    {
        if (block == null) block = new MaterialPropertyBlock();
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);

        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, padColor);
            block.SetColor(EmissionId, padColor * glowIntensity * (pulse + flashAmount * 2f));
            glowRenderer.SetPropertyBlock(block);
        }

        if (beamRenderer != null)
        {
            beamRenderer.GetPropertyBlock(block);
            Color beam = padColor * (0.35f * pulse + flashAmount);
            beam.a = 1f;
            block.SetColor(TintId, beam);
            beamRenderer.SetPropertyBlock(block);
            float s = 1f + flashAmount * 0.4f;
            beamRenderer.transform.localScale = new Vector3(beamBaseScale.x * s, beamBaseScale.y * (1f + flashAmount), beamBaseScale.z * s);
        }
    }

    private Vector3 beamBaseScale = Vector3.one;
    void Start()
    {
        if (beamRenderer != null) beamBaseScale = beamRenderer.transform.localScale;
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (beamRenderer != null) beamBaseScale = beamRenderer.transform.localScale;
        if (glowRenderer != null) ApplyColors(0f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = padColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
        IEnumerable<TeleportPad> targets = (destinations != null && destinations.Length > 0)
            ? (IEnumerable<TeleportPad>)destinations
            : FindObjectsByType<TeleportPad>(FindObjectsSortMode.None);
        foreach (var destination in targets)
        {
            if (destination == null || destination == this) continue;
            Vector3 a = transform.position + Vector3.up * 0.5f;
            Vector3 b = destination.transform.position + Vector3.up * 0.5f;
            Gizmos.DrawLine(a, b);
            // little arrow head pointing at the destination
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up) * 0.5f;
            Gizmos.DrawLine(b, b - dir + side);
            Gizmos.DrawLine(b, b - dir - side);
        }
    }

    void OnDestroy()
    {
        // keep the static table tidy between rounds / scene loads
        var dead = new List<CharacterController>();
        foreach (var kv in lastTeleportTime) if (kv.Key == null) dead.Add(kv.Key);
        foreach (var k in dead) lastTeleportTime.Remove(k);
    }
}
