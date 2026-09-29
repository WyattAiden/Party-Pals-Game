using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class MenuPlayerDummy1 : MonoBehaviour
    {
        public Transform WeaponPoint;
        // Use this for initialization
        void Start()
        {

            GameObject obj = Instantiate(GlobalContents.MainGlobalContent.WeaponPrefabs[2].GetComponent<Weapon_Base>().WeaponModelPrefab);
            obj.transform.position = WeaponPoint.position;
            obj.transform.rotation = WeaponPoint.rotation;
            obj.transform.parent = WeaponPoint;
        }

        // Update is called once per frame
        void Update()
        {
            transform.rotation *= Quaternion.Euler(0, Time.deltaTime * 50, 0);
        }
    }
}