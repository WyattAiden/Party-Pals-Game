using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackForce = 30f;

    [Header("Arc (leave at 0 for a straight shot)")]
    [SerializeField] private float gravity = 0f;
    [SerializeField] private float launchUpSpeed = 0f;
    private float verticalSpeed;

    [Header("Piercing (e.g. a slash that cuts across the screen)")]
    [Tooltip("Keep going after hitting a player, hitting each player only once.")]
    [SerializeField] private bool piercePlayers = false;
    [Tooltip("Ignore walls, floors and other scenery (anything without Health).")]
    [SerializeField] private bool passThroughScenery = false;
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();
    private Collider ownCollider;
    private bool spent; // already hit something and is being destroyed

    [HideInInspector] public GameObject owner;

    // Where the projectile was last frame (used to find the exact surface it hit)
    public Vector3 LastPosition { get; private set; }

    void Start()
    {
        Destroy(gameObject, lifetime);
        verticalSpeed = launchUpSpeed;
        LastPosition = transform.position;
    }

    void Awake()
    {
        foreach (var c in GetComponents<Collider>())
            if (c.enabled) { ownCollider = c; break; }
    }

    void Update()
    {
        Vector3 start = transform.position;
        LastPosition = start;
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        if (gravity != 0f || verticalSpeed != 0f)
        {
            verticalSpeed -= gravity * Time.deltaTime;
            transform.position += Vector3.up * verticalSpeed * Time.deltaTime;
        }

        SweepForHits(start, transform.position);
    }

    // Fast shots (or low frame rates) can jump past a player in a single frame.
    // Check the whole path travelled this frame so nothing gets tunnelled through.
    private void SweepForHits(Vector3 start, Vector3 end)
    {
        if (spent || ownCollider == null) return;
        Vector3 delta = end - start;
        float dist = delta.magnitude;
        if (dist < 0.0001f) return;
        Vector3 dir = delta / dist;
        Vector3 scale = transform.lossyScale;

        RaycastHit[] hits;
        if (ownCollider is BoxCollider box)
        {
            Vector3 half = Vector3.Scale(box.size * 0.5f, scale);
            Vector3 center = start + transform.rotation * Vector3.Scale(box.center, scale);
            hits = Physics.BoxCastAll(center, half, dir, transform.rotation, dist, ~0, QueryTriggerInteraction.Ignore);
        }
        else if (ownCollider is SphereCollider sphere)
        {
            float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = start + transform.rotation * Vector3.Scale(sphere.center, scale);
            hits = Physics.SphereCastAll(center, r, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        }
        else
        {
            hits = Physics.SphereCastAll(start, ownCollider.bounds.extents.magnitude * 0.5f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider == ownCollider) continue;
            OnTriggerEnter(hit.collider);
            if (spent) break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (spent) return;
        if (other.gameObject == owner) return;
        if (other.GetComponent<WeaponPickup>() != null) return; // fly through pickups
        if (other.GetComponent<Projectile>() != null) return;   // shots don't destroy each other (spread weapons)
        if (other.GetComponent<GluePuddle>() != null) return;   // fly over glue puddles

        Health target = other.GetComponentInParent<Health>();
        if (passThroughScenery && target == null) return;
        if (piercePlayers && target != null && !alreadyHit.Add(target)) return; // already cut this player

        if (other.TryGetComponent(out Health health))
            health.TakeDamage(damage, owner);

        if (other.TryGetComponent(out EnemyKnockBack enemy))
            enemy.ApplyKnockback(transform.forward, knockbackForce);

        // Special effects on this projectile (glue, etc.) - see IProjectileHitEffect
        foreach (var effect in GetComponents<IProjectileHitEffect>())
            effect.OnProjectileHit(other, owner, LastPosition);

        if (piercePlayers && target != null) return; // keep flying
        spent = true;
        Destroy(gameObject);
    }
}