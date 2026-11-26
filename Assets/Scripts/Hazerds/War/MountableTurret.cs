using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A simple turret a player can climb onto.
/// Walk up to it and press Interact to mount, press Interact again to get off.
/// While mounted: aim (right stick, or move keys) turns the turret, hold Shoot to fire.
/// Put this on the turret's root object together with a Health component.
/// Shots that hit any of the turret's colliders damage it; at 0 health it is disabled
/// for the rest of the round (rider gets thrown off, it goes dark and can't be used).
/// </summary>
[RequireComponent(typeof(Health))]
public class MountableTurret : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The part that turns (the barrel should be a child of it).")]
    [SerializeField] private Transform turretHead;
    [Tooltip("Where shots come out. Its blue (forward) arrow should point down the barrel.")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Where the rider sits while mounted.")]
    [SerializeField] private Transform seat;

    [Header("Mounting")]
    [Tooltip("How close (flat distance from the turret head) a player has to be to climb on.")]
    [SerializeField] private float mountRange = 5f;
    [Tooltip("How far from the turret the rider is dropped when they get off.")]
    [SerializeField] private float dismountDistance = 4.5f;
    [Tooltip("Name of the input action used to get on and off.")]
    [SerializeField] private string interactAction = "Interact";

    [Header("Shooting")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Seconds between shots while Shoot is held.")]
    [SerializeField] private float fireCooldown = 0.15f;
    [Tooltip("How fast the turret turns to follow your aim, in degrees per second.")]
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float aimDeadzone = 0.2f;

    [Header("Aim line (only shown while someone is riding)")]
    [Tooltip("Line drawn out of the barrel while mounted. Uses world space, so it isn't stretched by the turret's scale.")]
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private float aimLineLength = 3f;
    [Tooltip("Hide the rider's own aim line while they're seated so there aren't two lines.")]
    [SerializeField] private bool hideRiderAimLine = true;

    [Header("Disabled")]
    [Tooltip("How dark the turret goes when disabled (0 = black, 1 = unchanged).")]
    [Range(0f, 1f)] [SerializeField] private float disabledBrightness = 0.3f;
    [Tooltip("Hidden when the turret is disabled (e.g. its health bar).")]
    [SerializeField] private GameObject hideWhenDisabled;
    [Tooltip("The barrel model. It tips toward the ground when the turret is disabled.")]
    [SerializeField] private Transform barrel;
    [Tooltip("How far the barrel tips down when disabled, in degrees.")]
    [SerializeField] private float barrelDroopAngle = 12f;
    [Tooltip("How long the barrel takes to tip down, in seconds.")]
    [SerializeField] private float barrelDroopTime = 0.35f;

    public bool IsDisabled => health != null && health.IsDead;
    public PlayerInput Rider => rider;

    private Health health;
    private PlayerInput rider;
    private PlayerController riderController;
    private CharacterController riderCC;
    private Health riderHealth;
    private LineRenderer riderAimLine;
    private bool riderAimLineWasOn;
    private float nextFireTime;
    private Quaternion headOffset; // turns "aim direction" into the head rotation that points the barrel there
    private readonly Dictionary<PlayerInput, bool> interactWasDown = new Dictionary<PlayerInput, bool>();
    private readonly HashSet<PlayerInput> interactJustPressed = new HashSet<PlayerInput>();

    void Awake()
    {
        health = GetComponent<Health>();
        if (aimLine != null)
        {
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    void OnEnable() => health.Died += OnDisabled;
    void OnDisable()
    {
        health.Died -= OnDisabled;
        Dismount();
    }

    void Start()
    {
        // Work out how the barrel sits relative to the head, so any model orientation works
        Vector3 barrelDir = Flat(firePoint.forward);
        headOffset = Quaternion.Inverse(Quaternion.LookRotation(barrelDir)) * turretHead.rotation;
    }

    void Update()
    {
        if (IsDisabled) return;
        TrackInteractPresses();

        if (rider == null)
        {
            CheckForMount();
            return;
        }

        // Rider died or left the game
        if (rider == null || !rider.isActiveAndEnabled || (riderHealth != null && riderHealth.IsDead))
        {
            Dismount();
            return;
        }

        if (interactJustPressed.Contains(rider))
        {
            Dismount();
            return;
        }

        Aim();
        HoldRiderInSeat();

        InputAction shoot = rider.actions.FindAction("Shoot");
        if (shoot != null && shoot.IsPressed() && Time.time >= nextFireTime)
            Fire();
    }

    // ---------------- Mounting ----------------

    private void CheckForMount()
    {
        foreach (PlayerInput p in PlayerInput.all)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            if (!interactJustPressed.Contains(p)) continue;
            if (p.TryGetComponent(out Health h) && h.IsDead) continue;
            if (Flat(p.transform.position - turretHead.position).magnitude > mountRange) continue;
            if (IsRidingAnotherTurret(p)) continue;

            Mount(p);
            return;
        }
    }

    private void Mount(PlayerInput p)
    {
        rider = p;
        riderController = p.GetComponent<PlayerController>();
        riderCC = p.GetComponent<CharacterController>();
        riderHealth = p.GetComponent<Health>();

        // Turn off walking/normal shooting while seated
        if (riderController != null) riderController.enabled = false;
        if (riderCC != null) riderCC.enabled = false;

        nextFireTime = Time.time + 0.2f; // don't fire on the same press that mounted
        HoldRiderInSeat();

        // Swap the rider's aim line for the turret's
        riderAimLine = (riderController != null && riderController.FirePoint != null)
            ? riderController.FirePoint.GetComponent<LineRenderer>() : null;
        if (hideRiderAimLine && riderAimLine != null)
        {
            riderAimLineWasOn = riderAimLine.enabled;
            riderAimLine.enabled = false;
        }
        if (aimLine != null) aimLine.enabled = true;
        UpdateAimLine();
    }

    private void Dismount()
    {
        if (rider == null) return;

        PlayerInput p = rider;
        rider = null;
        if (aimLine != null) aimLine.enabled = false;
        if (p == null) return;

        bool dead = riderHealth != null && riderHealth.IsDead;
        if (hideRiderAimLine && riderAimLine != null && !dead) riderAimLine.enabled = riderAimLineWasOn;
        riderAimLine = null;
        if (!dead)
        {
            p.transform.position = FindDropSpot(p);
            p.transform.rotation = Quaternion.LookRotation(Flat(firePoint.forward));
            Physics.SyncTransforms();
            if (riderCC != null) riderCC.enabled = true;
            if (riderController != null) riderController.enabled = true;
        }

        riderController = null;
        riderCC = null;
        riderHealth = null;
    }

    private bool IsRidingAnotherTurret(PlayerInput p)
    {
        foreach (var t in FindObjectsByType<MountableTurret>(FindObjectsSortMode.None))
            if (t != this && t.Rider == p) return true;
        return false;
    }

    // Drop the rider behind the turret (or to the side if that's blocked)
    private Vector3 FindDropSpot(PlayerInput p)
    {
        Vector3 back = -Flat(firePoint.forward);
        Vector3[] dirs = { back, Quaternion.Euler(0, 90, 0) * back, Quaternion.Euler(0, -90, 0) * back, -back };
        float halfHeight = riderCC != null ? riderCC.height * 0.5f : 1f;
        float radius = riderCC != null ? riderCC.radius : 0.5f;

        foreach (Vector3 d in dirs)
        {
            Vector3 spot = turretHead.position + d * dismountDistance;
            if (!Physics.Raycast(spot + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            Vector3 pos = hit.point + Vector3.up * (halfHeight + 0.05f);
            Vector3 a = pos + Vector3.up * (halfHeight - radius);
            Vector3 b = pos - Vector3.up * (halfHeight - radius);
            bool blocked = false;
            foreach (Collider c in Physics.OverlapCapsule(a, b, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(p.transform)) { blocked = true; break; }
            if (!blocked) return pos;
        }
        return turretHead.position + back * dismountDistance; // fallback
    }

    // ---------------- Riding ----------------

    private void Aim()
    {
        Vector2 stick = Read(rider, "Look");
        if (stick.sqrMagnitude < aimDeadzone * aimDeadzone) stick = Read(rider, "Move"); // keyboard players aim with move keys
        if (stick.sqrMagnitude < aimDeadzone * aimDeadzone) return;

        Vector3 dir = CameraRelative(stick);
        Quaternion target = Quaternion.LookRotation(dir) * headOffset;
        turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, target, turnSpeed * Time.deltaTime);
    }

    // LateUpdate so the line follows this frame's turning
    void LateUpdate()
    {
        if (rider != null) UpdateAimLine();
    }

    private void UpdateAimLine()
    {
        if (aimLine == null) return;
        aimLine.SetPosition(0, firePoint.position);
        aimLine.SetPosition(1, firePoint.position + firePoint.forward * aimLineLength);
    }

    private void HoldRiderInSeat()
    {
        rider.transform.SetPositionAndRotation(seat.position, Quaternion.LookRotation(Flat(firePoint.forward)));
    }

    private void Fire()
    {
        nextFireTime = Time.time + fireCooldown;
        if (projectilePrefab == null) return;
        Projectile proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.owner = rider.gameObject; // kills/score credit go to the rider
    }

    // ---------------- Disabled ----------------

    private void OnDisabled()
    {
        Dismount();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            foreach (Material m in r.materials) // runtime copies, the shared material is untouched
                if (m.HasProperty("_Color")) m.color = m.color * disabledBrightness;
        }
        if (hideWhenDisabled != null) hideWhenDisabled.SetActive(false);
        if (barrel != null) StartCoroutine(DroopBarrel());
    }

    // Tips the barrel down around its back end (where it meets the turret).
    // The turret top is stretched unevenly, so tilting the barrel while it's still a child of it
    // would skew it. Instead it's moved onto an unscaled hinge first (it looks exactly the same).
    private System.Collections.IEnumerator DroopBarrel()
    {
        Vector3 dir = Flat(firePoint.forward);

        // Half the barrel's length along the way it points
        float halfLength = 0f;
        foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            halfLength = Mathf.Max(halfLength, Mathf.Abs(Vector3.Dot(barrel.TransformVector(axis * 0.5f), dir)));

        Transform hinge = new GameObject("BarrelHinge").transform;
        hinge.SetParent(transform, false);
        hinge.position = barrel.position - dir * halfLength;
        hinge.rotation = Quaternion.identity;
        barrel.SetParent(hinge, true);

        Vector3 tiltAxis = Vector3.Cross(Vector3.up, dir); // positive angle around this points the barrel down
        float t = 0f;
        while (t < 1f)
        {
            t = barrelDroopTime > 0f ? Mathf.Min(1f, t + Time.deltaTime / barrelDroopTime) : 1f;
            float eased = 1f - (1f - t) * (1f - t); // fast start, gentle stop
            hinge.rotation = Quaternion.AngleAxis(barrelDroopAngle * eased, tiltAxis);
            yield return null;
        }
    }

    // ---------------- Helpers ----------------

    // Works out which players pressed Interact since last frame (button went from up to down)
    private void TrackInteractPresses()
    {
        interactJustPressed.Clear();
        foreach (PlayerInput p in PlayerInput.all)
        {
            if (p == null) continue;
            InputAction a = p.actions != null ? p.actions.FindAction(interactAction) : null;
            bool down = a != null && a.IsPressed();
            interactWasDown.TryGetValue(p, out bool wasDown);
            if (down && !wasDown) interactJustPressed.Add(p);
            interactWasDown[p] = down;
        }
    }

    private static Vector2 Read(PlayerInput p, string action)
    {
        InputAction a = p.actions != null ? p.actions.FindAction(action) : null;
        return a != null ? a.ReadValue<Vector2>() : Vector2.zero;
    }

    private static Vector3 CameraRelative(Vector2 stick)
    {
        Vector3 dir = new Vector3(stick.x, 0f, stick.y);
        Camera cam = Camera.main;
        if (cam == null) return dir.normalized;
        return (Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * dir).normalized;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    void OnDrawGizmosSelected()
    {
        if (turretHead == null) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(turretHead.position, mountRange);
    }
}
