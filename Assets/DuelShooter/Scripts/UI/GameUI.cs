using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class GameUI : Menu
    {


        public Text KillLimitText;
        public Text TimeText;

        public Text[] WeaponAmmoTexts;
        public Text[] ScoreTexts;
        public Image[] WeaponIconImages;
        public float[] WeaponInfoTimer = new float[2];

        public RectTransform canv;

        public PlayerInfoPanel1[] PlayerPanels;

        public GameObject PopupTextPrefab1;

        public static GameUI MainGameUI;

        public Image Reticle1;

        void Awake()
        {
            MainGameUI = this;
            UpdateImageList();
        }
        // Use this for initialization
        void Start()
        {
            //Cursor.visible = false;
            WeaponInfoTimer = new float[2];

            PlayerPanels[0].MyPlayerControl = GameControl.MainGameControl.PlayerControls[0];
            PlayerPanels[1].MyPlayerControl = GameControl.MainGameControl.PlayerControls[1];

            PlayerPanels[0].Background.color = GameControl.MainGameControl.PlayerControls[0].PlayerUIColor;
            PlayerPanels[1].Background.color = GameControl.MainGameControl.PlayerControls[1].PlayerUIColor;
        }

        // Update is called once per frame
        void Update()
        {

            for (int i = 0; i < 2; i++)
            {
                WeaponInfoTimer[i] -= Time.deltaTime;
                ImagesList["WeaponInfo" + (i + 1).ToString()].transform.localScale = 2 * Mathf.Clamp(WeaponInfoTimer[i], 0, 0.5f) * Vector3.one;
                if (WeaponInfoTimer[i] <= 0)
                {
                    WeaponInfoTimer[i] = 0;
                    ImagesList["WeaponInfo" + (i + 1).ToString()].gameObject.SetActive(false);
                }
            }


            TimeText.text = ((int)GameControl.MainGameControl.Timer).ToString();
            KillLimitText.text = "Kill Limit : " + GameControl.MainGameControl.KillLimit.ToString();
            Vector3 v;


            //--------------player 0
            v = GameControl.MainGameControl.PlayerControls[0].ReticlePosition;
            v.x = v.x / Screen.width;
            v.y = v.y / Screen.height;
            //print(v);

            v.x = canv.sizeDelta.x * v.x - 0.5f * canv.sizeDelta.x;
            v.y = canv.sizeDelta.y * v.y - 0.5f * canv.sizeDelta.y;

            Reticle1.rectTransform.localPosition = v;

            if (GameControl.MainGameControl.PlayerControls[0].MyPlayerChar != null)
            {
                v = CameraControl.MainCameraControl.MyCamera.WorldToScreenPoint(GameControl.MainGameControl.PlayerControls[0].MyPlayerChar.transform.position);
                v.x = v.x / (float)Screen.width;
                v.y = v.y / (float)Screen.height;
                v.x = canv.sizeDelta.x * v.x - 0.5f * canv.sizeDelta.x;
                v.y = canv.sizeDelta.y * v.y - 0.5f * canv.sizeDelta.y;

                ImagesList["WeaponInfo1"].rectTransform.localPosition = v + new Vector3(0, 20, 0);
            }

            if (GameControl.MainGameControl.PlayerControls[0].CurrentWeapon != null)
            {
                WeaponAmmoTexts[0].text = GameControl.MainGameControl.PlayerControls[0].CurrentWeapon.AmmoString;
                WeaponIconImages[0].sprite = GameControl.MainGameControl.PlayerControls[0].CurrentWeapon.WeaponIcon;
            }

            if (GameControl.MainGameControl.PlayerControls[1].MyPlayerChar != null)
            {
                v = CameraControl.MainCameraControl.MyCamera.WorldToScreenPoint(GameControl.MainGameControl.PlayerControls[1].MyPlayerChar.transform.position);
                v.x = v.x / (float)Screen.width;
                v.y = v.y / (float)Screen.height;
                v.x = canv.sizeDelta.x * v.x - 0.5f * canv.sizeDelta.x;
                v.y = canv.sizeDelta.y * v.y - 0.5f * canv.sizeDelta.y;

                ImagesList["WeaponInfo2"].rectTransform.localPosition = v + new Vector3(0, 20, 0);
            }

            if (GameControl.MainGameControl.PlayerControls[1].CurrentWeapon != null)
            {
                WeaponAmmoTexts[1].text = GameControl.MainGameControl.PlayerControls[1].CurrentWeapon.AmmoString;
                WeaponIconImages[1].sprite = GameControl.MainGameControl.PlayerControls[1].CurrentWeapon.WeaponIcon;
            }

            ScoreTexts[0].text = GameControl.MainGameControl.PlayerControls[0].Kills.ToString();
            ScoreTexts[1].text = GameControl.MainGameControl.PlayerControls[1].Kills.ToString();
        }

        public void ShowWeaponInfo(int num)
        {
            WeaponInfoTimer[num] = 2;
            ImagesList["WeaponInfo" + (num + 1).ToString()].gameObject.SetActive(true);
        }

        public void ShowPopUpText(Vector3 pos, string text)
        {
            Vector3 v = CameraControl.MainCameraControl.MyCamera.WorldToScreenPoint(pos);
            v.x = v.x / (float)Screen.width;
            v.y = v.y / (float)Screen.height;
            v.x = canv.sizeDelta.x * v.x - 0.5f * canv.sizeDelta.x;
            v.y = canv.sizeDelta.y * v.y - 0.5f * canv.sizeDelta.y;

            GameObject obj = Instantiate(PopupTextPrefab1);
            Image img = obj.GetComponent<Image>();
            img.rectTransform.SetParent(canv);
            img.rectTransform.localPosition = v;

            PopupTextPanel1 p = obj.GetComponent<PopupTextPanel1>();
            p.MyText.text = text;
        }
    }
}
