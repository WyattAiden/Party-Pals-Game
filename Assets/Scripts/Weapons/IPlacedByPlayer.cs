using UnityEngine;

// Implement on anything a power weapon places in the world (mines, traps...).
// PlayerWeaponHandler calls SetOwner right after placing it.
public interface IPlacedByPlayer
{
    void SetOwner(GameObject owner);
}
