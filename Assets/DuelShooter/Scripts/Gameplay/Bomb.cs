using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
namespace DuelShooter
{
    public class Bomb : MonoBehaviour
    {

        [HideInInspector]
        public float ExplodeTimer = 0;
        [HideInInspector]
        public bool Activated = false;

        public GameObject ExplodeParticle;
        public GameObject FlameParticle;
        public GameObject ExplodeDecalPrefab;

        


        [HideInInspector]
        public Rigidbody RBody;
        // Use this for initialization
        void Start()
        {
            
            ExplodeTimer = 2f;
            RBody = GetComponent<Rigidbody>();
        }

        

        // Update is called once per frame
        void Update()
        {
            

            if (Activated)
            {
                ExplodeTimer -= Time.deltaTime;
                if (ExplodeTimer <= 0)
                {
                   
                    Explode();
                    return;
                }


                
            }
            /*
            if ()
            {
                GameControl.MainGameControl.TotalObjectsCount -= 1;
                Explode();
            }
            */
            //print(transform.up.ToString());
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                Activate();
                Debug.Log(gameObject);
            }
        }

        public void Explode()
        {
            GameObject obj = Instantiate(ExplodeParticle);
            obj.transform.position = transform.position;
            Destroy(obj, 6);

            BarrelExplosion knockback = GetComponent<BarrelExplosion>();
            if (knockback != null)
            {
                knockback.Explosion();
            }



            //obj = Instantiate(ExplodeDecalPrefab);
            //Vector3 pos = transform.position;
            //pos.y = 0.2f;
            //obj.transform.position = pos;

            //obj.transform.localScale = Random.Range(8f, 12f) * Vector3.one;
            //obj.transform.rotation = Quaternion.Euler(90, 0, Random.Range(0f, 360f));



            Destroy(gameObject);
        }

        public void Activate()
        {
            if (!Activated)
            {
                Activated = true;
                ExplodeTimer = 0.5f;

                GameObject obj = Instantiate(FlameParticle);
                obj.transform.position = transform.position;
                obj.transform.parent = transform;
            }
        }

        void OnDrawGizmos()
        {

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 12f);
        }
    }
}