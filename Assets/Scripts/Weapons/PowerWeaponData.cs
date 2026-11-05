using UnityEngine;

// One asset per power weapon: right-click in the Project window > Create > Party Pals > Power Weapon
[CreateAssetMenu(menuName = "Party Pals/Power Weapon", fileName = "NewPowerWeapon")]
public class PowerWeaponData : ScriptableObject
{
    public string displayName = "Power Weapon";

    [Header("Random roll")]
    [Tooltip("If any are set, picking this up gives the player ONE of these at random instead (e.g. Rock / Paper / Scissors). Everything below is then ignored.")]
    public PowerWeaponData[] randomVariants;

    [Header("Visuals")]
    [Tooltip("Optional model shown on the player while they hold this weapon.")]
    public GameObject heldModelPrefab;
    [Tooltip("Where the held model sits relative to the player's fire point.")]
    public Vector3 heldModelOffset = Vector3.zero;

    [Header("Firing")]
    [Tooltip("Projectile to fire. Make a variant of Projectile.prefab with more damage/knockback/speed.")]
    public Projectile projectilePrefab;
    [Min(1)] public int startingAmmo = 5;
    [Min(0f)] public float fireCooldown = 0.3f;
    [Tooltip("Projectiles per shot. Use more than 1 for a shotgun-style spread.")]
    [Min(1)] public int projectilesPerShot = 1;
    [Tooltip("Total spread angle in degrees when firing more than one projectile.")]
    [Range(0f, 90f)] public float spreadAngle = 20f;

    [Header("Placeable (instead of shooting)")]
    [Tooltip("If set, pressing shoot places this in front of the player instead of firing a projectile (mines, traps...).")]
    public GameObject placeablePrefab;
    [Tooltip("How far in front of the player it gets placed.")]
    public float placeDistance = 0.9f;

    [Header("Attached (instead of shooting)")]
    [Tooltip("If set, pressing shoot spawns this on the player's fire point and it follows their aim (e.g. a beam).")]
    public GameObject attachedPrefab;

    [Header("Limits")]
    [Tooltip("Seconds before the weapon is taken away even with ammo left. 0 = no time limit.")]
    [Min(0f)] public float maxHoldTime = 0f;
}
