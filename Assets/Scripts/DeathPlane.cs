using UnityEngine;

public class DeathPlane : MonoBehaviour
{
    [SerializeField] private float damage = 10f;

    private void OnTriggerEnter(Collider other)
    {
        // Deal damage to anything with a Health component
        if (other.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage);
        }
    }
}
