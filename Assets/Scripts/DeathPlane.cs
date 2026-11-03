using UnityEngine;

/// Instantly kills anything with a Health component that touches this object.
/// Works whether the object's collider is a trigger or a solid collider:
/// solid colliders are made pass-through and a trigger volume is generated,
/// so CharacterController players fall into it instead of standing on it.
public class DeathPlane : MonoBehaviour
{
    [Tooltip("How far (world units) the kill volume extends above the plane's surface.")]
    [SerializeField] private float triggerHeight = 1f;
    [Tooltip("How far (world units) the kill volume extends below the plane's surface.")]
    [SerializeField] private float triggerDepth = 20f;

    private void Awake()
    {
        bool hasTrigger = false;
        foreach (var col in GetComponents<Collider>())
        {
            if (col.isTrigger) { hasTrigger = true; continue; }
            // Solid colliders would stop players on top of the plane, so turn them off
            // (a non-convex MeshCollider can't be a trigger, so we can't just flip isTrigger).
            col.enabled = false;
        }

        if (!hasTrigger) CreateTriggerVolume();
    }

    private void CreateTriggerVolume()
    {
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        // Size the box to the plane's mesh (local space), then give it real thickness.
        var mf = GetComponent<MeshFilter>();
        Bounds local = (mf != null && mf.sharedMesh != null)
            ? mf.sharedMesh.bounds
            : new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

        float scaleY = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float up = triggerHeight / scaleY;
        float down = triggerDepth / scaleY;

        box.size = new Vector3(local.size.x, local.size.y + up + down, local.size.z);
        box.center = new Vector3(local.center.x, local.center.y + (up - down) * 0.5f, local.center.z);
    }

    private void OnTriggerEnter(Collider other) => Kill(other);
    private void OnTriggerStay(Collider other) => Kill(other); // catches anything already inside
    private void OnCollisionEnter(Collision collision) => Kill(collision.collider);

    private void Kill(Collider other)
    {
        // Health usually lives on the player root, not necessarily on the collider's object
        var health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return;

        health.TakeDamage(health.Current, gameObject);
    }
}
