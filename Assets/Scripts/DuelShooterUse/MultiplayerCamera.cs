using UnityEngine;
using UnityEngine.InputSystem;

// Adapted from DuelShooter's CameraControl (which only handled 2 players).
// Put on the Main Camera. Follows the middle of all alive players and pulls back
// as they spread apart, keeping the camera angle you set in the scene.
// Call MultiplayerCamera.Shake(duration, strength) from any script for screen shake.
[RequireComponent(typeof(Camera))]
public class MultiplayerCamera : MonoBehaviour
{
    public static MultiplayerCamera Instance { get; private set; }

    [Header("Framing")]
    [Tooltip("Distance from the players when they are bunched together.")]
    [SerializeField] private float minDistance = 15f;
    [Tooltip("Distance from the players when they are far apart.")]
    [SerializeField] private float maxDistance = 35f;
    [Tooltip("How far apart players must be (in units) for the camera to reach Max Distance.")]
    [SerializeField] private float spreadForMaxDistance = 30f;
    [SerializeField] private float followSpeed = 2f;

    [Header("Shake")]
    [SerializeField] private float shakeFrequency = 25f;

    private Vector3 viewDir;   // from the focus point back to the camera
    private Vector3 focus;
    private float distance;

    private float shakeTime;
    private float shakeDuration = 0.01f;
    private float shakeStrength;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        viewDir = -transform.forward;

        // Start from the point on the ground (y = 0) the camera is already looking at,
        // so nothing jumps on the first frame.
        if (transform.forward.y < -0.01f)
        {
            float t = -transform.position.y / transform.forward.y;
            focus = transform.position + transform.forward * t;
        }
        else
        {
            focus = transform.position + transform.forward * minDistance;
        }
        distance = Vector3.Distance(transform.position, focus);
    }

    void LateUpdate()
    {
        if (TryGetPlayerBounds(out Bounds bounds))
        {
            float spread = Mathf.Max(bounds.size.x, bounds.size.z);
            float targetDistance = Mathf.Lerp(minDistance, maxDistance, spread / spreadForMaxDistance);

            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            focus = Vector3.Lerp(focus, bounds.center, t);
            distance = Mathf.Lerp(distance, targetDistance, t);
        }

        Vector3 pos = focus + viewDir * distance;

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            float amount = shakeStrength * Mathf.Clamp01(shakeTime / shakeDuration);
            float s = Time.time * shakeFrequency;
            Vector3 offset = transform.right * (Mathf.PerlinNoise(s, 0f) - 0.5f)
                           + transform.up * (Mathf.PerlinNoise(0f, s) - 0.5f);
            pos += offset * 2f * amount;
        }

        transform.position = pos;
    }

    // Bounds around every player that has joined and is still alive.
    private bool TryGetPlayerBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (PlayerInput player in PlayerInput.all)
        {
            if (player == null || !player.gameObject.activeInHierarchy) continue;
            if (player.TryGetComponent(out Health health) && health.IsDead) continue;

            if (!any)
            {
                bounds = new Bounds(player.transform.position, Vector3.zero);
                any = true;
            }
            else
            {
                bounds.Encapsulate(player.transform.position);
            }
        }
        return any;
    }

    public static void Shake(float duration, float strength)
    {
        if (Instance != null) Instance.StartShake(duration, strength);
    }

    public void StartShake(float duration, float strength)
    {
        // Don't let a small shake cut short a bigger one that's still going
        float remaining = shakeTime > 0f ? shakeStrength * (shakeTime / shakeDuration) : 0f;
        if (strength < remaining) return;

        shakeStrength = strength;
        shakeDuration = Mathf.Max(duration, 0.01f);
        shakeTime = shakeDuration;
    }
}
