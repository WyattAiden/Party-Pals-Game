using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Weapon_Shotgun1 : Weapon_Base
    {

        // Use this for initialization
        void Start()
        {

        }

        public override void FireWeapon(Vector3 pos, Vector3 dir)
        {
            //base.FireWeapon(pos, dir);
            for (int i = -3; i <= 3; i++)
            {
                GameObject obj = Instantiate(BulletPrefab);
                obj.transform.position = pos + new Vector3(0, 1.4f, 0);
                obj.transform.forward = Quaternion.Euler(0, i * 2, 0) * dir;
                Projectile_Base proj = obj.GetComponent<Projectile_Base>();
                proj.Creator = Owner.MyPlayerChar.gameObject;
                Destroy(obj, .2f);
            }

            GameObject obj1 = Instantiate(EffectPrefab);
            obj1.transform.position = CurrentWeaponModel.MuzzlePos.position;
            obj1.transform.forward = dir;
            //obj.transform.parent = CurrentWeaponModel.transform;
            Destroy(obj1, 5);
        }
    }
}