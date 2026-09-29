using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class GameControl : MonoBehaviour
    {

        //[HideInInspector]
        public List<PlayerControl> PlayerControls;

        public static GameControl MainGameControl;

        [HideInInspector]
        public GameObject[] SpawnPoints;
        [HideInInspector]
        public GameObject[] ObjectSpawnPoints;


        float SpawnBoxDelay = 0;
        int SpawnBoxCounter = 0;
        float SpawnWeaponDelay = 0;

        [HideInInspector]
        public int KillLimit = 10;
        [HideInInspector]
        public float TimeLimit = 180;
        [HideInInspector]
        public float Timer = 180;

        public int GameState = 0;

        [HideInInspector]
        public int TotalObjectsCount = 0;

        bool SlowMotionEnable = false;
        float SlowMotionTimer = 0;

        public bool Paused = false;
        void Awake()
        {

            MainGameControl = this;
            //PlayerControls = new List<PlayerControl>(2);
            //PlayerControls=new List<PlayerControl>()

            //GameObject[] obj = GameObject.FindGameObjectsWithTag("PlayerControl");

            //for (int i = 0; i < obj.Length; i++)
            //{
            //    PlayerControl pc = obj[i].GetComponent<PlayerControl>();
            //}

            SpawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
            ObjectSpawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint_Object");
        }

        // Use this for initialization
        void Start()
        {
            StartGameState(0);
            TimeLimit = 180;
            KillLimit = 5;
            Timer = TimeLimit;
            Time.timeScale = 1f;
            PlayerControls[0].PlayerName = "Player 1";
            PlayerControls[1].PlayerName = "Player 2";

            PlayerControls[0].PlayerUIColor = GlobalContents.MainGlobalContent.PlayerUIColors[0];
            PlayerControls[1].PlayerUIColor = GlobalContents.MainGlobalContent.PlayerUIColors[1];
            //PlayerControls[0].PlayerUIColor = GlobalContents.MainGlobalContent.PlayerUIColors[0];
            //PlayerControls[0].PlayerUIColor = GlobalContents.MainGlobalContent.PlayerUIColors[0];
        }

        // Update is called once per frame
        void Update()
        {
            if (GameState == 0)
            {
                if (TotalObjectsCount < 5)
                {
                    SpawnBoxDelay -= Time.deltaTime;
                    if (SpawnBoxDelay <= 0)
                    {
                        SpawnBoxDelay = Random.Range(4f, 8f);
                        GameObject obj = null;
                        if (SpawnBoxCounter <= 2)
                        {
                            obj = Instantiate(GlobalContents.MainGlobalContent.CratePrefabs[0]);
                        }
                        else if (SpawnBoxCounter == 3)
                        {
                            obj = Instantiate(GlobalContents.MainGlobalContent.CratePrefabs[1]);
                        }
                        else if (SpawnBoxCounter == 4)
                        {
                            obj = Instantiate(GlobalContents.MainGlobalContent.CratePrefabs[2]);
                        }

                        SpawnBoxCounter++;
                        if (SpawnBoxCounter > 4)
                        {
                            SpawnBoxCounter = 0;
                        }

                        int r = Random.Range(0, ObjectSpawnPoints.Length);
                        obj.transform.position = ObjectSpawnPoints[r].transform.position + new Vector3(0, 100, 0);
                        obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                        TotalObjectsCount++;
                    }
                }

                //SpawnWeaponDelay -= Time.deltaTime;
                //if (SpawnWeaponDelay <= 0)
                //{
                //    Vector3 pos = new Vector3(0, 2, 0);
                //    SpawnWeaponDelay = Random.Range(11f, 15f);
                //    int num = Random.Range(2, 6);
                //    GameObject obj = Instantiate(GlobalContents.MainGlobalContent.PickupPrefabs[num]);
                //    obj.transform.position = pos;
                //    Destroy(obj, 10);

                //    obj = Instantiate(GlobalContents.MainGlobalContent.SpawnEffectPrefab1);
                //    obj.transform.position = pos;
                //    Destroy(obj, 3);
                //}

                //Timer -= Time.deltaTime;
                if (Timer <= 0)
                {
                    Timer = 0;
                    StartGameState(1);
                }
                else
                {
                    int winer = -1;
                    for (int i = 0; i < PlayerControls.Count; i++)
                    {
                        if (PlayerControls[i].Kills >= KillLimit)
                        {
                            winer = i;
                            break;
                        }
                    }

                    if (winer != -1)
                    {

                        StartGameState(1);
                    }
                }
            }

            if (SlowMotionEnable)
            {
                SlowMotionTimer -= Time.deltaTime;
                if (SlowMotionTimer <= 0)
                {
                    SlowMotionEnable = false;
                    Time.timeScale = 1f;
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Paused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void StartGameState(int s)
        {
            GameState = s;

            switch (GameState)
            {
                case 0:
                    break;

                case 1:
                    Invoke("ShowEndMenu", 2);
                    PlayerControls[0].InputEnable = false;
                    PlayerControls[1].InputEnable = false;
                    break;
            }
        }
        public void StartSlowMotion()
        {
            if (!SlowMotionEnable)
            {
                SlowMotionEnable = true;
                SlowMotionTimer = 0.2f;
                Time.timeScale = 0.2f;
            }
        }

        public void PauseGame()
        {
            Paused = true;
            Time.timeScale = 0;
            PlayerControls[0].InputEnable = false;
            PlayerControls[1].InputEnable = false;
            Cursor.visible = true;
            MenuControl.GeneralMenuControl.ShowMenu("PauseMenu");
        }

        public void ResumeGame()
        {
            Paused = false;
            Time.timeScale = 1f;
            PlayerControls[0].InputEnable = true;
            PlayerControls[1].InputEnable = true;
            //Cursor.visible = false;

            PauseMenu.MainPauseMenu.Close();
        }


        public void ShowEndMenu()
        {
            MenuControl.GeneralMenuControl.ShowMenu("MatchEndMenu");
        }



        public void ShowKillMessage(PlayerControl Killer, PlayerControl Target, Vector3 pos)
        {
            Popup_KillMassage1 msg = (Popup_KillMassage1)MenuControl.GeneralMenuControl.ShowMenu("KillMassage");
            msg.Killer = Killer;
            msg.Target = Target;
            msg.pos = pos;
        }
    }
}