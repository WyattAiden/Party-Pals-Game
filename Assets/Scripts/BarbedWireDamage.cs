using UnityEngine;

public class BarbedWireDamage : MonoBehaviour
{
    [SerializeField] private float damage = 10f;

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Health health))
            health.TakeDamage(damage);
    }
}
