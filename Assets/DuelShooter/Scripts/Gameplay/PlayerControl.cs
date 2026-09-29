using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class PlayerControl : MonoBehaviour
    {

        [HideInInspector]
        public PlayerChar MyPlayerChar;
        [HideInInspector]
        public PlayerControl OtherControl;

        public GameObject PlayerPrefab1;

        public int ControllerNum = 0;
        [HideInInspector]
        public string PlayerName = "Player";
        [HideInInspector]
        public Color PlayerUIColor = Color.white;

        public Material[] PlayerMats;

        [HideInInspector]
        public int Kills = 0;

        bool Dead = false;
        float RespawnDelay = 0;

        [HideInInspector]
        public Vector3 ReticlePosition;

        [HideInInspector]
        public Weapon_Base[] Weapons = new Weapon_Base[4];
        [HideInInspector]
        public int CurrentWeaponNum = 0;
        [HideInInspector]
        public Weapon_Base CurrentWeapon;

        [HideInInspector]
        public int Ammo_Mine = 2;

        [HideInInspector]
        public PlayerControl TargetPlayer;

        [HideInInspector]
        public bool InputEnable = true;

        [HideInInspector]
        public Transform NextSpawnPoint;

        [HideInInspector]
        public Vector3 LastDeathPosition;

        bool SelectedSpawnPoint = false;
        void Awake()
        {
            //GameControl.MainGameControl.PlayerControls.Insert(ControllerNum, this);
            //GameControl.MainGameControl.PlayerControls.Add(this);
        }

        void Start()
        {
            InputEnable = true;

            Weapons = new Weapon_Base[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject obj = (GameObject)Instantiate(GlobalContents.MainGlobalContent.WeaponPrefabs[i]);
                Weapon_Base w = obj.GetComponent<Weapon_Base>();
                Weapons[i] = w;
                Weapons[i].AmmoCount = 0;
                Weapons[i].WeaponEnable = false;
                Weapons[i].Owner = this;
            }

            Weapons[0].AmmoCount = 400;
            Weapons[0].WeaponEnable = true;
            Weapons[1].AmmoCount = 10;
            Weapons[1].WeaponEnable = true;
            Weapons[2].AmmoCount = 40;
            Weapons[2].WeaponEnable = true;
            Weapons[3].AmmoCount = 1;
            Weapons[3].WeaponEnable = true;
            Weapons[4].AmmoCount = 1;
            Weapons[4].WeaponEnable = true;

            Respawn();
        }


        // Update is called once per frame
        void Update()
        {
            if (Dead)
            {
                RespawnDelay -= Time.deltaTime;

                if (RespawnDelay <= 1.8f)
                {
                    if (!SelectedSpawnPoint)
                    {
                        SelectedSpawnPoint = true;
                        FindSpawnPoint();
                        GameObject obj = Instantiate(GlobalContents.MainGlobalContent.SpawnTrailPrefabs[ControllerNum]);
                        obj.transform.position = LastDeathPosition;
                        obj.GetComponent<SpawnTrail1>().TargetPos = NextSpawnPoint.position;
                    }
                }
                if (RespawnDelay <= 0)
                {
                    RespawnDelay = 0;
                    Dead = false;
                    Respawn();
                }
            }

            foreach (PlayerControl p in GameControl.MainGameControl.PlayerControls)
            {
                if (p != this)
                {
                    TargetPlayer = p;
                    break;
                }
            }
        }

        public void Kill()
        {
            Dead = true;
            SelectedSpawnPoint = false;
            RespawnDelay = 2;
        }

        public void Respawn()
        {
            GameObject obj = Instantiate(PlayerPrefab1);
            MyPlayerChar = obj.GetComponent<PlayerChar>();
            MyPlayerChar.ControllerNum = ControllerNum;
            MyPlayerChar.MyPlayerControl = this;

            if (NextSpawnPoint == null)
            {
                FindSpawnPoint();
            }
            MyPlayerChar.transform.position = NextSpawnPoint.position;
            NextSpawnPoint = null;

            CreateSkin();

            obj = Instantiate(GlobalContents.MainGlobalContent.SpawnEffectPrefabs[ControllerNum]);
            obj.transform.position = MyPlayerChar.transform.position;
        }

        public void FindSpawnPoint()
        {
            PlayerControl other = GameControl.MainGameControl.PlayerControls[0];
            if (GameControl.MainGameControl.PlayerControls[0] == this)
            {
                other = GameControl.MainGameControl.PlayerControls[1];
            }

            while (true)
            {
                int r = Random.Range(0, GameControl.MainGameControl.SpawnPoints.Length);
                if (other.MyPlayerChar == null || Vector3.Distance(other.MyPlayerChar.transform.position, GameControl.MainGameControl.SpawnPoints[r].transform.position) > 20)
                {
                    NextSpawnPoint = GameControl.MainGameControl.SpawnPoints[r].transform;
                    break;
                }
            }
        }
        public void CreateSkin()
        {
            //if (ControllerNum == 0)
            //{
            MyPlayerChar.BodyRenderer.materials[0].color = PlayerMats[0].color;
            //}
            //else
            //{
            //    MyPlayerChar.BodyRenderer.materials[0].color = PlayerMats[0].color;
            //}
        }

        public void GiveWeapon(int num, int ammo)
        {
            Weapons[num].AmmoCount = ammo;
            Weapons[num].WeaponEnable = true;

            CurrentWeaponNum = num;
            CurrentWeapon = Weapons[num];
            MyPlayerChar.ArmWeapon();
        }
    }
}