using System.Collections;
using UnityEngine;

// Adapted from DuelShooter's Mine_A. Place in a level (needs a collider).
// When a living player gets close it flashes, then explodes after a short fuse.
// Add a Health component too if you want players to be able to shoot it / chain-react it.
public class ProximityMine : MonoBehaviour
{
    [SerializeField] private float triggerRadius = 3f;
    [Tooltip("Seconds after spawning before the mine can be set off.")]
    [SerializeField] private float armDelay = 1f;
    [Tooltip("Seconds between being set off and exploding.")]
    [SerializeField] private float fuseTime = 0.4f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float maxDamage = 35f;
    [SerializeField] private float minDamage = 10f;
    [SerializeField] private float knockbackForce = 20f;
    [SerializeField] private float physicsForce = 800f;
    [Tooltip("Visual only - the mine does its own damage. Don't use a prefab that also deals damage (e.g. one with Explosion or AreaExplosion on it) or players get hit twice.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float effectLifetime = 4f;

    [Header("Warning")]
    [Tooltip("Optional effect spawned when the mine is set off (e.g. a click/spark).")]
    [SerializeField] private GameObject triggeredEffectPrefab;
    [Tooltip("Optional renderer that blinks while the fuse burns (e.g. the mine's light).")]
    [SerializeField] private Renderer blinkRenderer;
    [SerializeField] private Color blinkColor = Color.red;
    [SerializeField] private float blinkRate = 12f;

    private bool triggered;
    private bool exploded;
    private float spawnTime;
    private Health health;

    void Awake() => health = GetComponent<Health>();

    void OnEnable()
    {
        if (health != null) health.Died += Explode;
    }

    void OnDisable()
    {
        if (health != null) health.Died -= Explode;
    }

    void Start() => spawnTime = Time.time;

    void Update()
    {
        if (triggered || Time.time - spawnTime < armDelay) return;

        foreach (Collider col in Physics.OverlapSphere(transform.position, triggerRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col.GetComponentInParent<PlayerController>() == null) continue;
            Health playerHealth = col.GetComponentInParent<Health>();
            if (playerHealth != null && playerHealth.IsDead) continue;

            StartCoroutine(Fuse());
            break;
        }
    }

    private IEnumerator Fuse()
    {
        triggered = true;

        if (triggeredEffectPrefab != null)
            Destroy(Instantiate(triggeredEffectPrefab, transform.position, Quaternion.identity), 2f);

        Material mat = blinkRenderer != null ? blinkRenderer.material : null;
        Color startColor = mat != null ? mat.color : Color.white;

        float timer = 0f;
        while (timer < fuseTime)
        {
            if (mat != null)
                mat.color = Mathf.Repeat(timer * blinkRate, 1f) < 0.5f ? blinkColor : startColor;
            timer += Time.deltaTime;
            yield return null;
        }

        Explode();
    }

    public void Explode()
    {
        if (exploded) return;   // guards against chain reactions hitting this mine again
        exploded = true;

        if (explosionEffectPrefab != null)
            Destroy(Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity), effectLifetime);

        AreaExplosion.Detonate(transform.position, explosionRadius, maxDamage, minDamage, knockbackForce, physicsForce);
        MultiplayerCamera.Shake(0.5f, 0.8f);
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
