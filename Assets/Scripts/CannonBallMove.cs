using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonBallMove : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float speed = 15f;

    [SerializeField] private float knockbackForce = 30f;

    private Vector3 direction;

    public void SetDirection(Vector3 newDirection, float newSpeed)
    {
        direction = newDirection.normalized;
        speed = newSpeed;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Deal damage to anything with a Health component
        if (other.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage);

            if (other.TryGetComponent(out EnemyKnockBack enemy))
                enemy.ApplyKnockback(transform.forward, knockbackForce);
        }

        // Destroy the projectile after hitting something
        Destroy(gameObject);
    }
}
