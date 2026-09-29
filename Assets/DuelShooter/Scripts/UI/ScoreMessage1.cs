using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class ScoreMessage1 : Menu
    {

        public Text[] NameTexts;
        public Text[] ScoreTexts;
        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            NameTexts[0].text = GameControl.MainGameControl.PlayerControls[0].PlayerName;
            NameTexts[1].text = GameControl.MainGameControl.PlayerControls[1].PlayerName;

            ScoreTexts[0].text = GameControl.MainGameControl.PlayerControls[0].Kills.ToString();
            ScoreTexts[1].text = GameControl.MainGameControl.PlayerControls[1].Kills.ToString();
        }
    }
}