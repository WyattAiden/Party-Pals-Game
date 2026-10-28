using UnityEngine;

// Dropped bomb (used by BombSpawn in WarLevel). Replaces DuelShooter's Bomb script.
// When it hits something it waits Fuse Delay seconds, then spawns the explosion
// prefab (ExplosionParticle1 does the damage) and removes itself.
[RequireComponent(typeof(Rigidbody))]
public class Bomb : MonoBehaviour
{
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Seconds between landing and exploding. 0 = explode on impact.")]
    [SerializeField] private float fuseDelay = 0f;
    [Tooltip("Explode anyway after this many seconds, even if it never lands. 0 = never.")]
    [SerializeField] private float maxLifetime = 10f;
    [Tooltip("How long the spawned explosion stays before it is cleaned up.")]
    [SerializeField] private float explosionLifetime = 6f;

    private bool landed;
    private bool exploded;

    void Start()
    {
        if (maxLifetime > 0f) Invoke(nameof(Explode), maxLifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed || exploded) return;
        landed = true;
        if (fuseDelay <= 0f) Explode();
        else
        {
            CancelInvoke(nameof(Explode));
            Invoke(nameof(Explode), fuseDelay);
        }
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            if (explosionLifetime > 0f) Destroy(fx, explosionLifetime);
        }
        Destroy(gameObject);
    }
}
