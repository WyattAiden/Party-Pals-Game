using UnityEngine;
using UnityEngine.InputSystem;

// A placeable jack-in-the-box mine. After arming, it pops when another player gets close
// OR when its timer runs out - whichever comes first. Popping is an explosion (damage + knockback)
// that hits everyone nearby, including the player who placed it. Nearby mines get set off too.
public class JackInTheBoxMine : MonoBehaviour, IPlacedByPlayer
{
    [Header("Triggering")]
    [Tooltip("Seconds after being placed before it can be set off (time to walk away).")]
    [SerializeField] private float armDelay = 1f;
    [SerializeField] private float triggerRadius = 2.5f;
    [Tooltip("Pops on its own this many seconds after arming. 0 = only pops when someone gets close.")]
    [SerializeField] private float popTimer = 8f;
    [Tooltip("Warning time between being set off and popping (crank spins fast, box shakes).")]
    [SerializeField] private float windUpTime = 0.6f;
    [SerializeField] private bool ownerCanTrigger = false;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float maxDamage = 35f;
    [SerializeField] private float minDamage = 10f;
    [SerializeField] private float knockbackForce = 22f;
    [SerializeField] private float physicsForce = 800f;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeStrength = 0.8f;
    [Tooltip("Visual only. Don't use a prefab that also deals damage (e.g. ExplosionParticle1) or players get hit twice.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float effectLifetime = 3f;

    [Header("Parts (for animation)")]
    [Tooltip("Shaken during the wind-up.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Hinge of the lid - rotates open when it pops.")]
    [SerializeField] private Transform lidPivot;
    [Tooltip("Spins around its local X axis while ticking.")]
    [SerializeField] private Transform crank;
    [Tooltip("The jack (spring + head). Hidden until it pops.")]
    [SerializeField] private Transform jack;
    [Tooltip("Tinted with the owner's player colour so everyone can tell whose mine it is.")]
    [SerializeField] private Renderer[] ownerTintRenderers;
    [SerializeField] private float crankSpeed = 180f;

    private GameObject owner;
    private float placedTime;
    private bool windingUp;
    private float windUpStart;
    private float currentWindUp;
    private bool popped;
    private float popTime;
    private Vector3 jackFullScale = Vector3.one;
    private Vector3 visualBasePos;

    void Awake()
    {
        placedTime = Time.time;
        if (jack != null)
        {
            jackFullScale = jack.localScale;
            jack.localScale = Vector3.zero;
            jack.gameObject.SetActive(false);
        }
        if (visualRoot != null) visualBasePos = visualRoot.localPosition;
    }

    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
        if (owner == null || ownerTintRenderers == null) return;
        var input = owner.GetComponent<PlayerInput>();
        if (input == null || input.playerIndex < 0 || input.playerIndex >= GameSettings.playerColors.Length) return;
        Color c = GameSettings.playerColors[input.playerIndex];
        foreach (var r in ownerTintRenderers)
            if (r != null) r.material.color = c;
    }

    void Update()
    {
        if (popped) { AnimatePop(); return; }

        float age = Time.time - placedTime;
        bool armed = age >= armDelay;

        if (crank != null)
        {
            float speed = windingUp ? crankSpeed * 5f : armed ? crankSpeed : crankSpeed * 0.25f;
            crank.Rotate(speed * Time.deltaTime, 0f, 0f, Space.Self);
        }

        if (windingUp)
        {
            if (visualRoot != null)
            {
                float t = (Time.time - windUpStart) / Mathf.Max(currentWindUp, 0.01f);
                visualRoot.localPosition = visualBasePos + Random.insideUnitSphere * 0.04f * (0.5f + t);
            }
            if (Time.time - windUpStart >= currentWindUp) Pop();
            return;
        }

        if (!armed) return;

        if (popTimer > 0f && age - armDelay >= popTimer) { StartWindUp(windUpTime); return; }

        foreach (Collider col in Physics.OverlapSphere(transform.position, triggerRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var player = col.GetComponentInParent<PlayerController>();
            if (player == null) continue;
            if (player.gameObject == owner && !ownerCanTrigger) continue;
            var health = player.GetComponent<Health>();
            if (health != null && health.IsDead) continue;
            StartWindUp(windUpTime);
            return;
        }
    }

    // Also used for chain reactions from other mines
    public void StartWindUp(float duration)
    {
        if (windingUp || popped) return;
        windingUp = true;
        windUpStart = Time.time;
        currentWindUp = duration;
    }

    private void Pop()
    {
        popped = true;
        popTime = Time.time;
        if (visualRoot != null) visualRoot.localPosition = visualBasePos;
        if (jack != null) jack.gameObject.SetActive(true);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        Vector3 center = transform.position + transform.up * 0.4f;
        if (explosionEffectPrefab != null)
            Destroy(Instantiate(explosionEffectPrefab, center, Quaternion.identity), effectLifetime);

        AreaExplosion.Detonate(center, explosionRadius, maxDamage, minDamage, knockbackForce, physicsForce, owner);
        MultiplayerCamera.Shake(shakeDuration, shakeStrength);

        // Chain reaction: set off other mines caught in the blast
        foreach (Collider col in Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var other = col.GetComponentInParent<JackInTheBoxMine>();
            if (other != null && other != this) other.StartWindUp(0.15f);
        }

        Destroy(gameObject, 0.9f); // leave the popped jack visible for a moment
    }

    private void AnimatePop()
    {
        float t = Time.time - popTime;
        if (lidPivot != null)
            lidPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -120f, Mathf.Clamp01(t / 0.12f)), 0f, 0f);
        if (jack != null)
        {
            // springy overshoot: shoots past full size then settles, wobbling
            float s = 1f - Mathf.Exp(-12f * t) * Mathf.Cos(18f * t);
            jack.localScale = jackFullScale * Mathf.Max(0f, s);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
