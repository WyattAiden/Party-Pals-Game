using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class DestructBlock1 : MonoBehaviour
    {
        [HideInInspector]
        public DamageControl MyDamageControl;

        public GameObject BrakeEffectPrefab1;
        // Use this for initialization
        void Start()
        {
            MyDamageControl = GetComponent<DamageControl>();
        }

        // Update is called once per frame
        void Update()
        {
            if (MyDamageControl.IsDead)
            {
                GameObject obj = Instantiate(BrakeEffectPrefab1);
                obj.transform.position = transform.position;
                Destroy(obj, 3);

                //obj = Instantiate(GlobalContents.MainGlobalContent.PickupPrefabs[0]);
                //obj.transform.position = transform.position;

                //GameControl.MainGameControl.TotalObjectsCount -= 1;
                Destroy(gameObject);
            }
        }
    }
}