using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    // Spawned by Bomb.cs when a bomb lands. Damages and knocks back players in range
    // and pushes "Block" rigidbodies.
    public class Explosion : MonoBehaviour
    {
        public float Radius = 5;
        public float Damage = 20f;

        [Header("Knockback")]
        [Tooltip("Push strength on a player standing right at the centre of the blast.")]
        public float KnockbackForce = 15f;
        [Tooltip("Fraction of the push a player at the very edge of the blast gets (0-1).")]
        [Range(0f, 1f)] public float EdgeKnockbackMultiplier = 0.5f;

        void Start()
        {
            var hitPlayers = new HashSet<PlayerController>();
            var damaged = new HashSet<Health>();

            Collider[] colls = Physics.OverlapSphere(transform.position, Radius);
            foreach (Collider col in colls)
            {
                if (col.CompareTag("Player"))
                {
                    // 0 at the centre, 1 at the edge
                    float t = Mathf.Clamp01(Vector3.Distance(col.bounds.center, transform.position) / Radius);

                    Health health = col.GetComponentInParent<Health>();
                    if (health != null && damaged.Add(health))
                        health.TakeDamage(Damage);

                    PlayerController player = col.GetComponentInParent<PlayerController>();
                    if (player != null && hitPlayers.Add(player))
                    {
                        Vector3 dir = player.transform.position - transform.position;
                        dir.y = 0f; // push along the ground
                        if (dir.sqrMagnitude < 0.001f) dir = player.transform.forward * -1f; // standing dead centre: push backwards
                        dir.y = 0f;
                        float strength = KnockbackForce * Mathf.Lerp(1f, EdgeKnockbackMultiplier, t);
                        player.ApplyKnockBack(dir.normalized * strength);
                    }
                }
                else if (col.CompareTag("Block"))
                {
                    Rigidbody rb = col.GetComponent<Rigidbody>();
                    if (rb != null)
                        rb.AddForceAtPosition(col.transform.position - transform.forward, transform.position);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
