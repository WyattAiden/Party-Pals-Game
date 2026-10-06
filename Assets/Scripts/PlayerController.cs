using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Aiming")]
    [SerializeField] private float turnSpeed = 720f;       // degrees per second
    [SerializeField] private float lookDeadzone = 0.2f;

    [Header("Shooting")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 0.5f;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
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

    public void Look(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + fireCooldown;
        Projectile proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.owner = gameObject;
    }

    void Update()
    {
        // Left stick: move in world space (stick up = +Z), independent of facing
        Vector3 move = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Right stick: face the direction it is pushed; keep last facing when released
        if (lookInput.sqrMagnitude > lookDeadzone * lookDeadzone)
        {
            Quaternion target = Quaternion.LookRotation(new Vector3(lookInput.x, 0f, lookInput.y));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        // Gravity (reset while grounded so it doesn't build up forever)
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}