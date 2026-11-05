using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the object straight in one direction and destroys it
/// once it has travelled a set distance from where it started.
/// Any player the car touches takes damage and gets knocked away.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CarMover : MonoBehaviour
{
    [Tooltip("How fast the car drives (units per second).")]
    [SerializeField] private float speed = 10f;

    [Tooltip("The car is destroyed after travelling this far from its starting point.")]
    [SerializeField] private float destroyDistance = 40f;

    [Tooltip("Direction to drive, relative to the car itself. (0,0,1) = the car's blue Z arrow, (0,0,-1) = the opposite way.")]
    [SerializeField] private Vector3 localDirection = Vector3.back;

    [Header("Hitting Players")]
    [SerializeField] private float damage = 10f;
    [Tooltip("How hard the player is launched when hit.")]
    [SerializeField] private float knockbackForce = 25f;
    [Tooltip("0 = pushed straight ahead of the car, 1 = pushed equally ahead and out to the side.")]
    [Range(0f, 2f)]
    [SerializeField] private float sidewaysPush = 1.25f;
    [Tooltip("Small upward pop on hit. 0 = stay on the ground.")]
    [SerializeField] private float upwardForce = 0f;
    [Tooltip("On: each car can only hit a given player once. Off: it can hit them again after Rehit Cooldown.")]
    [SerializeField] private bool hitEachPlayerOnce = true;
    [Tooltip("Seconds before this car can hit the same player again (only used when Hit Each Player Once is off).")]
    [SerializeField] private float rehitCooldown = 0.75f;

    [Header("Hitbox")]
    [Tooltip("When on, the trigger box is resized at Start to cover the whole car (all child meshes).")]
    [SerializeField] private bool autoFitHitbox = true;
    [Tooltip("Extra size added around the car so players get hit before the solid body blocks them.")]
    [SerializeField] private float hitboxPadding = 0.3f;

    private Vector3 startPosition;
    private BoxCollider hitbox;
    private readonly Dictionary<GameObject, float> lastHitTime = new Dictionary<GameObject, float>();

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider>();
        hitbox.isTrigger = true;

        // A kinematic rigidbody makes trigger events reliable on an object moved by its transform
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (autoFitHitbox) FitHitboxToCar();
    }

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.Translate(localDirection.normalized * speed * Time.deltaTime, Space.Self);

        if (Vector3.Distance(startPosition, transform.position) >= destroyDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);

    // Catches players who were already overlapping when the cooldown ran out
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return;

        // The player has a CapsuleCollider AND a CharacterController, so one touch
        // can send two trigger events. The cooldown makes it count as one hit.
        GameObject target = health.gameObject;
        if (lastHitTime.TryGetValue(target, out float last) && (hitEachPlayerOnce || Time.time - last < rehitCooldown)) return;
        lastHitTime[target] = Time.time;

        health.TakeDamage(damage, gameObject);

        Vector3 push = GetPushDirection(target.transform.position) * knockbackForce;
        push.y = upwardForce;

        if (target.TryGetComponent(out PlayerController player))
            player.ApplyKnockBack(push);
        else if (target.TryGetComponent(out EnemyKnockBack enemy))
            enemy.ApplyKnockback(push, knockbackForce);
    }

    // Pushes the target the way the car is driving, plus out to whichever side of the car they're on
    private Vector3 GetPushDirection(Vector3 targetPosition)
    {
        Vector3 driveDir = transform.TransformDirection(localDirection.normalized);
        driveDir.y = 0f;
        driveDir.Normalize();

        Vector3 carCenter = hitbox.bounds.center;
        Vector3 toTarget = targetPosition - carCenter;
        toTarget.y = 0f;

        // Keep only the sideways part (perpendicular to the driving direction)
        Vector3 side = toTarget - Vector3.Dot(toTarget, driveDir) * driveDir;
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(Vector3.up, driveDir) * (Random.value < 0.5f ? 1f : -1f);

        return (driveDir + side.normalized * sidewaysPush).normalized;
    }

    // Resizes the trigger so it covers every mesh on the car, in the car's own local space
    private void FitHitboxToCar()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        bool first = true;
        Bounds local = new Bounds();

        foreach (Renderer r in renderers)
        {
            Bounds wb = r.bounds;
            Vector3 min = wb.min, max = wb.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 p = toLocal.MultiplyPoint3x4(corner);
                if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                else local.Encapsulate(p);
            }
        }

        // Undo the root's scale so the box size matches what we measured
        Vector3 s = transform.lossyScale;
        Vector3 scaleFix = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                                       1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                                       1f / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        Vector3 pad = Vector3.Scale(Vector3.one * hitboxPadding * 2f, scaleFix);

        hitbox.center = local.center;
        hitbox.size = local.size + pad;
    }

    // Shows where the car will be destroyed when it's selected in the Scene view
    private void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? startPosition : transform.position;
        Vector3 to = from + transform.TransformDirection(localDirection.normalized) * destroyDistance;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.5f);
    }
}
