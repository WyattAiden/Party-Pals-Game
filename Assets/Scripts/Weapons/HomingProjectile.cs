using UnityEngine;

// Put on a projectile (next to Projectile) to make it steer toward an enemy player, e.g. a homing rocket.
// It only turns left/right (stays at the same height) and has a limited turn rate, so it can be dodged.
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
    [Tooltip("Optional visual shown under the locked-on player (no colliders). Removed when the rocket is gone.")]
    [SerializeField] private GameObject lockOnIndicatorPrefab;

    private Projectile projectile;
    private Transform target;
    private Health targetHealth;
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

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * Time.deltaTime);
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
