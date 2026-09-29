using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
namespace DuelShooter
{
    public class Menu : MonoBehaviour
    {

        public Image[] Images;
        public Dictionary<string, Image> ImagesList = new Dictionary<string, Image>();


        public Button[] UIButtons;
        public Dictionary<string, Button> UIButtonsList = new Dictionary<string, Button>();

        [HideInInspector]
        public Menu ParentMenu;

        public bool AutoFindButtons = false;

        int Counter = 1;

        void Awake()
        {
            //UpdateButtonList();
            UpdateImageList();
        }

        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }


        public virtual void UpdateImageList()
        {
            Counter = 1;
            ImagesList = new Dictionary<string, Image>();
            for (int i = 0; i < Images.Length; i++)
            {
                if (ImagesList.ContainsKey(Images[i].gameObject.name))
                {
                    //Images[i].gameObject.name = Images[i].gameObject.name + Counter.ToString();
                    //Counter++;
                }
                else
                {
                    ImagesList.Add(Images[i].gameObject.name, Images[i]);
                }
                //Images[i].MotherMenu = this;
            }
        }


        public virtual void Show()
        {
            gameObject.SetActive(true);
            //ResetAllButtons();

            //if (audio != null && MenuShowSound1 != null)
            //{
            //    audio.PlayOneShot(MenuShowSound1);
            //}
        }

        public void Close()
        {
            MenuControl.GeneralMenuControl.OpenMenuList.Remove(name);
            Destroy(gameObject);
            //print(name);
        }


        public virtual void Disable()
        {
            gameObject.SetActive(false);
        }

        void OnGUI()
        {
            GUI.depth = 15;
        }

        public virtual void ArrangeButtons()
        {
            if (AutoFindButtons)
            {
                Images = gameObject.GetComponentsInChildren<Image>(true);
                UpdateImageList();
            }
            else
            {
                Images = new Image[1];
                ImagesList = new Dictionary<string, Image>();
            }
        }



        void OnValidate()
        {
            ArrangeButtons();
        }


    }
}