using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DuelShooter
{
    public class PauseMenu : Menu
    {

        public Text[] NameTexts;
        public Text[] ScoreTexts;

        public static PauseMenu MainPauseMenu;
        // Use this for initialization
        void Start()
        {
            Cursor.visible = true;

            NameTexts[0].text = GameControl.MainGameControl.PlayerControls[0].PlayerName;
            NameTexts[1].text = GameControl.MainGameControl.PlayerControls[1].PlayerName;

            ScoreTexts[0].text = GameControl.MainGameControl.PlayerControls[0].Kills.ToString();
            ScoreTexts[1].text = GameControl.MainGameControl.PlayerControls[1].Kills.ToString();

            MainPauseMenu = this;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void BtnExit()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenuScene");

            int rs = Random.Range(1, 4);
            // SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
        }

        public void BtnRematch()
        {

            int rs = Random.Range(1, 4);
            // SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            Time.timeScale = 1.5f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void BtnResume()
        {

            int rs = Random.Range(1, 4);
            //SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            GameControl.MainGameControl.ResumeGame();
            Close();
        }
    }
}