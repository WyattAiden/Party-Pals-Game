using UnityEngine;

// Put on a projectile prefab to make it burst on impact and hit everyone nearby (e.g. the Paper).
// Set the projectile's own damage to 0 so the target isn't hit twice.
public class AreaBlastOnHit : MonoBehaviour, IProjectileHitEffect
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float maxDamage = 25f;
    [SerializeField] private float minDamage = 15f;
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float physicsForce = 500f;
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 0.5f;
    [Tooltip("Visual only (must not deal damage itself).")]
    [SerializeField] private GameObject burstEffectPrefab;
    [SerializeField] private float effectLifetime = 2f;

    public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition)
    {
        Vector3 center = transform.position;
        if (burstEffectPrefab != null)
            Destroy(Instantiate(burstEffectPrefab, center, Quaternion.identity), effectLifetime);
        AreaExplosion.Detonate(center, radius, maxDamage, minDamage, knockbackForce, physicsForce, owner);
        MultiplayerCamera.Shake(shakeDuration, shakeStrength);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
