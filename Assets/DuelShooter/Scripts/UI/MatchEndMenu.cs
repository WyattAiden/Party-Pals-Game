using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DuelShooter
{
    public class MatchEndMenu : Menu
    {

        public Text[] NameTexts;
        public Text[] ScoreTexts;


        void Start()
        {
            Cursor.visible = true;

            NameTexts[0].text = GameControl.MainGameControl.PlayerControls[0].PlayerName;
            NameTexts[1].text = GameControl.MainGameControl.PlayerControls[1].PlayerName;

            ScoreTexts[0].text = GameControl.MainGameControl.PlayerControls[0].Kills.ToString();
            ScoreTexts[1].text = GameControl.MainGameControl.PlayerControls[1].Kills.ToString();
        }
        // Update is called once per frame
        void Update()
        {

        }

        public void BtnExit()
        {

            int rs = Random.Range(1, 4);
            // SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            SceneManager.LoadScene("MainMenuScene");
        }

        public void BtnRematch()
        {

            int rs = Random.Range(1, 4);
            //SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}