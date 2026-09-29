using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DuelShooter
{
    public class PlayersMenu : Menu
    {

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void BtnExit()
        {
            int rs = Random.Range(1, 4);
            // SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            MenuControl.GeneralMenuControl.ShowMenu("OptionsMenu");
            Close();
        }

        public void BtnStart()
        {
            int rs = Random.Range(1, 4);
            //SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            SceneManager.LoadScene("Scene" + (DataStorage.MainDataStorage.SelectedMap - 1).ToString());

        }
    }
}