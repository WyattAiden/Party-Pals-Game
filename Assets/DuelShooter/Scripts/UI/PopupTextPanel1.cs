using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DuelShooter
{
    public class PopupTextPanel1 : MonoBehaviour
    {

        public Text MyText;
        // Use this for initialization
        void Start()
        {
            Invoke("Close", 2);
        }

        // Update is called once per frame
        void Update()
        {
            transform.position += Time.deltaTime * new Vector3(0, 10, 0);
        }

        public void Close()
        {
            Destroy(gameObject);
        }
    }
}