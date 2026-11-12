using System;
using UnityEngine;

// The list of weapon pickups a WeaponSpawnPoint can choose from.
// Create: right-click in the Project window > Create > Party Pals > Weapon Spawn Pool
[CreateAssetMenu(menuName = "Party Pals/Weapon Spawn Pool", fileName = "WeaponSpawnPool")]
public class WeaponSpawnPool : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public WeaponPickup pickupPrefab;
        [Tooltip("Higher = shows up more often. 0 = never.")]
        [Min(0f)] public float weight = 1f;
    }

    public Entry[] entries;

    // Weighted random pick. Tries not to repeat 'avoid' when there's another choice.
    public WeaponPickup PickRandom(WeaponPickup avoid = null)
    {
        if (entries == null || entries.Length == 0) return null;

        float total = 0f;
        int usable = 0;
        foreach (var e in entries)
            if (e.pickupPrefab != null && e.weight > 0f && e.pickupPrefab != avoid) { total += e.weight; usable++; }
        if (usable == 0) avoid = null; // only one option - allow the repeat

        if (avoid == null)
        {
            total = 0f;
            foreach (var e in entries) if (e.pickupPrefab != null && e.weight > 0f) total += e.weight;
        }
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, total);
        foreach (var e in entries)
        {
            if (e.pickupPrefab == null || e.weight <= 0f || e.pickupPrefab == avoid) continue;
            roll -= e.weight;
            if (roll <= 0f) return e.pickupPrefab;
        }
        return null;
    }
}
