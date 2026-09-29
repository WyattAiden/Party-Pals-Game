using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class BulletTrail1 : MonoBehaviour
    {

        public Transform MyBullet;
        public Vector3 InitPosition;
        Vector3 FinalPos;
        float scale = 0.4f;
        void Awake()
        {
            scale = 1;
        }
        // Use this for initialization
        void Start()
        {
            InitPosition = transform.position;
            //Destroy(gameObject, 2);
        }

        // Update is called once per frame
        void Update()
        {
            scale -= Time.deltaTime;
            scale = Mathf.Clamp(scale, 0, 1);

            if (MyBullet != null)
            {
                FinalPos = MyBullet.position;
            }

            Vector3 pos = FinalPos + InitPosition;
            transform.position = pos / 2f;

            pos = FinalPos - InitPosition;
            transform.rotation = Quaternion.LookRotation(pos) * Quaternion.Euler(90, 0, 0);

            transform.localScale = new Vector3(.3f * scale, pos.magnitude, 1);

        }
    }
}