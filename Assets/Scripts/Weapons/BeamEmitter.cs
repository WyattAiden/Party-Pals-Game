using System.Collections.Generic;
using UnityEngine;

// A continuous beam spawned on the player's fire point (see PowerWeaponData.attachedPrefab).
// Fires for a few seconds, follows the player's aim, bounces off walls, and damages + slows players it touches.
[RequireComponent(typeof(LineRenderer))]
public class BeamEmitter : MonoBehaviour, IPlacedByPlayer
{
    [Header("Beam")]
    [SerializeField] private float duration = 3f;
    [Tooltip("Total length of the beam, including bounces.")]
    [SerializeField] private float maxLength = 30f;
    [SerializeField] private int maxBounces = 3;
    [Tooltip("If off, the beam passes through players and keeps going.")]
    [SerializeField] private bool stopAtPlayers = true;
    [Tooltip("Can the beam hit the player firing it after bouncing back?")]
    [SerializeField] private bool canHitOwnerAfterBounce = false;
    [SerializeField] private float beamWidth = 0.25f;

    [Header("Damage")]
    [SerializeField] private float damagePerSecond = 12f;
    [SerializeField] private float damageTickInterval = 0.25f;

    [Header("Slow (players hit)")]
    [Tooltip("Speed of players caught in the beam. 1 = not slowed.")]
    [Range(0.1f, 1f)] [SerializeField] private float slowTo = 1f;
    [Tooltip("How long the slow lingers after leaving the beam.")]
    [SerializeField] private float slowLinger = 0.3f;
    [SerializeField] private Color slowTint = new Color(0.45f, 0.8f, 1f);

    [Header("Shooter (heavy weapon)")]
    [Tooltip("Speed of the player firing the beam while it's on. 1 = no slowdown.")]
    [Range(0.1f, 1f)] [SerializeField] private float ownerSpeedWhileFiring = 0.5f;
    [Tooltip("How fast the player firing can turn/aim while it's on. 1 = normal.")]
    [Range(0.05f, 1f)] [SerializeField] private float ownerTurnWhileFiring = 0.3f;

    private LineRenderer line;
    private GameObject owner;
    private PlayerStatusEffects ownerStatus;
    private float startTime;
    private float tickTimer;
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly HashSet<Health> touching = new HashSet<Health>();

    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
        ownerStatus = owner != null ? owner.GetComponent<PlayerStatusEffects>() : null;
    }

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        startTime = Time.time;
    }

    void Update()
    {
        float age = Time.time - startTime;
        if (age >= duration) { Destroy(gameObject); return; }

        TraceBeam();

        // Flicker a little, and thin out over the last 0.2s
        float fade = Mathf.Clamp01((duration - age) / 0.2f);
        float w = beamWidth * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f)) * fade;
        line.startWidth = w;
        line.endWidth = w;

        if (ownerStatus != null)
        {
            if (ownerSpeedWhileFiring < 1f) ownerStatus.ApplySlow(ownerSpeedWhileFiring, 0.1f, null);
            if (ownerTurnWhileFiring < 1f) ownerStatus.ApplyTurnSlow(ownerTurnWhileFiring, 0.1f);
        }

        tickTimer -= Time.deltaTime;
        bool tick = tickTimer <= 0f;
        if (tick) tickTimer = damageTickInterval;

        foreach (var health in touching)
        {
            var status = health.GetComponent<PlayerStatusEffects>();
            if (status != null && slowTo < 1f) status.ApplySlow(slowTo, slowLinger, slowTint);
            if (tick) health.TakeDamage(damagePerSecond * damageTickInterval, owner);
        }
    }

    private void TraceBeam()
    {
        points.Clear();
        touching.Clear();

        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();

        points.Add(origin);
        float remaining = maxLength;
        int bounces = 0;

        while (remaining > 0.01f)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, remaining, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool found = false;
            RaycastHit hit = default;
            Health hitHealth = null;
            foreach (var h in hits)
            {
                var hp = h.collider.GetComponentInParent<Health>();
                if (hp != null && hp.gameObject == owner && !(canHitOwnerAfterBounce && bounces > 0)) continue;
                if (hp != null && hp.IsDead) continue;
                hit = h; hitHealth = hp; found = true;
                break;
            }

            if (!found)
            {
                points.Add(origin + dir * remaining);
                break;
            }

            points.Add(hit.point);
            remaining -= hit.distance;

            if (hitHealth != null)
            {
                touching.Add(hitHealth);
                if (stopAtPlayers) break;
                origin = hit.point + dir * 0.05f; // carry on through them
                continue;
            }

            // Scenery: bounce (flattened so the beam stays at the same height)
            if (bounces >= maxBounces) break;
            Vector3 n = hit.normal;
            n.y = 0f;
            if (n.sqrMagnitude < 0.01f) break; // hit the floor/a slope - just stop
            n.Normalize();
            dir = Vector3.Reflect(dir, n);
            dir.y = 0f;
            dir.Normalize();
            origin = hit.point + n * 0.02f;
            bounces++;
        }

        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
    }
}
