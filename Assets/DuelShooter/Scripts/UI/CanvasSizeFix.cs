using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class CanvasSizeFix : MonoBehaviour
    {

        // Use this for initialization

        void Awake()
        {
            RectTransform r = gameObject.GetComponent<RectTransform>();
            float ratio = (float)Screen.width / (float)Screen.height;
            r.sizeDelta = new Vector2(ratio * 300, 300);
        }
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}