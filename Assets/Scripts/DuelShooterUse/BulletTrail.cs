using UnityEngine;

// Adapted from DuelShooter's BulletTrail1. A cheap streak that stretches from where a
// bullet was fired to where it is now, then thins out.
// Setup: make a prefab from a Quad with an unlit (ideally additive) material, put this
// script on it, then add ProjectileTrail to your Projectile prefab and assign this prefab.
public class BulletTrail : MonoBehaviour
{
    [SerializeField] private float width = 0.3f;
    [Tooltip("Seconds for the streak to thin out completely.")]
    [SerializeField] private float fadeTime = 0.6f;
    [Tooltip("Longest the streak can get. 0 = no limit.")]
    [SerializeField] private float maxLength = 8f;

    private Transform bullet;
    private Vector3 start;
    private Vector3 end;
    private float life = 1f;

    public static BulletTrail Spawn(BulletTrail prefab, Transform bullet)
    {
        BulletTrail trail = Instantiate(prefab, bullet.position, Quaternion.identity);
        trail.bullet = bullet;
        trail.start = trail.end = bullet.position;
        trail.transform.localScale = Vector3.zero;
        return trail;
    }

    void Awake()
    {
        // A Quad's collider would get hit by projectiles - remove it straight away
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
            Destroy(c);
        }
    }

    void LateUpdate()
    {
        life -= Time.deltaTime / Mathf.Max(fadeTime, 0.01f);
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (bullet != null) end = bullet.position;

        Vector3 d = end - start;
        if (maxLength > 0f && d.magnitude > maxLength)
        {
            start = end - d.normalized * maxLength;
            d = end - start;
        }

        if (d.sqrMagnitude < 0.0001f)
        {
            transform.localScale = Vector3.zero;
            return;
        }

        transform.position = (start + end) * 0.5f;
        transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = new Vector3(width * life, d.magnitude, 1f);
    }
}
