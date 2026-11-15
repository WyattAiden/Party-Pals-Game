using System.Collections.Generic;
using UnityEngine;

// Moves a minecart in a straight line for a set distance,
// damages and knocks back any player it runs into,
// then destroys itself so carts don't pile up in the scene.
public class CartMover : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How fast the cart rolls, in units per second. The spawner's Cannonball Speed overrides this.")]
    [SerializeField] private float speed = 10f;

    [Tooltip("How far the cart travels before it is destroyed.")]
    [SerializeField] private float travelDistance = 40f;

    [Header("Hitting Players")]
    [Tooltip("Health removed from a player the cart runs into.")]
    [SerializeField] private float damage = 20f;

    [Tooltip("How hard a hit player is shoved.")]
    [SerializeField] private float knockbackForce = 20f;

    [Tooltip("0 = shove straight along the track, 1 = shove mostly sideways off the track.")]
    [Range(0f, 1f)]
    [SerializeField] private float sidewaysPush = 0.6f;

    private Vector3 direction;
    private Vector3 startPosition;
    private bool hasDirection;

    // Each player only gets hit once per cart
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    // Called by CannonBallSpawn right after the cart is spawned.
    public void SetDirection(Vector3 newDirection, float newSpeed)
    {
        direction = newDirection.normalized;
        speed = newSpeed;
        hasDirection = true;
    }

    private void Awake()
    {
        SetUpHitbox();
    }

    private void Start()
    {
        startPosition = transform.position;

        // If nothing told us which way to go, roll the way the cart is facing
        if (!hasDirection)
            direction = transform.forward;
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        // Destroy the cart once it has gone far enough
        if (Vector3.Distance(startPosition, transform.position) >= travelDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only things with Health (the players) get hit
        Health health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead || alreadyHit.Contains(health))
            return;

        alreadyHit.Add(health);
        health.TakeDamage(damage, gameObject);

        EnemyKnockBack knockBack = health.GetComponent<EnemyKnockBack>();
        if (knockBack != null)
            knockBack.ApplyKnockback(GetPushDirection(health.transform.position), knockbackForce);
    }

    // Shove the player forward along the track and out to the side they are on
    private Vector3 GetPushDirection(Vector3 playerPosition)
    {
        Vector3 forward = new Vector3(direction.x, 0f, direction.z).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);

        Vector3 toPlayer = playerPosition - transform.position;
        if (Vector3.Dot(toPlayer, side) < 0f)
            side = -side;

        return Vector3.Lerp(forward, side, sidewaysPush).normalized;
    }

    // Adds a trigger box around the whole cart model, plus a kinematic
    // Rigidbody so Unity reports when it overlaps a player.
    private void SetUpHitbox()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        // Measure the cart model in the cart's own (local) space
        Bounds localBounds = new Bounds();
        bool first = true;
        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 local = transform.InverseTransformPoint(corner);
                if (first) { localBounds = new Bounds(local, Vector3.zero); first = false; }
                else localBounds.Encapsulate(local);
            }
        }

        BoxCollider hitbox = gameObject.AddComponent<BoxCollider>();
        hitbox.isTrigger = true;
        hitbox.center = localBounds.center;
        hitbox.size = localBounds.size;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    // Shows the cart's path in the Scene view when it is selected
    private void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? startPosition : transform.position;
        Vector3 dir = Application.isPlaying ? direction : transform.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, from + dir * travelDistance);
        Gizmos.DrawWireSphere(from + dir * travelDistance, 0.5f);
    }
}
