using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Crate_A : MonoBehaviour
    {
        [HideInInspector]
        public DamageControl MyDamageControl;

        public GameObject BrakeEffectPrefab1;

        public bool IsHazardBox = false;
        // Use this for initialization
        void Start()
        {
            MyDamageControl = GetComponent<DamageControl>();
        }

        // Update is called once per frame
        void Update()
        {
            if (transform.position.y <= -4f)
            {
                GameControl.MainGameControl.TotalObjectsCount -= 1;
                Destroy(gameObject);
            }


            if (MyDamageControl.IsDead)
            {
                GameObject obj = Instantiate(BrakeEffectPrefab1);
                obj.transform.position = transform.position;
                Destroy(obj, 3);

                if (!IsHazardBox)
                {
                    int r = Random.Range(0, 4);
                    //r = 2;
                    if (r <= 2)
                    {
                        obj = Instantiate(GlobalContents.MainGlobalContent.PickupPrefabs[1]);
                        obj.transform.position = transform.position;
                    }
                    else
                    {
                        obj = Instantiate(GlobalContents.MainGlobalContent.PickupPrefabs[0]);
                        obj.transform.position = transform.position;
                    }
                }
                else
                {
                    obj = (GameObject)Instantiate(GlobalContents.MainGlobalContent.ItemPrefabs[0]);
                    Vector3 pos = transform.position;
                    pos.y = 0;
                    obj.transform.position = pos;
                }

                GameControl.MainGameControl.TotalObjectsCount -= 1;
                Destroy(gameObject);
            }
        }
    }
}