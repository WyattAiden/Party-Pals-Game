using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuelShooter
{
    public class PlayerInfoPanel1 : MonoBehaviour
    {

        [HideInInspector]
        public PlayerControl MyPlayerControl;

        public Text PlayerName;
        public Text AmmoText;
        public Text MineAmmoText;
        //public Text KillText;
        public Image WeaponImage;
        public Image Background;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (MyPlayerControl != null)
            {
                PlayerName.text = MyPlayerControl.PlayerName;
                if (MyPlayerControl.CurrentWeapon!=null)
                    AmmoText.text = MyPlayerControl.CurrentWeapon.AmmoString;
                MineAmmoText.text = MyPlayerControl.Ammo_Mine.ToString();
                // if (KillText != null)
                // {
                //     KillText.text = "Kills: " + MyPlayerControl.Kills.ToString();
                // }
                //KillText.text = "Kills  :  " + MyPlayerControl.Kills.ToString();
                //KillText.text = MyPlayerControl.Kills.ToString();
                if (MyPlayerControl.CurrentWeapon != null)
                    WeaponImage.sprite = MyPlayerControl.CurrentWeapon.WeaponIcon;
            }
        }
    }
}