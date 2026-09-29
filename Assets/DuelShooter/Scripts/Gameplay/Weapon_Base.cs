using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class Weapon_Base : MonoBehaviour
    {

        public bool AutoFire = true;
        public float FireDelay = 0.2f;
        public int MaxAmmo = 80;
        public Sprite WeaponIcon;

        public bool InfiniteAmmo = false;

        public GameObject WeaponModelPrefab;
        public GameObject BulletPrefab;
        public GameObject EffectPrefab;

        [HideInInspector]
        public bool WeaponEnable = true;
        [HideInInspector]
        public int AmmoCount = 50;

        [HideInInspector]
        public WeaponModel CurrentWeaponModel;

        [HideInInspector]
        public PlayerControl Owner;

        public string AmmoString = "00";
        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (InfiniteAmmo)
            {
                AmmoString = "∞";
            }
            else
            {
                AmmoString = AmmoCount.ToString();
            }
        }

        public virtual void FireWeapon(Vector3 pos, Vector3 dir)
        {
            GameObject obj = Instantiate(BulletPrefab);
            //obj.transform.position = CurrentWeaponModel.transform.position;
            obj.transform.position = pos + new Vector3(0, 1.4f, 0);
            obj.transform.forward = Quaternion.Euler(0, Random.Range(-2, 3), 0) * dir;
            Projectile_Base proj = obj.GetComponent<Projectile_Base>();
            proj.Creator = Owner.MyPlayerChar.gameObject;
            Destroy(obj, 5);

            obj = Instantiate(EffectPrefab);
            obj.transform.position = CurrentWeaponModel.MuzzlePos.position;
            obj.transform.forward = dir;
            //obj.transform.parent = CurrentWeaponModel.transform;
            Destroy(obj, 5);
        }
    }
}