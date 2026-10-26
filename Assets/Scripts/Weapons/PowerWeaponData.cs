using UnityEngine;

// One asset per power weapon: right-click in the Project window > Create > Party Pals > Power Weapon
[CreateAssetMenu(menuName = "Party Pals/Power Weapon", fileName = "NewPowerWeapon")]
public class PowerWeaponData : ScriptableObject
{
    public string displayName = "Power Weapon";

    [Header("Visuals")]
    [Tooltip("Optional model shown on the player while they hold this weapon.")]
    public GameObject heldModelPrefab;

    [Header("Firing")]
    [Tooltip("Projectile to fire. Make a variant of Projectile.prefab with more damage/knockback/speed.")]
    public Projectile projectilePrefab;
    [Min(1)] public int startingAmmo = 5;
    [Min(0f)] public float fireCooldown = 0.3f;
    [Tooltip("Projectiles per shot. Use more than 1 for a shotgun-style spread.")]
    [Min(1)] public int projectilesPerShot = 1;
    [Tooltip("Total spread angle in degrees when firing more than one projectile.")]
    [Range(0f, 90f)] public float spreadAngle = 20f;

    [Header("Limits")]
    [Tooltip("Seconds before the weapon is taken away even with ammo left. 0 = no time limit.")]
    [Min(0f)] public float maxHoldTime = 0f;
}
