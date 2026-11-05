using System.Collections;
using UnityEngine;

// Spawns a random power weapon pickup here. When someone grabs it, a new random one appears after a delay.
public class WeaponSpawnPoint : MonoBehaviour
{
    [SerializeField] private WeaponSpawnPool pool;
    [Tooltip("Random delay before the first weapon appears at the start of a round (min, max seconds).")]
    [SerializeField] private Vector2 firstSpawnDelay = new Vector2(3f, 8f);
    [Tooltip("Random delay before a new weapon appears after one is picked up (min, max seconds).")]
    [SerializeField] private Vector2 respawnDelay = new Vector2(10f, 16f);
    [Tooltip("Try not to spawn the same weapon twice in a row here.")]
    [SerializeField] private bool avoidRepeats = true;
    [Tooltip("How high above this point the pickup floats.")]
    [SerializeField] private float pickupHeight = 1f;

    private WeaponPickup current;
    private WeaponPickup lastPrefab;

    void Start()
    {
        StartCoroutine(SpawnAfter(Random.Range(firstSpawnDelay.x, firstSpawnDelay.y)));
    }

    private IEnumerator SpawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Spawn();
    }

    private void Spawn()
    {
        if (pool == null || current != null) return;
        WeaponPickup prefab = pool.PickRandom(avoidRepeats ? lastPrefab : null);
        if (prefab == null) return;

        lastPrefab = prefab;
        current = Instantiate(prefab, transform.position + Vector3.up * pickupHeight, Quaternion.identity);
        current.MakeOneTime();
        current.PickedUp += OnPickedUp;
    }

    private void OnPickedUp(WeaponPickup pickup)
    {
        pickup.PickedUp -= OnPickedUp;
        current = null;
        StartCoroutine(SpawnAfter(Random.Range(respawnDelay.x, respawnDelay.y)));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * pickupHeight, Vector3.one * 0.8f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * pickupHeight);
    }
}
