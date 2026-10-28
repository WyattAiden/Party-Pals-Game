using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Aiming")]
    [SerializeField] private float turnSpeed = 720f;       // degrees per second
    [SerializeField] private float lookDeadzone = 0.2f;

    [Header("Shooting")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 0.5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    private bool isDashing;
    private float dashTimer;
    private float cooldownTimer;
    private Vector3 dashDirection;
    public bool IsDashing => isDashing;

    private CharacterController controller;
    public Vector2 MoveInput => moveInput;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private float nextFireTime;
    Vector3 knockbackVelocity;
    public float knockbackDecay = 8f;
   
    public void ApplyKnockBack(Vector3 force)
    {
        Debug.Log("Knockback applied: " + force);
        knockbackVelocity += force;
    }


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
        PlaytestLogger.Shot(gameObject);
    }


    public void Dash(InputAction.CallbackContext context)
    {
        Debug.Log("Dash Called");
        if (!context.performed) return;
        if (isDashing || cooldownTimer > 0f) return;

        Vector3 inputDir = new Vector3(MoveInput.x, 0f, MoveInput.y);

        dashDirection = inputDir.sqrMagnitude > 0.01f
           ? inputDir.normalized
           : transform.forward;

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
    }


    void Update()
    {
       
        // Left stick: move in world space (stick up = +Z), independent of facing
        Vector3 move = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
       

        // Right stick: face the direction it is pushed; keep last facing when released
        if (lookInput.sqrMagnitude > lookDeadzone * lookDeadzone)
        {
            Quaternion target = Quaternion.LookRotation(new Vector3(lookInput.x, 0f, lookInput.y));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        // Gravity (reset while grounded so it doesn't build up forever)
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        
        Vector3 total = (move * moveSpeed) + knockbackVelocity + new Vector3(0f, velocity.y, 0f);
        controller.Move(total * Time.deltaTime);

        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);

        // Dash stuff
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
}