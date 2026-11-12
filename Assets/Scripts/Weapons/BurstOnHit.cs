using UnityEngine;

// Put on a projectile (e.g. the pin cushion grenade). When it hits anything it bursts,
// firing a ring of smaller projectiles (e.g. needles) outward at body height.
public class BurstOnHit : MonoBehaviour, IProjectileHitEffect
{
    [SerializeField] private Projectile burstProjectile;
    [SerializeField] private int count = 8;
    [Tooltip("Height above the ground the burst fires at (roughly chest height).")]
    [SerializeField] private float burstHeight = 0.8f;
    [Tooltip("Randomly rotate the ring so needles don't always fly the same directions.")]
    [SerializeField] private bool randomRotation = true;
    [Tooltip("Can the player who fired it be hit by their own burst?")]
    [SerializeField] private bool hitsOwner = false;
    [Tooltip("Visual only (must not deal damage itself).")]
    [SerializeField] private GameObject burstEffectPrefab;
    [SerializeField] private float effectLifetime = 2f;
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeStrength = 0.3f;

    public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition)
    {
        if (burstProjectile == null || count <= 0) return;

        // Burst from just before the impact (so it doesn't start inside a wall), at body height above the ground
        Vector3 center = fromPosition;
        if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 10f, ~0, QueryTriggerInteraction.Ignore)
            && ground.collider.GetComponentInParent<Health>() == null)
            center.y = ground.point.y + burstHeight;

        if (burstEffectPrefab != null)
            Destroy(Instantiate(burstEffectPrefab, center, Quaternion.identity), effectLifetime);
        MultiplayerCamera.Shake(shakeDuration, shakeStrength);

        float start = randomRotation ? Random.Range(0f, 360f) : 0f;
        for (int i = 0; i < count; i++)
        {
            Quaternion rot = Quaternion.Euler(0f, start + i * 360f / count, 0f);
            Projectile p = Instantiate(burstProjectile, center, rot);
            p.owner = hitsOwner ? null : owner;
        }
    }
}
