using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class ExplosiveBox : MonoBehaviour
    {

        [HideInInspector]
        public float ExplodeTimer = 0;
        [HideInInspector]
        public bool Activated = false;

        public GameObject ExplodeParticle;
        public GameObject FlameParticle;
        public GameObject ExplodeDecalPrefab;

        [HideInInspector]
        public DamageControl MyDamageControl;

        [HideInInspector]
        public Rigidbody RBody;
        // Use this for initialization
        void Start()
        {
            MyDamageControl = GetComponent<DamageControl>();
            ExplodeTimer = 2f;
            RBody = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {
            if (MyDamageControl.Damage < MyDamageControl.MaxDamage)
            {
                Activate();
            }

            if (Activated)
            {
                ExplodeTimer -= Time.deltaTime;
                if (ExplodeTimer <= 0)
                {
                    GameControl.MainGameControl.TotalObjectsCount -= 1;
                    Explode();
                    return;
                }


                RBody.AddForceAtPosition(-500 * transform.up, transform.position - 2 * transform.up);
            }

            if (MyDamageControl.IsDead)
            {
                GameControl.MainGameControl.TotalObjectsCount -= 1;
                Explode();
            }

            //print(transform.up.ToString());
        }

        public void Explode()
        {
            GameObject obj = Instantiate(ExplodeParticle);
            obj.transform.position = transform.position;
            Destroy(obj, 6);



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
                ExplodeTimer = 2.5f;

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