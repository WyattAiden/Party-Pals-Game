using System.Collections.Generic;
using UnityEngine;

// Adapted from DuelShooter's Explosion (fixed the null-reference bug and added
// damage falloff, player knockback and physics push).
// Put on an explosion effect prefab: it hits everything in range the moment it spawns.
// Other scripts can call AreaExplosion.Detonate(...) directly instead.
public class AreaExplosion : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [Tooltip("Damage at the centre of the blast.")]
    [SerializeField] private float maxDamage = 25f;
    [Tooltip("Damage at the edge of the blast.")]
    [SerializeField] private float minDamage = 8f;
    [SerializeField] private float knockbackForce = 15f;
    [Tooltip("Push applied to non-kinematic Rigidbodies (crates, barrels...).")]
    [SerializeField] private float physicsForce = 800f;

    [Header("Camera shake")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeStrength = 0.6f;

    [Tooltip("Destroy this object after this many seconds. 0 = leave it to whatever spawned it.")]
    [SerializeField] private float destroyAfter = 0f;

    void Start()
    {
        Detonate(transform.position, radius, maxDamage, minDamage, knockbackForce, physicsForce);
        MultiplayerCamera.Shake(shakeDuration, shakeStrength);
        if (destroyAfter > 0f) Destroy(gameObject, destroyAfter);
    }

    public static void Detonate(Vector3 center, float radius, float maxDamage, float minDamage,
                                float knockbackForce, float physicsForce)
    {
        var damaged = new HashSet<Health>();
        var pushedPlayers = new HashSet<PlayerController>();
        var pushedBodies = new HashSet<Rigidbody>();

        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            // 0 at the centre, 1 at the edge
            float t = Mathf.Clamp01(Vector3.Distance(col.bounds.ClosestPoint(center), center) / radius);

            Health health = col.GetComponentInParent<Health>();
            if (health != null && damaged.Add(health))
                health.TakeDamage(Mathf.Lerp(maxDamage, minDamage, t));

            PlayerController player = col.GetComponentInParent<PlayerController>();
            if (player != null && pushedPlayers.Add(player))
            {
                Vector3 dir = player.transform.position - center;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                player.ApplyKnockBack(dir.normalized * knockbackForce * Mathf.Lerp(1f, 0.5f, t));
            }

            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic && pushedBodies.Add(rb))
                rb.AddExplosionForce(physicsForce, center, radius, 0.5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
