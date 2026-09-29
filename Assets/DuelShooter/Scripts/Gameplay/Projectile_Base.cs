using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Projectile_Base : MonoBehaviour
    {

        public GameObject ExplosionPrefab;
        public GameObject HitParticlePrefab1;
        public GameObject HitParticlePrefab2;
        [HideInInspector]
        public GameObject Creator;

        public float Speed = 100;
        public float Damage = 1;
        // Use this for initialization
        void Start()
        {
            GameObject obj = Instantiate(GlobalContents.MainGlobalContent.BulletTrailPrefab1);
            obj.transform.position = transform.position;
            BulletTrail1 bt = obj.GetComponent<BulletTrail1>();
            bt.MyBullet = transform;
        }

        void Update()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, .5f, transform.forward, Speed * Time.deltaTime);
            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;
                if (Vector3.Dot(hit.normal, transform.forward) < 0)
                {
                    if (col.gameObject.tag == "Player")
                    {
                        if (col.gameObject != Creator)
                        {
                            PlayerChar p = col.gameObject.GetComponent<PlayerChar>();
                            p.MyDamageControl.ApplyDamage(Damage, transform.forward, 1);
                            p.ShakeDamage();
                            Destroy(gameObject);

                            Destroyed(hit.point);
                        }
                        //Destroy(gameObject);
                        //GameObject obj = Instantiate(RocketPrefab);
                        //obj.transform.position = transform.position;
                        //obj.transform.forward = transform.forward;
                    }
                    else if (col.gameObject.tag == "Block")
                    {
                        Destroyed(hit.point);

                        Rigidbody rb = col.gameObject.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.AddForceAtPosition((Damage * 500) * transform.forward + new Vector3(0, 2000, 0), transform.position);
                        }

                        DamageControl d = col.gameObject.GetComponent<DamageControl>();
                        if (d != null)
                        {
                            d.ApplyDamage(Damage, transform.forward, 1);
                        }
                    }
                    else if (col.gameObject.tag == "ReflectBlock")
                    {
                        transform.forward = Vector3.Reflect(transform.forward, hit.normal);
                        transform.position = hit.point;
                        GameObject obj = Instantiate(HitParticlePrefab1);
                        obj.transform.position = hit.point;
                        //obj.transform.localScale = 0.4f * Vector3.one;

                        //SoundGallery.PlaySound("RicoMetal1");

                        Destroy(obj, 3);
                    }
                }
            }

            transform.position += Speed * Time.deltaTime * transform.forward;
        }

        public virtual void Destroyed(Vector3 pos)
        {
            Destroy(gameObject);
            GameObject obj = Instantiate(HitParticlePrefab1);
            obj.transform.position = pos;
            //obj.transform.localScale = 0.4f * Vector3.one;
            Destroy(obj, 6);
        }
    }
}