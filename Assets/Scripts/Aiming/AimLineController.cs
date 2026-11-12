using UnityEngine;
using UnityEngine.InputSystem;

// Added to each player at runtime by CityAimLineSetup (CityLevel only).
// Hold ExtendAim (T / Right Bumper) to lengthen the FirePoint aim line,
// hold ShrinkAim (Y / Left Bumper) to shorten it. A reticle sits at the end of the line.
[DisallowMultipleComponent]
public class AimLineController : MonoBehaviour
{
    [Header("Length")]
    public float minLength = 1f;
    public float maxLength = 12f;
    public float startLength = 3f;
    [Tooltip("How fast the line grows/shrinks while the button is held (units per second)")]
    public float adjustSpeed = 8f;

    [Header("Walls")]
    [Tooltip("Stop the line (and reticle) at the first solid thing it hits")]
    public bool stopAtWalls = true;
    public LayerMask wallMask = ~0;

    [Header("Reticle")]
    public float reticleRadius = 0.35f;
    public float reticleLineWidth = 0.06f;
    [Range(8, 64)] public int reticleSegments = 32;
    [Tooltip("Colour the reticle with the player's colour from GameSettings")]
    public bool usePlayerColor = true;
    public Color reticleColor = Color.white;

    [Header("Input action names (Gameplay map)")]
    public string extendActionName = "ExtendAim";
    public string shrinkActionName = "ShrinkAim";

    public float CurrentLength => length;

    private LineRenderer line;
    private Transform firePoint;
    private InputAction extendAction;
    private InputAction shrinkAction;
    private float length;
    private float shownLength;
    private Vector3 originalEnd;
    private bool initialised;

    private GameObject reticle;
    private Material reticleMaterial;

    void Start()
    {
        var pc = GetComponent<PlayerController>();
        firePoint = pc != null ? pc.FirePoint : transform.Find("FirePoint");
        if (firePoint == null) { Debug.LogWarning("AimLineController: no FirePoint found.", this); enabled = false; return; }

        line = firePoint.GetComponent<LineRenderer>();
        if (line == null) { Debug.LogWarning("AimLineController: FirePoint has no LineRenderer.", this); enabled = false; return; }

        line.useWorldSpace = false;
        if (line.positionCount < 2) line.positionCount = 2;
        originalEnd = line.GetPosition(line.positionCount - 1);

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
        {
            extendAction = playerInput.actions.FindAction(extendActionName);
            shrinkAction = playerInput.actions.FindAction(shrinkActionName);
        }
        if (extendAction == null || shrinkAction == null)
            Debug.LogWarning($"AimLineController: couldn't find '{extendActionName}' / '{shrinkActionName}' actions.", this);

        length = Mathf.Clamp(startLength, minLength, maxLength);
        BuildReticle(playerInput);
        initialised = true;
    }

    void Update()
    {
        if (!initialised) return;

        float dir = 0f;
        if (extendAction != null && extendAction.IsPressed()) dir += 1f;
        if (shrinkAction != null && shrinkAction.IsPressed()) dir -= 1f;
        length = Mathf.Clamp(length + dir * adjustSpeed * Time.deltaTime, minLength, maxLength);
    }

    // LateUpdate so the line follows the player's rotation from this frame
    void LateUpdate()
    {
        if (!initialised) return;

        shownLength = stopAtWalls ? DistanceToWall(length) : length;

        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, new Vector3(0f, 0f, shownLength));

        bool visible = line.enabled && line.gameObject.activeInHierarchy;
        if (reticle.activeSelf != visible) reticle.SetActive(visible);
        reticle.transform.localPosition = new Vector3(0f, 0f, shownLength);
        reticle.transform.localRotation = Quaternion.identity;
    }

    private float DistanceToWall(float maxDistance)
    {
        var hits = Physics.RaycastAll(firePoint.position, firePoint.forward, maxDistance, wallMask, QueryTriggerInteraction.Ignore);
        float best = maxDistance;
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;                 // our own body
            if (hit.collider.GetComponentInParent<Projectile>() != null) continue; // bullets in flight
            if (hit.distance < best) best = hit.distance;
        }
        return Mathf.Max(best, 0.05f);
    }

    private void BuildReticle(PlayerInput playerInput)
    {
        Color color = reticleColor;
        if (usePlayerColor && playerInput != null)
        {
            color = GameSettings.GetColors(playerInput.playerIndex);
            color.a = 1f;
        }

        reticleMaterial = new Material(line.sharedMaterial);
        reticleMaterial.color = color;

        reticle = new GameObject("AimReticle");
        reticle.transform.SetParent(firePoint, false);

        // Ring
        var ring = MakeLine("Ring", reticleSegments, true, color);
        for (int i = 0; i < reticleSegments; i++)
        {
            float a = i / (float)reticleSegments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * reticleRadius, 0f, Mathf.Sin(a) * reticleRadius));
        }

        // Small cross in the middle for the exact aim point
        float c = reticleRadius * 0.45f;
        var crossA = MakeLine("CrossA", 2, false, color);
        crossA.SetPosition(0, new Vector3(-c, 0f, 0f));
        crossA.SetPosition(1, new Vector3(c, 0f, 0f));
        var crossB = MakeLine("CrossB", 2, false, color);
        crossB.SetPosition(0, new Vector3(0f, 0f, -c));
        crossB.SetPosition(1, new Vector3(0f, 0f, c));
    }

    private LineRenderer MakeLine(string name, int points, bool loop, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(reticle.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = points;
        lr.widthMultiplier = reticleLineWidth;
        lr.sharedMaterial = reticleMaterial;
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        return lr;
    }

    // Put the line back the way the prefab had it if this is removed
    void OnDestroy()
    {
        if (initialised && line != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, originalEnd);
        }
        if (reticle != null) Destroy(reticle);
        if (reticleMaterial != null) Destroy(reticleMaterial);
    }
}
