using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class DataStorage : MonoBehaviour
    {

        public static DataStorage MainDataStorage;

        [HideInInspector]
        public int SelectedMap;

        [HideInInspector]
        public string[] PlayerNames;
        void Awake()
        {
            MainDataStorage = this;
            Load();
        }
        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void Save()
        {
            PlayerPrefs.SetString("PlayerName1", PlayerNames[0]);
            PlayerPrefs.SetString("PlayerName2", PlayerNames[1]);

            PlayerPrefs.Save();
        }

        public void Load()
        {
            PlayerNames = new string[2];
            PlayerNames[0] = PlayerPrefs.GetString("PlayerName1");
            PlayerNames[1] = PlayerPrefs.GetString("PlayerName2");
        }
    }
}