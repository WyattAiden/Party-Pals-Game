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

    private float age;
    private Vector3 fullScale;

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
        transform.localScale = fullScale * Mathf.Lerp(0.2f, 1f, grow) * shrink;
        if (age >= lifetime) Destroy(gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        var status = other.GetComponentInParent<PlayerStatusEffects>();
        if (status != null) status.ApplyGlue(0f, 0f, slowTo, lingerTime, null);
    }
}
