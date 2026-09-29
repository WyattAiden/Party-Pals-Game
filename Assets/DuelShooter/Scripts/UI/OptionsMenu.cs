using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DuelShooter
{
    public class OptionsMenu : Menu
    {

        // Use this for initialization
        int selectedMap = 1;
        void Start()
        {
            ImagesList["SelectedMap"].transform.localPosition = ImagesList["Map" + selectedMap.ToString()].transform.localPosition;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void BtnExit()
        {
            int rs = Random.Range(1, 4);
            //SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            MenuControl.GeneralMenuControl.ShowMenu("MainMenu");
            Close();
        }

        public void BtnNext()
        {
            int rs = Random.Range(1, 4);
            // SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            DataStorage.MainDataStorage.SelectedMap = selectedMap;
            MenuControl.GeneralMenuControl.ShowMenu("PlayersMenu");
            Close();
        }

        public void BtnSelectMap(int num)
        {
            int rs = Random.Range(1, 4);
            //SoundGallery.PlaySound("Die" + rs.ToString(), Random.Range(0.9f, 1.1f));
            selectedMap = num;
            ImagesList["SelectedMap"].transform.localPosition = ImagesList["Map" + num.ToString()].transform.localPosition;
        }
    }
}