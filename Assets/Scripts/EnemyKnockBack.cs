using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyKnockBack : MonoBehaviour
{
    public float knockbackDuration = 0.25f;

    Vector3 velocity;
    float timer;

    public bool IsKnockedBack => timer > 0f;

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.y = 0f;                       // keep it on the ground
        velocity = direction.normalized * force;
        timer = knockbackDuration;
    }

    // Update is called once per frame
    void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        // Fade the push out so it feels smooth
        float t = timer / knockbackDuration;
        transform.position += velocity * t * Time.deltaTime;
    }
}

