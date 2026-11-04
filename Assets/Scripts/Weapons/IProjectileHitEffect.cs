using UnityEngine;

// Add special on-hit behaviour to a projectile without touching Projectile.cs:
// make a MonoBehaviour that implements this, and put it on the projectile prefab.
// Projectile calls OnProjectileHit on every one of these it has (so effects can be combined).
//
// Example:
//   public class FreezeOnHit : MonoBehaviour, IProjectileHitEffect
//   {
//       public void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition) { ... }
//   }
public interface IProjectileHitEffect
{
    // target:       what the projectile hit (a player, the ground, a wall...)
    // owner:        the player who fired it (may be null)
    // fromPosition: where the projectile was the frame before the hit - use it to trace the exact surface point
    void OnProjectileHit(Collider target, GameObject owner, Vector3 fromPosition);
}
