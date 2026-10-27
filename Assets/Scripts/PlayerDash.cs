using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerController))]
public class PlayerDash : MonoBehaviour
{
    

    [Header("Dash Settings")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;


    private CharacterController controller;
    private PlayerController player;
    private bool isDashing;
    private float dashTimer;
    private float cooldownTimer;
    private Vector3 dashDirection;
    public bool IsDashing => isDashing;

    public void Awake()
    {
        controller = GetComponent<CharacterController>();
        player = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);

            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
    }

    public void Dash(InputAction.CallbackContext context)
    {
        Debug.Log("Dash Called");
        if (!context.performed) return;
        if (isDashing || cooldownTimer > 0f) return;

        Vector3 inputDir = new Vector3(player.MoveInput.x, 0f, player.MoveInput.y);

        dashDirection = inputDir.sqrMagnitude > 0.01f
           ? inputDir.normalized
           : transform.forward;

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
    }
}
