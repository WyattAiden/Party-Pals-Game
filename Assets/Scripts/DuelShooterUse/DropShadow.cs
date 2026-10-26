using UnityEngine;

// Adapted from DuelShooter's Shadow. Makes a fake "blob" shadow on the ground under
// something that flies or falls (bombs, cannonballs, jumping players).
// Setup: add a Quad (or flat sprite) with a dark, soft, transparent material as a CHILD
// of the object, then put this script on that quad. It detaches itself on Start and
// shrinks the higher its owner is above the ground.
public class DropShadow : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Above this height the shadow disappears.")]
    [SerializeField] private float maxHeight = 20f;
    [Tooltip("Size of the shadow (relative to its normal size) at Max Height.")]
    [SerializeField] private float minScale = 0.3f;
    [SerializeField] private float groundOffset = 0.05f;
    [Tooltip("Tilt the shadow to match sloped ground. Assumes a Unity Quad.")]
    [SerializeField] private bool alignToGround = true;

    private Transform owner;
    private Vector3 baseScale;
    private Renderer[] renderers;

    void Start()
    {
        owner = transform.parent;
        transform.SetParent(null, true);
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();

        // A Quad comes with a collider - it would block the ground check (and bullets)
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
            Destroy(c);
        }
    }

    void LateUpdate()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (owner.gameObject.activeInHierarchy &&
            Physics.Raycast(owner.position, Vector3.down, out RaycastHit hit, maxHeight, groundMask, QueryTriggerInteraction.Ignore))
        {
            SetVisible(true);
            transform.position = hit.point + hit.normal * groundOffset;
            transform.rotation = alignToGround
                ? Quaternion.LookRotation(-hit.normal, Vector3.forward)
                : Quaternion.Euler(90f, 0f, 0f);
            transform.localScale = baseScale * Mathf.Lerp(1f, minScale, hit.distance / maxHeight);
        }
        else
        {
            SetVisible(false);
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
    }
}
