using UnityEngine;

// Put on a projectile prefab for a big shove on hit (e.g. the Rock).
// Goes through PlayerController's knockback, so it slides the player but won't push them through walls.
public class KnockbackOnHit : MonoBehaviour, IProjectileHitEffect
{
    [Tooltip("Launch speed. The player slides roughly force / 8 metres.")]
    [SerializeField] private float force = 70f;
    [Tooltip("Small upward-looking bump is ignored; this only pushes along the ground.")]
    [SerializeField] private bool useShotDirection = true;

    public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition)
    {
        var player = target.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Vector3 dir = useShotDirection ? transform.forward : player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        player.ApplyKnockBack(dir.normalized * force);
    }
}
