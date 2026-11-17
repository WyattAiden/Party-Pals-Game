using UnityEngine;

// Spawned on the fire point when the Glue Gun fires (PowerWeaponData.attachedPrefab).
// Squeezes out a quick stream of small glue droplets that follow the player's aim,
// each one joined to the one before by a sticky strand (see GlueStrand).
public class GlueSquirt : MonoBehaviour, IPlacedByPlayer
{
    [SerializeField] private Projectile dropletPrefab;
    [Tooltip("How long one trigger squeeze keeps glue coming out.")]
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float dropletsPerSecond = 35f;
    [Tooltip("Random left/right wobble per droplet, in degrees.")]
    [SerializeField] private float wobbleAngle = 2.5f;
    [Tooltip("Random up/down wobble per droplet, in degrees.")]
    [SerializeField] private float pitchWobble = 2f;
    [Tooltip("Random size range for each droplet (1 = prefab size).")]
    [SerializeField] private Vector2 sizeRange = new Vector2(0.8f, 1.2f);
    [Tooltip("Droplets get smaller toward the end of the squirt, like the glue running out.")]
    [Range(0.1f, 1f)] [SerializeField] private float endSizeMultiplier = 0.55f;

    private GameObject owner;
    private float age;
    private float spawnAccumulator;
    private GlueStrand previous;

    public void SetOwner(GameObject newOwner) => owner = newOwner;

    void Start()
    {
        SpawnDroplet(); // first droplet right away so the shot feels instant
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= duration) { Destroy(gameObject); return; }

        spawnAccumulator += Time.deltaTime * dropletsPerSecond;
        while (spawnAccumulator >= 1f)
        {
            spawnAccumulator -= 1f;
            SpawnDroplet();
        }
    }

    private void SpawnDroplet()
    {
        if (dropletPrefab == null) return;

        Quaternion rot = transform.rotation *
                         Quaternion.Euler(Random.Range(-pitchWobble, pitchWobble), Random.Range(-wobbleAngle, wobbleAngle), 0f);
        Projectile p = Instantiate(dropletPrefab, transform.position, rot);
        p.owner = owner;

        float t = duration > 0f ? Mathf.Clamp01(age / duration) : 0f;
        p.transform.localScale *= Random.Range(sizeRange.x, sizeRange.y) * Mathf.Lerp(1f, endSizeMultiplier, t);

        var strand = p.GetComponent<GlueStrand>();
        if (strand != null)
        {
            strand.Link(previous);
            previous = strand;
        }
    }
}
