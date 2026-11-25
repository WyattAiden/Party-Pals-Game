using UnityEngine;
using UnityEngine.InputSystem;

// Push back: shoves any other player standing in front of you.
// Hook the "PushBack" action (Q / Left Bumper) on PlayerInput to PushBack().
[RequireComponent(typeof(PlayerController))]
public class PlayerPushBack : MonoBehaviour
{
    [Header("Push Settings")]
    [SerializeField] private float pushRange = 2.5f;      // how far in front you can reach
    [SerializeField] private float pushAngle = 90f;       // width of the push cone (degrees)
    [SerializeField] private float pushForce = 25f;       // strength of the shove
    [SerializeField] private float pushCooldown = 0.75f;  // seconds between pushes
    [SerializeField] private LayerMask playerLayers = ~0; // everything by default

    private float cooldownTimer;
    private readonly Collider[] hits = new Collider[16];

    // Raised when a push happens (for animation / sound later)
    public event System.Action Pushed;

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    public void PushBack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (cooldownTimer > 0f) return;

        cooldownTimer = pushCooldown;
        DoPush();
        Pushed?.Invoke();
    }

    void DoPush()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, pushRange, hits, playerLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            PlayerController other = hits[i].GetComponentInParent<PlayerController>();
            if (other == null || other.gameObject == gameObject) continue;

            Vector3 toOther = other.transform.position - transform.position;
            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.0001f) toOther = transform.forward;

            // Only push players that are in front of us
            if (Vector3.Angle(transform.forward, toOther) > pushAngle * 0.5f) continue;

            other.ApplyKnockBack(toOther.normalized * pushForce);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pushRange);
        Vector3 left = Quaternion.Euler(0f, -pushAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, pushAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * pushRange);
        Gizmos.DrawLine(transform.position, transform.position + right * pushRange);
    }
}
