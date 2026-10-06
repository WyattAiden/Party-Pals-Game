using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnockBackAttack : MonoBehaviour
{
    public KeyCode attackKey = KeyCode.F;
    public float range = 3f;
    public float knockbackForce = 30f;
    public LayerMask enemyLayer = ~0;   // everything by default

    
    void Update()
    {
        if (Input.GetKeyDown(attackKey))
            Knockback();
    }

    void Knockback()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range, enemyLayer);

        foreach (Collider hit in hits)
        {
            EnemyKnockBack enemy = hit.GetComponent<EnemyKnockBack>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - transform.position;
            enemy.ApplyKnockback(dir, knockbackForce);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
