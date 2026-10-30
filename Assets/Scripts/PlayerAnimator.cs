using UnityEngine;

/// <summary>
/// Drives the puppet's Animator from PlayerController (twin-stick: movement is
/// converted into the character's local space so it walks / strafes / backpedals
/// relative to where it's aiming).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Blend-tree value used while sprinting (normal move = 1)")]
    [SerializeField] private float sprintBlend = 1.6f;
    [SerializeField] private float dampTime = 0.1f;

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveZ = Animator.StringToHash("MoveZ");
    static readonly int Fire = Animator.StringToHash("Fire");
    static readonly int Reload = Animator.StringToHash("Reload");

    private PlayerController player;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void OnEnable() { if (player != null) player.Shot += OnShot; }
    void OnDisable() { if (player != null) player.Shot -= OnShot; }

    void Update()
    {
        if (animator == null) return;
        Vector2 input = Vector2.ClampMagnitude(player.MoveInput, 1f);
        Vector3 local = transform.InverseTransformDirection(new Vector3(input.x, 0f, input.y));
        float scale = player.IsRunning ? sprintBlend : 1f;
        animator.SetFloat(MoveX, local.x * scale, dampTime, Time.deltaTime);
        animator.SetFloat(MoveZ, local.z * scale, dampTime, Time.deltaTime);
    }

    void OnShot() { if (animator != null) animator.SetTrigger(Fire); }

    /// <summary>Call this when the player reloads (e.g. power-weapon ammo).</summary>
    public void PlayReload() { if (animator != null) animator.SetTrigger(Reload); }
}
