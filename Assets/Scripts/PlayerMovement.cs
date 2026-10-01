using System.Globalization;
using UnityEngine;
using Unity.Netcode;


public class PlayerMovement : NetworkBehaviour
{

    public float speed = 5f;

    private void Start()
    {
        if (IsOwner)
        {
            GetComponent<Renderer>().material.color = Color.green;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        transform.Translate(direction * speed * Time.deltaTime);
    }
}
