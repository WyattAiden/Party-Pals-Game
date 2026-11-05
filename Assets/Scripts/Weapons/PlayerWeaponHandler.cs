using System;
using UnityEngine;

// Goes on the player prefab. Holds the current power weapon and its ammo.
// PlayerController.Shoot calls TryFire while the player has a power weapon.
public class PlayerWeaponHandler : MonoBehaviour
{
    [Tooltip("Where the held weapon model is attached. Uses the FirePoint if left empty.")]
    [SerializeField] private Transform weaponSocket;
    [Tooltip("If true, walking over a pickup while already holding a power weapon swaps to the new one.")]
    [SerializeField] private bool allowSwapping = true;

    public PowerWeaponData Current { get; private set; }
    public int Ammo { get; private set; }
    public bool HasPowerWeapon => Current != null;

    // (weapon or null, ammo left) - hook a HUD/ammo counter up to this
    public event Action<PowerWeaponData, int> AmmoChanged;

    private GameObject heldModel;
    private float nextFireTime;
    private float equipTime;

    public bool CanPickUp(PowerWeaponData weapon)
    {
        if (weapon == null) return false;
        if (!HasPowerWeapon) return true;
        if (Current == weapon) return Ammo < weapon.startingAmmo; // same weapon: refill
        return allowSwapping;
    }

    public void Equip(PowerWeaponData weapon, Transform fallbackSocket)
    {
        weapon = ResolveRandom(weapon);
        if (weapon == null) return;
        Unequip();
        Current = weapon;
        Ammo = weapon.startingAmmo;
        equipTime = Time.time;
        nextFireTime = 0f;

        Transform socket = weaponSocket != null ? weaponSocket : fallbackSocket;
        if (weapon.heldModelPrefab != null && socket != null)
        {
            heldModel = Instantiate(weapon.heldModelPrefab, socket);
            heldModel.transform.localPosition = weapon.heldModelOffset;
        }

        AmmoChanged?.Invoke(Current, Ammo);
    }

    // Returns true if the power weapon handled the shot (even if on cooldown),
    // false if the player has no power weapon and should use their normal shot.
    public bool TryFire(Transform firePoint, GameObject owner)
    {
        if (!HasPowerWeapon) return false;
        if (Time.time < nextFireTime) return true;
        nextFireTime = Time.time + Current.fireCooldown;

        if (Current.placeablePrefab != null)
        {
            Place(owner);
        }
        else
        {
            int count = Current.projectilesPerShot;
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1
                    ? Mathf.Lerp(-Current.spreadAngle * 0.5f, Current.spreadAngle * 0.5f, i / (float)(count - 1))
                    : 0f;
                Quaternion rot = firePoint.rotation * Quaternion.Euler(0f, angle, 0f);
                Projectile proj = Instantiate(Current.projectilePrefab, firePoint.position, rot);
                proj.owner = owner;
            }
        }

        Ammo--;
        AmmoChanged?.Invoke(Current, Ammo);
        if (Ammo <= 0) Unequip();
        return true;
    }

    // Weapons with random variants (Rock/Paper/Scissors) hand out one of them
    private static PowerWeaponData ResolveRandom(PowerWeaponData weapon)
    {
        int guard = 0;
        while (weapon != null && weapon.randomVariants != null && weapon.randomVariants.Length > 0 && guard++ < 5)
            weapon = weapon.randomVariants[UnityEngine.Random.Range(0, weapon.randomVariants.Length)];
        return weapon;
    }

    // Drops the weapon's placeable on the ground in front of the player
    private void Place(GameObject owner)
    {
        Vector3 fwd = owner.transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

        Vector3 spot = owner.transform.position + fwd * Current.placeDistance;
        Vector3 pos;
        if (Physics.Raycast(spot + Vector3.up, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        else
        {
            var cc = owner.GetComponent<CharacterController>();
            pos = new Vector3(spot.x, cc != null ? cc.bounds.min.y : owner.transform.position.y, spot.z);
        }

        GameObject placed = Instantiate(Current.placeablePrefab, pos, Quaternion.LookRotation(fwd));
        foreach (var p in placed.GetComponentsInChildren<IPlacedByPlayer>())
            p.SetOwner(owner);
    }

    public void Unequip()
    {
        if (heldModel != null) Destroy(heldModel);
        heldModel = null;
        bool hadWeapon = Current != null;
        Current = null;
        Ammo = 0;
        if (hadWeapon) AmmoChanged?.Invoke(null, 0);
    }

    void Update()
    {
        if (HasPowerWeapon && Current.maxHoldTime > 0f && Time.time - equipTime >= Current.maxHoldTime)
            Unequip();
    }

    void OnDisable() => Unequip(); // e.g. player dies/despawns
}
