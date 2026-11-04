using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackForce = 30f;

    [Header("Arc (leave at 0 for a straight shot)")]
    [SerializeField] private float gravity = 0f;
    [SerializeField] private float launchUpSpeed = 0f;
    private float verticalSpeed;

    [HideInInspector] public GameObject owner;

    // Where the projectile was last frame (used to find the exact surface it hit)
    public Vector3 LastPosition { get; private set; }

    void Start()
    {
        Destroy(gameObject, lifetime);
        verticalSpeed = launchUpSpeed;
        LastPosition = transform.position;
    }

    void Update()
    {
        LastPosition = transform.position;
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        if (gravity != 0f || verticalSpeed != 0f)
        {
            verticalSpeed -= gravity * Time.deltaTime;
            transform.position += Vector3.up * verticalSpeed * Time.deltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;
        if (other.GetComponent<WeaponPickup>() != null) return; // fly through pickups
        if (other.GetComponent<Projectile>() != null) return;   // shots don't destroy each other (spread weapons)
        if (other.GetComponent<GluePuddle>() != null) return;   // fly over glue puddles

        if (other.TryGetComponent(out Health health))
            health.TakeDamage(damage, owner);

        if (other.TryGetComponent(out EnemyKnockBack enemy))
            enemy.ApplyKnockback(transform.forward, knockbackForce);

        // Special effects on this projectile (glue, etc.) - see IProjectileHitEffect
        foreach (var effect in GetComponents<IProjectileHitEffect>())
            effect.OnProjectileHit(other, owner, LastPosition);

        Destroy(gameObject);
    }
}