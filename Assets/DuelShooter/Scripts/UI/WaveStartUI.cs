using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class WaveStartUI : Menu
    {

        public Text WaveText;
        // Use this for initialization
        void Start()
        {
            //WaveText.text = "Wave "+ GameControl.MainGameControl.WaveNum.ToString();

            Invoke("Close", 3);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}