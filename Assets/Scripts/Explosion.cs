using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Explosion : MonoBehaviour
    {

        // Use this for initialization
        public float Radius = 5;
        void Start()
        {
            //CameraControl.MainCameraControl.StartShake(1f, 1f);
            /*
            if (transform.position.y <= 2)
            {
                GameObject obj = Instantiate(GlobalContents.MainGlobalContent.DecalsPrefabs[0]);
                Vector3 pos = transform.position;
                pos.y = 0.1f;
                obj.transform.position = pos;
                Destroy(obj, 30);
            }
            */

            Collider[] colls = Physics.OverlapSphere(transform.position, Radius);
            foreach (Collider col in colls)
            {
                if (col.gameObject.tag == "Player")
                {
                    float lerp = Vector3.Distance(col.bounds.center, transform.position) / (float)Radius;
                    Health p = col.gameObject.GetComponent<Health>();
                    if (p.TryGetComponent(out Health health))
                        health.TakeDamage(20f);
                    //print(Vector3.Distance(col.bounds.center, transform.position));
                }
                else if (col.gameObject.tag == "Block")
                {
                    Rigidbody rb = col.gameObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForceAtPosition(col.gameObject.transform.position - transform.forward, transform.position);
                    }

                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        float lerp = Vector3.Distance(col.bounds.center, transform.position) / Radius;
                        d.ApplyDamage(Mathf.Lerp(10, 1, lerp), transform.forward, 1);
                    }
                }
            }
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
