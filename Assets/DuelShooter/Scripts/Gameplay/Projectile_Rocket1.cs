using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Projectile_Rocket1 : Projectile_Base
    {

        public GameObject TrailEffect;
        // Use this for initialization
        void Start()
        {

        }


        public override void Destroyed(Vector3 pos)
        {
            base.Destroyed(pos);
            TrailEffect.transform.parent = null;
            Destroy(TrailEffect, .6f);
        }
    }
}