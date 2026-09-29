using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace DuelShooter
{
    public class MenuControl : MonoBehaviour
    {

        //public GameObject MainCamera;
        public Menu[] Menus;

        public Dictionary<string, Menu> MenuList;
        public Dictionary<string, Menu> OpenMenuList;
        //public MainMenu mainMenu;
        //public TeamSelectMenu teamSelectMenu;

        public static MenuControl GeneralMenuControl;
        public static Camera EventCamera;

        public bool AutoFindMenus = false;

        [HideInInspector]
        public string NextMenu = "MainMenu";

        public Stack<Menu> MenuStack = new Stack<Menu>();

        void Awake()
        {
            GeneralMenuControl = this;
            MenuList = new Dictionary<string, Menu>();
            OpenMenuList = new Dictionary<string, Menu>();

            for (int i = 0; i < Menus.Length; i++)
            {
                //Menus[i].gameObject.SetActive(true);
                MenuList.Add(Menus[i].gameObject.name, Menus[i]);
            }

            //EventCamera = GameObject.FindGameObjectWithTag("EventCamera").GetComponent<Camera>();
        }
        // Use this for initialization
        void Start()
        {

        }

        void Update()
        {


        }

        public Menu ShowMenu(string menuName)
        {
            //MenuControl.GeneralMenuControl.MenuList[menuName].Show();
            GameObject p = (GameObject)GameObject.Instantiate(MenuList[menuName].gameObject);
            p.name = menuName;
            p.transform.parent = transform;
            p.transform.localScale = Vector3.one;
            p.transform.localPosition = Vector3.zero;

            //print("Show Menu - " + menuName);
            //if (OpenMenuList.ContainsKey(menuName))
            //{
            //    GameObject m = OpenMenuList[menuName].gameObject;
            //    OpenMenuList.Remove(menuName);
            //    Destroy(m);
            //}

            //OpenMenuList.Add(menuName, p.GetComponent<Menu>());
            return (p.GetComponent<Menu>());
        }

        public void CloseMenu(string menuName)
        {
            if (OpenMenuList.ContainsKey(menuName))
            {
                GameObject m = OpenMenuList[menuName].gameObject;
                OpenMenuList.Remove(menuName);
                Destroy(m);
            }
        }

        public virtual void UpdateMenuList()
        {
            MenuList = new Dictionary<string, Menu>();
            for (int i = 0; i < Menus.Length; i++)
            {
                if (!MenuList.ContainsKey(Menus[i].gameObject.name))
                {
                    MenuList.Add(Menus[i].gameObject.name, Menus[i]);
                }
            }
        }

        void OnValidate()
        {
            if (AutoFindMenus)
            {
                int num = 0;
                Menu[] m = new Menu[100];
                Object[] objs = Resources.FindObjectsOfTypeAll(typeof(GameObject));
                for (int i = 0; i < objs.Length; i++)
                {
                    GameObject go = (GameObject)objs[i];
                    if (go.tag == "Menu" && go.activeSelf && go.transform.parent == null)
                    {
                        m[num++] = go.GetComponent<Menu>();
                    }
                }

                Menus = new Menu[num];
                for (int i = 0; i < num; i++)
                {
                    Menus[i] = m[i];
                }

                UpdateMenuList();

            }
            //    Menu[] ms = gameObject.GetComponentsInChildren<Menu>(true);
            //    Menus = ms;
            //    UpdateMenuList();
            //}
            //else
            //{
            //    Menus = new Menu[1];
            //    MenuList = new Dictionary<string, Menu>();
            //}
        }

    }
}
