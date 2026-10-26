using UnityEngine;

// Put on the Projectile prefab (or a power-weapon projectile variant) to give it a BulletTrail.
public class ProjectileTrail : MonoBehaviour
{
    [SerializeField] private BulletTrail trailPrefab;

    void Start()
    {
        if (trailPrefab != null) BulletTrail.Spawn(trailPrefab, transform);
    }
}
