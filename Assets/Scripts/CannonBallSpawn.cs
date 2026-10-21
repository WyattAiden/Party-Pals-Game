using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CannonBallSpawn : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject cannonball;
    [SerializeField] private float cannonballSpeed = 15f;

    [Header("Random Fire Timing")]
    [SerializeField] private float minFireInterval = 1f;
    [SerializeField] private float maxFireInterval = 4f;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] topSpawnPoints;
    [SerializeField] private Transform[] bottomSpawnPoints;

    private void Start()
    {
        // Give every top spawn point its own firing routine
        foreach (Transform spawnPoint in topSpawnPoints)
        {
            if (spawnPoint != null)
                StartCoroutine(FireFromSpawnPoint(
                    spawnPoint, Vector3.back));
        }

        // Give every bottom spawn point its own firing routine
        foreach (Transform spawnPoint in bottomSpawnPoints)
        {
            if (spawnPoint != null)
                StartCoroutine(FireFromSpawnPoint(
                    spawnPoint, Vector3.forward));
        }
    }

    private IEnumerator FireFromSpawnPoint(
        Transform spawnPoint, Vector3 direction)
    {
        while (true)
        {
            // Each spawn point waits for its own random delay
            float randomDelay = UnityEngine.Random.Range(
                minFireInterval,
                maxFireInterval
            );

            yield return new WaitForSeconds(randomDelay);

            SpawnProjectile(spawnPoint, direction);
        }
    }

    private void SpawnProjectile(
        Transform spawnPoint, Vector3 direction)
    {
        if (cannonball == null || spawnPoint == null)
            return;

        GameObject projectile = Instantiate(
            cannonball,
            spawnPoint.position,
            Quaternion.LookRotation(direction)
        );

        CannonBallMove movement =
            projectile.GetComponent<CannonBallMove>();

        if (movement != null)
        {
            movement.SetDirection(direction, cannonballSpeed);
        }
    }
}
