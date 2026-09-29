using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Mine_A : MonoBehaviour
    {

        public GameObject ExplosionPrefab1;
        public GameObject ActivateEffectPrefab1;
        bool Activated = false;
        float ExplodeDelay = 1;

        public Renderer LightMesh;
        public Material LightActivateMat;

        [HideInInspector]
        public PlayerControl Creator;
        void Start()
        {
            ExplodeDelay = 1;
        }

        // Update is called once per frame
        void Update()
        {
            if (!Activated)
            {
                foreach (PlayerControl p in GameControl.MainGameControl.PlayerControls)
                {
                    if (p.MyPlayerChar != null)
                    {
                        if (Vector3.Distance(p.MyPlayerChar.transform.position, transform.position) <= 4)
                        {
                            //SoundGallery.PlaySound("Click1");
                            GameObject obj = Instantiate(ActivateEffectPrefab1);
                            obj.transform.position = transform.position;
                            Destroy(obj, 2);
                            LightMesh.materials[1].CopyPropertiesFromMaterial(LightActivateMat);
                            Activated = true;
                            ExplodeDelay = .3f;
                            break;
                        }

                        //if (Vector3.Distance(p.MyPlayerChar.transform.position, transform.position) <= 7)
                        //{
                        //    Vector3 Move = p.MyPlayerChar.transform.position - transform.position;
                        //    Move.y = 0;
                        //    Move.Normalize();
                        //    transform.position += 8 * Time.deltaTime * Move;
                        //    transform.rotation = Quaternion.Euler(0, Time.deltaTime * 600, 0) * transform.rotation;
                        //}
                    }
                }
            }
            else
            {
                ExplodeDelay -= Time.deltaTime;
                if (ExplodeDelay <= 0)
                {
                    GameObject obj = Instantiate(ExplosionPrefab1);
                    obj.transform.position = transform.position;
                    Destroy(obj, 6);
                    Destroy(gameObject);
                }
            }
        }
    }
}