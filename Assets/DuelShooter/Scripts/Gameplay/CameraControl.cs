using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class CameraControl : MonoBehaviour
    {

        // Use this for initialization
        [HideInInspector]
        public float ShakeTimer;
        [HideInInspector]
        public float ShakeArc;
        [HideInInspector]
        public float ShakeRadius = 1;
        [HideInInspector]
        public Vector3 InitPosition;
        [HideInInspector]
        public Vector3 InitDir;

        float FinalDistance;
        Vector3 FinalTargetPos;

        public static CameraControl MainCameraControl;

        [HideInInspector]
        public float HandShakeArc;

        [HideInInspector]
        public Camera MyCamera;
        void Awake()
        {
            MainCameraControl = this;
            MyCamera = GetComponent<Camera>();
        }
        void Start()
        {
            InitPosition = transform.position;
            InitDir = new Vector3(0, 2, -1);
            InitDir.Normalize();
            MyCamera.fieldOfView = 50;
        }

        // Update is called once per frame
        void Update()
        {
            Vector3 targetPos = Vector3.zero;
            Vector3 finalPos = InitPosition;
            float CamDis = 50;

            PlayerControl[] players = GameControl.MainGameControl.PlayerControls.ToArray();
            if (players[0].MyPlayerChar != null && players[1].MyPlayerChar != null)
            {
                Vector3 pos = players[0].MyPlayerChar.transform.position + players[1].MyPlayerChar.transform.position;
                pos = 0.5f * pos;
                Vector3 dir = players[0].MyPlayerChar.transform.position - players[1].MyPlayerChar.transform.position;
                float dis = dir.magnitude;

                CamDis = Mathf.Lerp(30, 60, dis / 50f);
                targetPos = pos;
            }


            ShakeTimer -= Time.deltaTime;
            ShakeArc += 40 * Time.deltaTime;
            //ShakeTimer = 1;
            //ShakeArc += 1 * Time.deltaTime;
            if (ShakeTimer <= 0)
                ShakeTimer = 0;

            float shakeSin = Mathf.Cos(ShakeArc) * Mathf.Clamp(ShakeTimer, 0, 0.5f);
            float shakeCos = Mathf.Sin(3 * ShakeArc) * Mathf.Clamp(ShakeTimer, 0, 0.5f);

            HandShakeArc += .3f * Time.deltaTime;
            float shakeSin2 = Mathf.Cos(HandShakeArc);
            float shakeCos2 = Mathf.Sin(3 * HandShakeArc);

            FinalDistance = CamDis;
            FinalTargetPos = Vector3.Lerp(FinalTargetPos, targetPos, 2 * Time.deltaTime);
            transform.position = FinalTargetPos + FinalDistance * InitDir;
            transform.forward = FinalTargetPos - transform.position;
            transform.position += new Vector3(ShakeRadius * shakeSin, 0, 0.5f * ShakeRadius * shakeCos);


        }

        public void StartShake(float t, float r)
        {
            if (ShakeTimer == 0 || ShakeRadius < r)
                ShakeRadius = r;

            ShakeTimer = t;
        }
    }
}