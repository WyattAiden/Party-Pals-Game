using UnityEngine;

// Put on a projectile (e.g. a needle). Hitting a player sticks a visual into them and adds a bleed stack.
public class BleedOnHit : MonoBehaviour, IProjectileHitEffect
{
    [SerializeField] private float bleedDamagePerSecond = 2.5f;
    [SerializeField] private float bleedDuration = 4f;
    [Tooltip("Visual-only object left stuck in the player (no colliders/scripts). Removed when the bleed ends.")]
    [SerializeField] private GameObject stuckVisualPrefab;
    [Tooltip("How far the stuck visual sinks in toward the player's centre.")]
    [SerializeField] private float embedDepth = 0.15f;

    public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition)
    {
        var status = target.GetComponentInParent<PlayerStatusEffects>();
        if (status == null) return;

        GameObject stuck = null;
        if (stuckVisualPrefab != null)
        {
            // Stick it on the surface of the player where the needle came in, pointing the way it was flying
            Vector3 hitPoint = target.ClosestPoint(fromPosition);
            Vector3 toCenter = target.bounds.center - hitPoint;
            toCenter.y = 0f;
            Vector3 pos = hitPoint + (toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized * embedDepth : Vector3.zero);
            stuck = Instantiate(stuckVisualPrefab, pos, transform.rotation, status.transform);
        }

        status.ApplyBleed(bleedDamagePerSecond, bleedDuration, owner, stuck);
    }
}
