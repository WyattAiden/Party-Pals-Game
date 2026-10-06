using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackForce = 30f;

    [HideInInspector] public GameObject owner;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;

        if (other.TryGetComponent(out Health health))
            health.TakeDamage(damage);

        if (other.TryGetComponent(out EnemyKnockBack enemy))
            enemy.ApplyKnockback(transform.forward, knockbackForce);

        Destroy(gameObject);
    }
}