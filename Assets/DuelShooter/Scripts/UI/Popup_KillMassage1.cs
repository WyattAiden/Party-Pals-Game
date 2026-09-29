using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class Popup_KillMassage1 : Menu
    {

        public Text KillerText;
        public Text TargetText;

        public RectTransform panel;
        public RectTransform canv;

        public PlayerControl Killer;
        public PlayerControl Target;
        public Vector3 pos;
        // Use this for initialization
        void Start()
        {
            //Invoke("Close", 2);
            UpdateMessage();
        }

        // Update is called once per frame
        void Update()
        {
            panel.transform.localPosition += Time.deltaTime * new Vector3(0, 20, 0);
        }

        public void UpdateMessage()
        {
            KillerText.text = Killer.PlayerName;
            TargetText.text = Target.PlayerName;

            KillerText.color = Killer.PlayerUIColor;
            TargetText.color = Target.PlayerUIColor;

            Vector3 v = CameraControl.MainCameraControl.MyCamera.WorldToScreenPoint(pos);
            v.x = v.x / Screen.width;
            v.y = v.y / Screen.height;
            v.x = canv.sizeDelta.x * v.x - 0.5f * canv.sizeDelta.x;
            v.y = canv.sizeDelta.y * v.y - 0.5f * canv.sizeDelta.y;


            panel.localPosition = v;
        }
    }
}