using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Weapon_Pistol1 : Weapon_Base
    {

        // Use this for initialization
        void Start()
        {

        }


        public override void FireWeapon(Vector3 pos, Vector3 dir)
        {
            //base.FireWeapon(pos, dir);
            GameObject obj = Instantiate(BulletPrefab);
            obj.transform.position = pos + new Vector3(0, 1.4f, 0);
            obj.transform.forward = dir;
            Projectile_Base proj = obj.GetComponent<Projectile_Base>();
            proj.Creator = Owner.MyPlayerChar.gameObject;
            Destroy(obj, .5f);

            obj = Instantiate(EffectPrefab);
            obj.transform.position = CurrentWeaponModel.MuzzlePos.position;
            obj.transform.forward = dir;
            //obj.transform.parent = CurrentWeaponModel.transform;
            Destroy(obj, 5);
        }
    }
}