using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maker level hazard spawner: at random intervals a NEW chisel (from a prefab) appears above
/// a target (usually a player), a warning marker shows where it will land, then it stabs down.
/// Anyone under it takes damage. Every chisel stays stuck standing in the desk afterwards as a
/// solid obstacle. Chisels already in the scene are never touched.
/// </summary>
public class ChiselStabHazard : MonoBehaviour
{
    [Header("Chisel")]
    [SerializeField] GameObject chiselPrefab;
    [Tooltip("Max lean away from straight up, in degrees, so stuck chisels look natural.")]
    [SerializeField] float maxLean = 10f;
    [Tooltip("0 = unlimited. Otherwise the oldest stuck chisel is removed when a new one would go over the limit.")]
    [SerializeField] int maxStuckChisels = 0;

    [Header("Timing")]
    [SerializeField] float firstStabDelay = 5f;
    [SerializeField] float minInterval = 6f;
    [SerializeField] float maxInterval = 12f;
    [Tooltip("How long the warning marker shows before the chisel comes down.")]
    [SerializeField] float warningTime = 1.2f;
    [SerializeField] float plungeTime = 0.12f;

    [Header("Movement")]
    [Tooltip("Height above the desk the new chisel appears at.")]
    [SerializeField] float spawnHeight = 20f;
    [Tooltip("Height above the desk the tip hovers at before stabbing.")]
    [SerializeField] float hoverHeight = 7f;
    [Tooltip("How far the tip sinks into the desk so it looks stuck.")]
    [SerializeField] float embedDepth = 0.35f;

    [Header("Targeting")]
    [Range(0f, 1f)]
    [Tooltip("Chance to go after a living player instead of a random spot.")]
    [SerializeField] float targetPlayerChance = 0.95f;
    [Tooltip("Seconds at the end of the warning where the spot is locked, so players can still dodge.")]
    [SerializeField] float lockTime = 0.45f;
    [Tooltip("When the spot locks, aim this many seconds ahead of where the player is moving.")]
    [SerializeField] float leadTime = 0.25f;
    [Tooltip("Avoid picking the same player twice in a row when others are available.")]
    [SerializeField] bool spreadTargets = true;
    [Tooltip("Area (world space) for random (non-player) stabs. Players are chased anywhere.")]
    [SerializeField] Bounds stabArea = new Bounds(new Vector3(0f, 0f, -6f), new Vector3(28f, 10f, 14f));

    [Header("Damage")]
    [SerializeField] float damage = 25f;
    [SerializeField] float hitRadius = 1.3f;

    [Header("Feedback (optional)")]
    [SerializeField] GameObject warningMarkerPrefab;
    [SerializeField] Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] AudioClip spawnSound;
    [SerializeField] AudioClip stabSound;

    readonly Queue<GameObject> stuckChisels = new Queue<GameObject>();
    Health lastTarget;
    AudioSource audioSource;
    GameObject marker;

    void Awake()
    {
        if (spawnSound != null || stabSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        CreateMarker();
    }

    void Start()
    {
        if (chiselPrefab == null)
        {
            Debug.LogWarning($"{name}: ChiselStabHazard has no chisel prefab assigned.", this);
            enabled = false;
            return;
        }
        StartCoroutine(StabLoop());
    }

    IEnumerator StabLoop()
    {
        yield return new WaitForSeconds(firstStabDelay);
        while (true)
        {
            yield return StabOnce();
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    IEnumerator StabOnce()
    {
        Health victim = PickVictim();
        Vector3 target = victim != null ? victim.transform.position : RandomPoint();
        Vector3 surface = OnSurface(target);

        // Blade-down pose with a little random lean and twist.
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                       * Quaternion.Euler(Random.Range(-maxLean, maxLean), 0f, Random.Range(-maxLean, maxLean))
                       * Quaternion.Euler(90f, 0f, 0f); // prefab's +Z (blade) points down

        GameObject chisel = Instantiate(chiselPrefab, surface + Vector3.up * spawnHeight, rot);
        chisel.name = chiselPrefab.name + " (Stuck)";
        Vector3 tipOffset = chisel.transform.position - TipWorld(chisel);

        var col = chisel.GetComponent<Collider>();
        if (col == null) col = chisel.AddComponent<BoxCollider>();
        col.enabled = false;
        foreach (var rb in chisel.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.useGravity = false; }

        // Warning: the marker and chisel chase the victim, then lock just ahead of them.
        Play(spawnSound);
        ShowMarker(surface, true);
        float trackTime = Mathf.Max(0f, warningTime - lockTime);
        bool locked = false;
        Vector3 hoverPos = surface + Vector3.up * hoverHeight + tipOffset;
        float height = spawnHeight;
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            if (!locked && victim != null && !victim.IsDead && victim.gameObject.activeInHierarchy)
            {
                if (t < trackTime) surface = OnSurface(victim.transform.position);
                else { surface = OnSurface(victim.transform.position + PlanarVelocity(victim) * leadTime); locked = true; }
            }
            else if (t >= trackTime) locked = true;

            height = Mathf.Lerp(spawnHeight, hoverHeight, EaseOut(Mathf.Clamp01(t / (warningTime * 0.6f))));
            Vector3 want = surface + Vector3.up * height + tipOffset;
            // Follow quickly but not instantly, so it reads as chasing.
            Vector3 cur = chisel.transform.position;
            Vector3 flat = Vector3.Lerp(new Vector3(cur.x, 0f, cur.z), new Vector3(want.x, 0f, want.z), 1f - Mathf.Exp(-12f * Time.deltaTime));
            chisel.transform.SetPositionAndRotation(new Vector3(flat.x, want.y, flat.z), rot);
            ShowMarker(surface, true);
            PulseMarker(t, locked);
            yield return null;
        }
        hoverPos = chisel.transform.position;
        // Plunge straight down onto the locked spot.
        Vector3 stuckPos = new Vector3(hoverPos.x, surface.y - embedDepth + tipOffset.y, hoverPos.z);
        surface = new Vector3(hoverPos.x - tipOffset.x, surface.y, hoverPos.z - tipOffset.z);
        ShowMarker(surface, true);

        // Stab.
        t = 0f;
        while (t < plungeTime)
        {
            t += Time.deltaTime;
            chisel.transform.SetPositionAndRotation(Vector3.LerpUnclamped(hoverPos, stuckPos, EaseIn(Mathf.Clamp01(t / plungeTime))), rot);
            yield return null;
        }
        chisel.transform.SetPositionAndRotation(stuckPos, rot);
        ShowMarker(surface, false);
        Play(stabSound);
        DealDamage(surface);
        col.enabled = true;

        // It stays stuck in the desk from here on.
        stuckChisels.Enqueue(chisel);
        if (maxStuckChisels > 0)
            while (stuckChisels.Count > maxStuckChisels)
            {
                var old = stuckChisels.Dequeue();
                if (old != null) Destroy(old);
            }
    }

    /// World position of the blade tip: the end of the mesh pointing most toward world-down.
    static Vector3 TipWorld(GameObject chisel)
    {
        var mf = chisel.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return chisel.transform.position;
        Transform tr = mf.transform;
        Bounds b = mf.sharedMesh.bounds;
        Vector3 down = tr.InverseTransformDirection(Vector3.down);
        Vector3 a = new Vector3(Mathf.Abs(down.x), Mathf.Abs(down.y), Mathf.Abs(down.z));
        Vector3 axis = (a.x >= a.y && a.x >= a.z) ? new Vector3(Mathf.Sign(down.x), 0, 0)
                     : (a.y >= a.z) ? new Vector3(0, Mathf.Sign(down.y), 0)
                     : new Vector3(0, 0, Mathf.Sign(down.z));
        return tr.TransformPoint(b.center + Vector3.Scale(axis, b.extents));
    }

    Health PickVictim()
    {
        if (Random.value >= targetPlayerChance) return null;
        var candidates = new List<Health>();
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h.IsDead || !h.gameObject.activeInHierarchy) continue;
            candidates.Add(h);
        }
        if (spreadTargets && candidates.Count > 1) candidates.Remove(lastTarget);
        if (candidates.Count == 0) return null;
        lastTarget = candidates[Random.Range(0, candidates.Count)];
        return lastTarget;
    }

    Vector3 RandomPoint() => new Vector3(Random.Range(stabArea.min.x, stabArea.max.x), stabArea.center.y,
                                         Random.Range(stabArea.min.z, stabArea.max.z));

    Vector3 OnSurface(Vector3 p) => new Vector3(p.x, SurfaceHeight(p), p.z);

    static Vector3 PlanarVelocity(Health h)
    {
        var cc = h.GetComponent<CharacterController>();
        Vector3 v = cc != null ? cc.velocity : Vector3.zero;
        if (v == Vector3.zero) { var rb = h.GetComponent<Rigidbody>(); if (rb != null) v = rb.linearVelocity; }
        v.y = 0f;
        return v;
    }

    float SurfaceHeight(Vector3 point)
    {
        // Cast from just above the point so ceilings / props overhead are ignored; take the nearest surface below.
        Vector3 origin = new Vector3(point.x, point.y + 2f, point.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.PositiveInfinity, y = point.y;
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<Health>() != null) continue; // ignore players
            if (hit.collider is CharacterController) continue;
            if (hit.distance < bestDist) { bestDist = hit.distance; y = hit.point.y; }
        }
        return y;
    }

    void DealDamage(Vector3 surface)
    {
        var hitAlready = new HashSet<Health>();
        var cols = Physics.OverlapCapsule(surface, surface + Vector3.up * 3f, hitRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var h = c.GetComponentInParent<Health>();
            if (h == null || !hitAlready.Add(h)) continue;
            h.TakeDamage(damage, gameObject);
        }
    }

    // ---------- Warning marker ----------
    void CreateMarker()
    {
        if (warningMarkerPrefab != null)
        {
            marker = Instantiate(warningMarkerPrefab);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(marker.GetComponent<Collider>());
            var r = marker.GetComponent<Renderer>();
            r.material.color = warningColor;
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", warningColor);
            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", warningColor * 1.5f);
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        marker.name = name + "_StabWarning";
        marker.SetActive(false);
    }

    void ShowMarker(Vector3 surface, bool show)
    {
        if (marker == null) return;
        marker.transform.position = surface + Vector3.up * 0.03f;
        marker.transform.localScale = new Vector3(hitRadius * 2f, 0.01f, hitRadius * 2f);
        marker.SetActive(show);
    }

    void PulseMarker(float t, bool locked)
    {
        if (marker == null) return;
        float speed = locked ? 40f : 14f;
        float s = hitRadius * 2f * (0.85f + 0.15f * Mathf.Sin(t * speed));
        marker.transform.localScale = new Vector3(s, 0.01f, s);
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void OnDestroy()
    {
        if (marker != null) Destroy(marker);
    }

    static float EaseIn(float x) => x * x * x;
    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(stabArea.center, stabArea.size);
    }
}
