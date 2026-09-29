using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class SpawnTrail1 : MonoBehaviour
    {
        [HideInInspector]
        public Vector3 InitPos;
        [HideInInspector]
        public Vector3 TargetPos;

        float Lerp;

        public AnimationCurve MoveCurve;
        // Use this for initialization
        void Start()
        {
            InitPos = transform.position;
            Lerp = 0;
        }

        // Update is called once per frame
        void Update()
        {
            Lerp += 0.6f * Time.deltaTime;
            if (Lerp >= 1)
            {
                Destroy(gameObject, .8f);
            }

            Vector3 pos = Vector3.Lerp(InitPos, TargetPos, Lerp);
            pos.y = MoveCurve.Evaluate(Lerp) * 20;
            transform.position = pos;
        }
    }
}