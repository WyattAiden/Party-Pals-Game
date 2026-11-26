using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BombSpawn : MonoBehaviour
{
    [SerializeField] private GameObject bomb;
    [SerializeField] private Transform[] spawnPointsB;

    [SerializeField] private float startDelay = 5f;
    [SerializeField] private float bombInterval = 1f;
    [SerializeField] private float restInterval = 5f;

    void Start()
    {
        StartCoroutine(BombingRun());
    }

    private IEnumerator BombingRun()
    {
        // Wait before the first bombing run
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // Go through each spawn point in order
            for (int i = 0; i < spawnPointsB.Length; i++)
            {
                Instantiate(
                    bomb,
                    spawnPointsB[i].position,
                    spawnPointsB[i].rotation
                );

                // Time between each bomb
                yield return new WaitForSeconds(bombInterval);
            }

            // Wait before the next bombing run
            yield return new WaitForSeconds(restInterval);
        }
    }
}
