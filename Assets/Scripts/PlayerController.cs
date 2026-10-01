using DuelShooter;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Shooting")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 0.5f;


    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private float nextFireTime;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }


    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + fireCooldown;
        Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
    }

    void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        controller.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

}
