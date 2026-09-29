using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class PickUpItem : MonoBehaviour
    {

        [HideInInspector]
        public bool Picked = false;

        public GameObject PickUpParticle;

        public int ItemID = 0;
        // Use this for initialization
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            transform.rotation = Quaternion.Euler(0, Time.deltaTime * 100, 0) * transform.rotation;

            Collider[] colls = Physics.OverlapSphere(transform.position, 3f);
            foreach (Collider col in colls)
            {
                if (col.gameObject.tag == "Player")
                {
                    PlayerChar p = col.gameObject.GetComponent<PlayerChar>();
                    if (ItemID == 0) //health
                    {
                        p.MyDamageControl.AddHealth(8);
                        Pick();
                        break;
                    }
                    else if (ItemID == 1) //pistol ammo
                    {

                        //p.MyPlayerControl.AmmoCounts[0] += 20;
                        int r = Random.Range(1, 5);
                        switch (r)
                        {
                            case 1:
                                p.MyPlayerControl.Weapons[1].AmmoCount += 10;
                                GameUI.MainGameUI.ShowPopUpText(transform.position, "Shotgun +10");
                                break;
                            case 2:
                                p.MyPlayerControl.Weapons[2].AmmoCount += 50;
                                GameUI.MainGameUI.ShowPopUpText(transform.position, "MachinGun +50");
                                break;
                            case 3:
                                p.MyPlayerControl.Weapons[3].AmmoCount += 2;
                                GameUI.MainGameUI.ShowPopUpText(transform.position, "RPG +2");
                                break;
                            case 4:
                                p.MyPlayerControl.Weapons[4].AmmoCount += 2;
                                GameUI.MainGameUI.ShowPopUpText(transform.position, "Sniper +2");
                                break;
                        }
                        Pick();
                        break;
                    }
                    else if (ItemID == 22)
                    {
                        p.MyPlayerControl.GiveWeapon(1, 20);
                        GameUI.MainGameUI.ShowPopUpText(transform.position, "Shotgun (20)");
                        Pick();
                    }
                    else if (ItemID == 23)
                    {
                        p.MyPlayerControl.GiveWeapon(2, 70);
                        GameUI.MainGameUI.ShowPopUpText(transform.position, "MachinGun (70)");
                        Pick();
                    }
                    else if (ItemID == 24)
                    {
                        p.MyPlayerControl.GiveWeapon(3, 5);
                        GameUI.MainGameUI.ShowPopUpText(transform.position, "RPG (5)");
                        Pick();
                    }
                    else if (ItemID == 25)
                    {
                        p.MyPlayerControl.GiveWeapon(4, 10);
                        GameUI.MainGameUI.ShowPopUpText(transform.position, "Sniper (10)");
                        Pick();
                    }
                }
            }
        }

        public void Pick()
        {
            if (!Picked)
            {
                GameObject obj = Instantiate(PickUpParticle);
                obj.transform.position = transform.position;
                Destroy(obj, 2);
                Picked = true;
                Destroy(gameObject);
            }
        }
    }
}