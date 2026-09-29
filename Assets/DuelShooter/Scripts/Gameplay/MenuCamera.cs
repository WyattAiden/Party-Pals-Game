using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class MenuCamera : MonoBehaviour
    {

        [HideInInspector]
        public float HandShakeArc;
        [HideInInspector]
        public Vector3 InitPosition;
        // Use this for initialization
        void Start()
        {
            InitPosition = transform.position;
        }

        // Update is called once per frame
        void Update()
        {

            HandShakeArc += .5f * Time.deltaTime;
            float shakeSin2 = Mathf.Cos(HandShakeArc);
            float shakeCos2 = Mathf.Sin(3 * HandShakeArc);

            transform.position = InitPosition + new Vector3(1 * shakeSin2, 0, .5f * shakeCos2);
        }
    }
}