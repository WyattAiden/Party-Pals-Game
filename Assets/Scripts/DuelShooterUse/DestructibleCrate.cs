using UnityEngine;

// Adapted from DuelShooter's Crate_A / DestructBlock1 / DamageControl, using our Health script.
// Setup: a crate with a NON-trigger collider and a Health component on the SAME object
// (projectiles look for Health on whatever collider they hit). Set Health's Max Health low,
// e.g. 30. A Rigidbody is optional - add one if you want explosions to knock it around.
// When it breaks it plays an effect and can drop a random item, e.g. a WeaponPickup prefab
// (set that pickup's Respawn Delay to 0 so it doesn't respawn on the spot).
[RequireComponent(typeof(Health))]
public class DestructibleCrate : MonoBehaviour
{
    [SerializeField] private GameObject breakEffectPrefab;
    [SerializeField] private float effectLifetime = 3f;

    [Header("Drops")]
    [Tooltip("One of these is chosen at random when the crate breaks.")]
    [SerializeField] private GameObject[] dropPrefabs;
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.5f;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("The crate is removed if it falls below this height (e.g. knocked off the map).")]
    [SerializeField] private float killHeight = -10f;

    private Health health;

    void Awake() => health = GetComponent<Health>();
    void OnEnable() => health.Died += Break;
    void OnDisable() => health.Died -= Break;

    void Update()
    {
        if (transform.position.y < killHeight) Destroy(gameObject);
    }

    private void Break()
    {
        if (breakEffectPrefab != null)
            Destroy(Instantiate(breakEffectPrefab, transform.position, Quaternion.identity), effectLifetime);

        if (dropPrefabs != null && dropPrefabs.Length > 0 && Random.value < dropChance)
        {
            GameObject drop = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
            if (drop != null)
            {
                Vector3 pos = transform.position;
                pos.y = 0f;
                Instantiate(drop, pos + dropOffset, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}
