using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarrelExplosion : MonoBehaviour
{
    public float explosionRadius = 5f;
    public float knockBackForce = 15f;

   public void Explosion()
   {
        Collider[] objectsInRange = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach(Collider hit in objectsInRange)
        {
            if(hit.CompareTag("Player"))
            {
                PlayerController player = hit.GetComponentInParent<PlayerController>();
                if(player != null)
                {
                    Vector3 direction = hit.transform.position - transform.position;
                    direction.y = 0f;
                    direction.Normalize();
                    player.ApplyKnockBack(direction * knockBackForce);
                }
            }
        }
   }
    

   
}


 






