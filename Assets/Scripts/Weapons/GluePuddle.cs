using System.Collections.Generic;
using UnityEngine;

// A sticky glue puddle left where a Glue Gun shot lands. Slows (and tints) players standing in it.
// Needs a trigger collider. Its "up" points away from the surface it is stuck to.
[RequireComponent(typeof(Collider))]
public class GluePuddle : MonoBehaviour
{
    [Range(0.1f, 1f)] [SerializeField] private float slowTo = 0.6f;
    [Tooltip("How long the slow lingers after stepping out.")]
    [SerializeField] private float lingerTime = 0.4f;
    [SerializeField] private float lifetime = 6f;
    [Tooltip("Shrinks away over this many seconds at the end of its life.")]
    [SerializeField] private float shrinkTime = 1f;
    [Tooltip("Pops up from nothing over this many seconds when it lands.")]
    [SerializeField] private float growTime = 0.15f;

    [Header("Growing (glue stream)")]
    [Tooltip("How much bigger it gets each time more glue lands on it.")]
    [SerializeField] private float growPerFeed = 0.08f;
    [SerializeField] private float maxSize = 1.8f;

    private static readonly List<GluePuddle> active = new List<GluePuddle>();

    private float age;
    private Vector3 fullScale;
    private float size = 1f;
    private float targetSize = 1f;

    // Nearest puddle within radius that's stuck to a surface facing the same way (so floor glue doesn't join wall glue)
    public static GluePuddle FindNear(Vector3 point, Vector3 normal, float radius)
    {
        GluePuddle best = null;
        float bestDist = float.MaxValue;
        foreach (var p in active)
        {
            if (p == null || Vector3.Dot(p.transform.up, normal) < 0.8f) continue;
            float d = Vector3.Distance(p.transform.position, point);
            if (d <= radius * p.size && d < bestDist) { best = p; bestDist = d; }
        }
        return best;
    }

    // More glue landed here: grow a little and start the lifetime over
    public void Feed()
    {
        targetSize = Mathf.Min(targetSize + growPerFeed, maxSize);
        age = Mathf.Min(age, growTime);
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        fullScale = transform.localScale;
        transform.localScale = fullScale * 0.2f;
    }

    void Update()
    {
        age += Time.deltaTime;
        float grow = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
        float shrink = shrinkTime > 0f ? Mathf.Clamp01((lifetime - age) / shrinkTime) : 1f;
        size = Mathf.MoveTowards(size, targetSize, Time.deltaTime * 2f);
        transform.localScale = fullScale * size * Mathf.Lerp(0.2f, 1f, grow) * shrink;
        if (age >= lifetime) Destroy(gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        var status = other.GetComponentInParent<PlayerStatusEffects>();
        if (status != null) status.ApplyGlue(0f, 0f, slowTo, lingerTime, null);
    }
}
