using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class Shadow : MonoBehaviour
    {
        Transform parent;
        Vector3 Scale;
        // Use this for initialization
        void Start()
        {
            parent = transform.parent;
            transform.parent = null;
            Scale = transform.localScale;
        }

        // Update is called once per frame
        void Update()
        {
            if (parent != null)
            {
                if (parent.position.y >= -.2f)
                {
                    Vector3 pos = parent.position;
                    pos.y = .1f;
                    transform.position = pos;
                    transform.rotation = Quaternion.Euler(90, 0, 0);

                    float delta = Mathf.Abs(transform.position.y - parent.position.y);
                    float lerp = 1 - Mathf.Clamp(delta, 0, 80) / 80f;
                    transform.localScale = lerp * Scale;
                }
                else
                {
                    transform.position = new Vector3(0, -100, 0);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}