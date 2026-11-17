using UnityEngine;

// Put this on an object with a collider set to "Is Trigger".
// Any player that touches it dies instantly.
public class DeathPlane : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            health.TakeDamage(health.Current);
        }
    }
}
