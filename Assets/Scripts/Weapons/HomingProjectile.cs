using UnityEngine;

// Put on a projectile (next to Projectile) to make it steer toward an enemy player, e.g. a homing rocket.
// It can steer up/down as well as left/right (aiming at the target's body), with a limited turn rate so it can be dodged.
[RequireComponent(typeof(Projectile))]
public class HomingProjectile : MonoBehaviour
{
    [Tooltip("Degrees per second the rocket can turn. Lower = easier to dodge.")]
    [SerializeField] private float turnRate = 120f;
    [Tooltip("Flies straight for this long before it starts steering.")]
    [SerializeField] private float armDelay = 0.15f;
    [Tooltip("Players further than this are ignored.")]
    [SerializeField] private float lockRange = 30f;
    [Tooltip("At launch, prefer targets within this angle of where the shooter aimed.")]
    [SerializeField] private float lockConeAngle = 70f;
    [Tooltip("Also steer up and down (e.g. to reach players on higher or lower ground). Off = stays at launch height.")]
    [SerializeField] private bool steerVertically = true;
    [Tooltip("Steepest climb/dive angle, so it doesn't loop straight up or down.")]
    [Range(0f, 89f)] [SerializeField] private float maxPitch = 60f;
    [Tooltip("If scenery blocks the straight line to the target, climb at least this steeply (degrees) until the path is clear. 0 = off.")]
    [Range(0f, 89f)] [SerializeField] private float climbAngle = 35f;
    [Tooltip("Optional visual shown under the locked-on player (no colliders). Removed when the rocket is gone.")]
    [SerializeField] private GameObject lockOnIndicatorPrefab;

    private Projectile projectile;
    private Transform target;
    private Health targetHealth;
    private Collider targetCollider;
    private GameObject indicator;
    private float spawnTime;

    public Transform Target => target;

    void Awake() => projectile = GetComponent<Projectile>();

    void Start()
    {
        spawnTime = Time.time;
        AcquireTarget(true);
    }

    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy || (targetHealth != null && targetHealth.IsDead))
            AcquireTarget(false);
        if (target == null || Time.time - spawnTime < armDelay) return;

        // Aim at the middle of their body, not their feet
        Vector3 aim = targetCollider != null ? targetCollider.bounds.center : target.position;
        Vector3 to = aim - transform.position;
        if (!steerVertically) to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        // Limit how steep it can climb/dive
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
        float pitch = Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
        if (steerVertically && climbAngle > 0f && PathBlocked(aim)) pitch = Mathf.Max(pitch, climbAngle); // fly up over cover
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * Time.deltaTime);
    }

    // Is there scenery (not players) between the rocket and where it's aiming?
    private bool PathBlocked(Vector3 aim)
    {
        Vector3 to = aim - transform.position;
        float dist = to.magnitude;
        if (dist < 0.5f) return false;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.3f, to / dist, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Health>() != null) continue;     // players
            if (hit.collider.GetComponentInParent<Projectile>() != null) continue; // itself / other shots
            if (hit.distance <= 0f) continue;                                      // already touching (e.g. floor)
            return true;
        }
        return false;
    }

    private void AcquireTarget(bool useCone)
    {
        Transform best = null;
        Health bestHealth = null;
        float bestScore = float.MaxValue;
        Vector3 fwd = transform.forward; fwd.y = 0f;

        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.gameObject == projectile.owner) continue;
            var health = player.GetComponent<Health>();
            if (health != null && health.IsDead) continue;

            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist > lockRange) continue;
            float angle = Vector3.Angle(fwd, to);
            if (useCone && angle > lockConeAngle) continue;

            float score = dist + angle * 0.05f; // closest, slightly favouring what you aimed at
            if (score < bestScore) { bestScore = score; best = player.transform; bestHealth = health; }
        }

        if (best == null && useCone) { AcquireTarget(false); return; } // nobody in front: take anyone in range
        SetTarget(best, bestHealth);
    }

    private void SetTarget(Transform newTarget, Health health)
    {
        if (newTarget == target) return;
        target = newTarget;
        targetHealth = health;
        targetCollider = target != null ? target.GetComponent<CharacterController>() : null;
        if (indicator != null) Destroy(indicator);
        if (target != null && lockOnIndicatorPrefab != null)
        {
            var cc = target.GetComponent<CharacterController>();
            float footY = cc != null ? cc.bounds.min.y : target.position.y;
            indicator = Instantiate(lockOnIndicatorPrefab, new Vector3(target.position.x, footY + 0.03f, target.position.z), Quaternion.identity, target);
        }
    }

    void OnDestroy()
    {
        if (indicator != null) Destroy(indicator);
    }
}
