using UnityEngine;

// Put on a projectile prefab (next to Projectile).
// Hitting a player burns + slows them. Hitting a surface (ground, wall, tank...) leaves a sticky puddle there.
public class GlueOnHit : MonoBehaviour, IProjectileHitEffect
{
    [Header("Burn (hot glue)")]
    [SerializeField] private float burnDamagePerSecond = 4f;
    [SerializeField] private float burnDuration = 3f;

    [Header("Slow (sticky)")]
    [Tooltip("Speed while glued: 0.5 = half speed.")]
    [Range(0.1f, 1f)] [SerializeField] private float slowTo = 0.5f;
    [SerializeField] private float slowDuration = 2.5f;

    [Header("Puddle")]
    [Tooltip("Spawned where the glue hits a surface. Leave empty for no puddles.")]
    [SerializeField] private GluePuddle puddlePrefab;

    // Called by Projectile when it hits something
    public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition)
    {
        var status = target.GetComponentInParent<PlayerStatusEffects>();
        if (status != null)
        {
            status.ApplyGlue(burnDamagePerSecond, burnDuration, slowTo, slowDuration, owner);
            return;
        }

        if (puddlePrefab == null || target.isTrigger) return; // only stick to solid surfaces

        // Find the exact point + surface direction by tracing along the path the blob was flying
        Vector3 travel = transform.position - fromPosition;
        Vector3 dir = travel.sqrMagnitude > 0.0001f ? travel.normalized : Vector3.down;
        Vector3 point;
        Vector3 normal;
        if (target.Raycast(new Ray(fromPosition - dir * 0.5f, dir), out RaycastHit hit, travel.magnitude + 1.5f))
        {
            point = hit.point;
            normal = hit.normal;
        }
        else
        {
            // Grazing hit (e.g. clipped an edge): trace toward the nearest point on the surface instead
            point = target.ClosestPoint(transform.position);
            Vector3 toSurface = point - transform.position;
            if (toSurface.sqrMagnitude > 0.0001f &&
                target.Raycast(new Ray(transform.position - toSurface.normalized * 0.5f, toSurface.normalized), out hit, toSurface.magnitude + 1f))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else
            {
                normal = Vector3.up;
            }
        }

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Instantiate(puddlePrefab, point + normal * 0.02f, rot);
    }
}
