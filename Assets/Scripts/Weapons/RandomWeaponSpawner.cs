using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// RANDOM WEAPON SPAWNING (experimental - easy to undo)
// Drop the RandomWeaponSpawner prefab into a level. While this component is enabled it takes over
// from the level's WeaponSpawnPoints (they are switched off for the round). Untick it or delete the
// object and the old fixed-spawn-point system works exactly as before.
//
// Tweak: how many weapons (amount), how often (rate), and how rare each weapon is (rarity).
[DefaultExecutionOrder(-100)]
public class RandomWeaponSpawner : MonoBehaviour
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }
    public enum LocationMode
    {
        [Tooltip("Pick a random free WeaponSpawnPoint already placed in the level.")]
        RandomSpawnPoint,
        [Tooltip("Pick a random spot on the ground inside the Spawn Area box.")]
        RandomPointInArea
    }

    [Serializable]
    public class WeaponEntry
    {
        public WeaponPickup pickupPrefab;
        public Rarity rarity = Rarity.Common;
    }

    [Header("Weapons + rarity")]
    [Tooltip("Weapons that can spawn. If empty, everything in the Fallback Pool is used as Common.")]
    [SerializeField] private List<WeaponEntry> weapons = new List<WeaponEntry>();
    [Tooltip("Used only when the Weapons list above is empty.")]
    [SerializeField] private WeaponSpawnPool fallbackPool;
    [Tooltip("Relative chance for each rarity. Higher = more often. 0 = never.")]
    [SerializeField, Min(0f)] private float commonWeight = 60f;
    [SerializeField, Min(0f)] private float uncommonWeight = 25f;
    [SerializeField, Min(0f)] private float rareWeight = 12f;
    [SerializeField, Min(0f)] private float legendaryWeight = 3f;
    [Tooltip("Try not to spawn the same weapon twice in a row.")]
    [SerializeField] private bool avoidRepeats = true;

    [Header("Amount")]
    [Tooltip("Most weapon pickups lying on the map at the same time.")]
    [SerializeField, Min(1)] private int maxOnMap = 3;
    [Tooltip("How many spawn together at the start of the round (capped by Max On Map).")]
    [SerializeField, Min(0)] private int startingCount = 1;
    [Tooltip("Total weapons allowed to spawn in one round. 0 = unlimited.")]
    [SerializeField, Min(0)] private int maxPerRound = 0;

    [Header("Rate")]
    [Tooltip("Delay before the starting weapons appear (min, max seconds).")]
    [SerializeField] private Vector2 firstSpawnDelay = new Vector2(3f, 6f);
    [Tooltip("Time between spawns while below Max On Map (min, max seconds).")]
    [SerializeField] private Vector2 spawnInterval = new Vector2(6f, 12f);
    [Tooltip("Unclaimed pickups vanish after this many seconds, making room for a new one. 0 = stay forever.")]
    [SerializeField, Min(0f)] private float pickupLifetime = 0f;

    [Header("Where")]
    [SerializeField] private LocationMode location = LocationMode.RandomSpawnPoint;
    [Tooltip("How high above the ground the pickup floats.")]
    [SerializeField] private float pickupHeight = 1f;
    [Tooltip("Don't spawn closer than this to any player.")]
    [SerializeField, Min(0f)] private float minDistanceFromPlayers = 3f;
    [Tooltip("Don't spawn closer than this to another pickup.")]
    [SerializeField, Min(0f)] private float minDistanceBetweenPickups = 3f;

    [Header("Area mode only")]
    [Tooltip("Box (relative to this object) to scatter weapons in. Drawn yellow in the Scene view.")]
    [SerializeField] private Vector3 areaSize = new Vector3(30f, 10f, 30f);
    [Tooltip("What counts as ground to land on.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Ground steeper than this (degrees) is skipped.")]
    [SerializeField, Range(0f, 89f)] private float maxSlope = 30f;
    [Tooltip("Free space needed around the spot (stops weapons spawning inside walls/props).")]
    [SerializeField, Min(0.1f)] private float clearanceRadius = 0.6f;

    private readonly List<WeaponPickup> active = new List<WeaponPickup>();
    private readonly Dictionary<WeaponPickup, Transform> occupiedPoint = new Dictionary<WeaponPickup, Transform>();
    private readonly List<Transform> spawnPoints = new List<Transform>();
    private WeaponPickup lastPrefab;
    private int spawnedThisRound;

    void Awake()
    {
        // Take over from the fixed spawn points (Awake runs before their Start, so they never spawn).
        foreach (var p in FindObjectsByType<WeaponSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            spawnPoints.Add(p.transform);
            p.enabled = false;
        }
    }

    void OnDisable()
    {
        // Ticking the component off mid-play hands control back to the old spawn points.
        StopAllCoroutines();
        foreach (var t in spawnPoints)
            if (t != null && t.TryGetComponent(out WeaponSpawnPoint p)) p.enabled = true;
    }

    void OnEnable()
    {
        foreach (var t in spawnPoints)
            if (t != null && t.TryGetComponent(out WeaponSpawnPoint p)) p.enabled = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(RandomIn(firstSpawnDelay));

        int start = Mathf.Min(startingCount, maxOnMap);
        for (int i = 0; i < start; i++) TrySpawn();

        while (true)
        {
            yield return new WaitForSeconds(RandomIn(spawnInterval));
            active.RemoveAll(p => p == null);
            if (active.Count < maxOnMap) TrySpawn();
        }
    }

    private bool TrySpawn()
    {
        if (maxPerRound > 0 && spawnedThisRound >= maxPerRound) return false;

        WeaponPickup prefab = PickWeapon();
        if (prefab == null) return false;
        if (!TryFindSpot(out Vector3 pos, out Transform point)) return false;

        var pickup = Instantiate(prefab, pos, Quaternion.identity);
        pickup.MakeOneTime();
        pickup.PickedUp += OnPickedUp;
        active.Add(pickup);
        if (point != null) occupiedPoint[pickup] = point;
        lastPrefab = prefab;
        spawnedThisRound++;

        if (pickupLifetime > 0f) StartCoroutine(Expire(pickup, pickupLifetime));
        return true;
    }

    private void OnPickedUp(WeaponPickup pickup)
    {
        pickup.PickedUp -= OnPickedUp;
        Forget(pickup);
    }

    private IEnumerator Expire(WeaponPickup pickup, float after)
    {
        yield return new WaitForSeconds(after);
        if (pickup == null) yield break;
        Forget(pickup);
        Destroy(pickup.gameObject);
    }

    private void Forget(WeaponPickup pickup)
    {
        active.Remove(pickup);
        occupiedPoint.Remove(pickup);
    }

    // ---------- Rarity ----------

    private float RarityWeight(Rarity r)
    {
        switch (r)
        {
            case Rarity.Uncommon:  return uncommonWeight;
            case Rarity.Rare:      return rareWeight;
            case Rarity.Legendary: return legendaryWeight;
            default:               return commonWeight;
        }
    }

    private List<WeaponEntry> Candidates()
    {
        if (weapons != null && weapons.Count > 0) return weapons;
        var list = new List<WeaponEntry>();
        if (fallbackPool != null && fallbackPool.entries != null)
            foreach (var e in fallbackPool.entries)
                if (e.pickupPrefab != null && e.weight > 0f) list.Add(new WeaponEntry { pickupPrefab = e.pickupPrefab });
        return list;
    }

    // Picks a rarity tier first (by tier weight), then a weapon within that tier.
    // So adding more Commons doesn't make Legendaries rarer.
    private WeaponPickup PickWeapon()
    {
        var all = Candidates();
        WeaponPickup avoid = avoidRepeats ? lastPrefab : null;

        var usable = all.FindAll(e => e.pickupPrefab != null && RarityWeight(e.rarity) > 0f && e.pickupPrefab != avoid);
        if (usable.Count == 0) usable = all.FindAll(e => e.pickupPrefab != null && RarityWeight(e.rarity) > 0f);
        if (usable.Count == 0) return null;

        float total = 0f;
        var tiers = new List<Rarity>();
        foreach (Rarity r in Enum.GetValues(typeof(Rarity)))
            if (usable.Exists(e => e.rarity == r)) { tiers.Add(r); total += RarityWeight(r); }

        float roll = UnityEngine.Random.Range(0f, total);
        Rarity chosen = tiers[tiers.Count - 1];
        foreach (var r in tiers)
        {
            roll -= RarityWeight(r);
            if (roll <= 0f) { chosen = r; break; }
        }

        var inTier = usable.FindAll(e => e.rarity == chosen);
        return inTier[UnityEngine.Random.Range(0, inTier.Count)].pickupPrefab;
    }

    // ---------- Location ----------

    private bool TryFindSpot(out Vector3 pos, out Transform point)
    {
        point = null;
        pos = default;
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        if (location == LocationMode.RandomSpawnPoint)
        {
            var free = spawnPoints.FindAll(t => t != null && !occupiedPoint.ContainsValue(t));
            Shuffle(free);
            // Prefer spots away from players; if none, take any free one.
            foreach (var t in free)
            {
                Vector3 p = t.position + Vector3.up * pickupHeight;
                if (FarFromPlayers(p, players) && FarFromPickups(p)) { pos = p; point = t; return true; }
            }
            if (free.Count > 0) { point = free[0]; pos = point.position + Vector3.up * pickupHeight; return true; }
            return false;
        }

        float cosMax = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 local = new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f) * areaSize.x,
                0.5f * areaSize.y,
                UnityEngine.Random.Range(-0.5f, 0.5f) * areaSize.z);
            Vector3 from = transform.TransformPoint(local);

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, areaSize.y, groundMask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < cosMax) continue;

            Vector3 p = hit.point + Vector3.up * pickupHeight;
            Vector3 bottom = hit.point + Vector3.up * (clearanceRadius + 0.05f);
            if (Physics.CheckCapsule(bottom, p, clearanceRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!FarFromPlayers(p, players) || !FarFromPickups(p)) continue;

            pos = p;
            return true;
        }
        return false; // no good spot this time - will try again next interval
    }

    private bool FarFromPlayers(Vector3 p, PlayerController[] players)
    {
        foreach (var pl in players)
            if (pl != null && (pl.transform.position - p).sqrMagnitude < minDistanceFromPlayers * minDistanceFromPlayers) return false;
        return true;
    }

    private bool FarFromPickups(Vector3 p)
    {
        foreach (var a in active)
            if (a != null && (a.transform.position - p).sqrMagnitude < minDistanceBetweenPickups * minDistanceBetweenPickups) return false;
        return true;
    }

    private static float RandomIn(Vector2 range) => UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    // Right-click the component header > "Fill weapons from pool" to copy the pool into the list.
    [ContextMenu("Fill weapons from pool")]
    private void FillFromPool()
    {
        if (fallbackPool == null || fallbackPool.entries == null) return;
        UnityEditor.Undo.RecordObject(this, "Fill weapons from pool");
        weapons.Clear();
        foreach (var e in fallbackPool.entries)
            if (e.pickupPrefab != null) weapons.Add(new WeaponEntry { pickupPrefab = e.pickupPrefab });
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnValidate()
    {
        startingCount = Mathf.Min(startingCount, maxOnMap);
    }
#endif

    void OnDrawGizmosSelected()
    {
        if (location != LocationMode.RandomPointInArea) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, areaSize);
    }
}
