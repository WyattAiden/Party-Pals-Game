using System.Collections.Generic;
using UnityEngine;

// Drives a boat along a smooth, looping route made of waypoint Transforms,
// then parks it back at the dock for a moment before setting off again.
// Players standing on the deck are carried along with the boat.
//
// Setup: put the waypoints (in order) under a separate GameObject (NOT under the
// boat, or they'd move with it). The first waypoint should be the dock spot.
[DefaultExecutionOrder(-100)] // move before players so riders follow cleanly
public class BoatPathMover : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("Waypoints in driving order. Waypoint 0 is the dock, where the boat waits. The route loops back to it.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("How many smooth samples are made between each pair of waypoints.")]
    [SerializeField] private int samplesPerSegment = 16;

    [Header("Driving")]
    [Tooltip("Top speed in units per second.")]
    [SerializeField] private float speed = 8f;

    [Tooltip("Distance used to speed up after leaving the dock and slow down when arriving.")]
    [SerializeField] private float easeDistance = 8f;

    [Tooltip("Slowest speed while easing in/out, so the boat never fully stalls mid-route.")]
    [SerializeField] private float minSpeed = 1.5f;

    [Tooltip("Seconds the boat waits at the dock before each lap.")]
    [SerializeField] private float dockWaitTime = 4f;

    [Tooltip("Seconds before the very first lap starts.")]
    [SerializeField] private float firstDepartureDelay = 4f;

    [Tooltip("Which local axis of the boat model is the front (bow). This boat's bow is +X.")]
    [SerializeField] private Vector3 bowAxis = Vector3.right;

    [Header("Bobbing")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField] private float rollDegrees = 2f;

    [Header("Riders")]
    [Tooltip("Carry players and weapon pickups on the deck along with the boat.")]
    [SerializeField] private bool carryRiders = true;

    [Tooltip("How far above the deck to look for riders.")]
    [SerializeField] private float riderCheckHeight = 1.5f;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<float> distances = new List<float>();
    private float totalLength;

    private float traveled;
    private float waitTimer;
    private Quaternion modelOffset;
    private Bounds localBounds;
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly HashSet<CharacterController> riders = new HashSet<CharacterController>();
    private readonly HashSet<WeaponPickup> pickups = new HashSet<WeaponPickup>();

    private void Awake()
    {
        modelOffset = Quaternion.Inverse(Quaternion.LookRotation(bowAxis.normalized, Vector3.up));

        var mf = GetComponent<MeshFilter>();
        localBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);

        // A kinematic Rigidbody makes a moving collider behave properly with physics.
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;

        BuildPath();
        waitTimer = firstDepartureDelay;
        if (totalLength > 0f) ApplyPose(0f, 0f);
    }

    private void Update()
    {
        if (totalLength <= 0f) return;

        Vector3 oldPos = transform.position;
        Quaternion oldRot = transform.rotation;
        if (carryRiders) FindRiders();

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
        }
        else
        {
            float distToEnd = totalLength - traveled;
            float ease = Mathf.Clamp01(Mathf.Min(traveled, distToEnd) / Mathf.Max(0.01f, easeDistance));
            float currentSpeed = Mathf.Lerp(minSpeed, speed, Mathf.SmoothStep(0f, 1f, ease));
            traveled += currentSpeed * Time.deltaTime;

            if (traveled >= totalLength)
            {
                traveled = 0f;            // back at the dock
                waitTimer = dockWaitTime;
            }
        }

        ApplyPose(traveled, Time.time);

        if (carryRiders) MoveRiders(oldPos, oldRot);
    }

    // Position + face the boat at a distance along the route, with a little bob.
    private void ApplyPose(float distance, float time)
    {
        Vector3 pos = SampleAt(distance, out Vector3 tangent);
        pos.y += Mathf.Sin(time * bobSpeed) * bobHeight;

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f) tangent = transform.rotation * bowAxis;

        Quaternion facing = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        Quaternion roll = Quaternion.AngleAxis(Mathf.Sin(time * bobSpeed * 0.8f) * rollDegrees, Vector3.forward);

        transform.SetPositionAndRotation(pos, facing * roll * modelOffset);
    }

    // Find every CharacterController standing on the deck (before the boat moves).
    private void FindRiders()
    {
        Vector3 s = transform.lossyScale;
        Vector3 halfExtents = new Vector3(localBounds.extents.x * s.x, riderCheckHeight * 0.5f, localBounds.extents.z * s.z);
        Vector3 deckTop = transform.TransformPoint(new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z));
        Vector3 center = deckTop + Vector3.up * (riderCheckHeight * 0.5f);

        Quaternion flatRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, flatRot, ~0, QueryTriggerInteraction.Collide);

        riders.Clear();
        pickups.Clear();
        for (int i = 0; i < count; i++)
        {
            var hit = overlapBuffer[i];
            var pickup = hit.GetComponentInParent<WeaponPickup>();
            if (pickup != null) { pickups.Add(pickup); continue; } // weapon pickups on the deck
            if (hit.isTrigger) continue;
            var cc = hit.GetComponentInParent<CharacterController>();
            if (cc != null) riders.Add(cc);
        }
    }

    // Shift riders by however the boat moved/turned this frame.
    private void MoveRiders(Vector3 oldPos, Quaternion oldRot)
    {
        Quaternion deltaRot = transform.rotation * Quaternion.Inverse(oldRot);
        foreach (var cc in riders)
        {
            if (!cc.enabled) continue;
            Vector3 p = cc.transform.position;
            Vector3 target = transform.position + deltaRot * (p - oldPos);
            Vector3 delta = target - p;
            delta.y = 0f; // let the player's own gravity handle height
            cc.Move(delta);
        }

        foreach (var pickup in pickups)
            if (pickup != null) pickup.CarryWith(oldPos, transform.position, deltaRot);
    }

    private void BuildPath()
    {
        points.Clear();
        distances.Clear();
        totalLength = 0f;

        var wp = new List<Vector3>();
        foreach (var t in waypoints) if (t != null) wp.Add(t.position);
        if (wp.Count < 2) return;

        int n = wp.Count;
        int steps = Mathf.Max(2, samplesPerSegment);
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = wp[(i - 1 + n) % n], p1 = wp[i], p2 = wp[(i + 1) % n], p3 = wp[(i + 2) % n];
            for (int k = 0; k < steps; k++)
                points.Add(CatmullRom(p0, p1, p2, p3, k / (float)steps));
        }
        points.Add(wp[0]); // close the loop at the dock

        distances.Add(0f);
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
            distances.Add(totalLength);
        }
    }

    private Vector3 SampleAt(float distance, out Vector3 tangent)
    {
        distance = Mathf.Clamp(distance, 0f, totalLength);
        int i = 1;
        while (i < distances.Count - 1 && distances[i] < distance) i++;

        float segLen = distances[i] - distances[i - 1];
        float t = segLen > 0f ? (distance - distances[i - 1]) / segLen : 0f;
        tangent = points[i] - points[i - 1];
        return Vector3.Lerp(points[i - 1], points[i], t);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // Draw the route in the Scene view so it's easy to tweak.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) BuildPath();
        if (points.Count < 2) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f);
        for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1], points[i]);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Count; i++)
            if (waypoints[i] != null) Gizmos.DrawWireSphere(waypoints[i].position, i == 0 ? 0.8f : 0.4f);
    }
}
