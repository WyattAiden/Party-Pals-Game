using System.Collections.Generic;
using UnityEngine;

// Rocks the ship like it is on stormy water, and carries players standing on the deck with it.
// Put this on the "Ship" object (the parent of all ship pieces). It adds/needs a kinematic Rigidbody
// so the ship's colliders move properly. Only runs in Play mode.
[RequireComponent(typeof(Rigidbody))]
public class ShipRocking : MonoBehaviour
{
    [Header("On/off and overall strength")]
    public bool rockingEnabled = true;
    [Range(0f, 3f)] public float stormStrength = 1f;

    [Header("Motion (degrees, units, cycles per second)")]
    public float rollDegrees = 2f;      // tilt around the ship's long axis (X)
    public float pitchDegrees = 1f;     // bow-up / bow-down tilt (around Z)
    public float heaveUnits = 0.12f;    // up-and-down bob
    public float rollSpeed = 0.23f;
    public float pitchSpeed = 0.17f;
    public float heaveSpeed = 0.31f;
    [Range(0f, 1f)] public float roughness = 0.5f;   // 0 = smooth regular rocking, 1 = very irregular

    [Header("Carrying players")]
    public float rideCheckDistance = 2f;   // how far below a player to look for the deck

    Rigidbody rb;
    Vector3 basePosition, lastPosition;
    Quaternion baseRotation, lastRotation;
    float seed;
    float nextRefresh;
    readonly List<CharacterController> riders = new List<CharacterController>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        basePosition = lastPosition = transform.position;
        baseRotation = lastRotation = transform.rotation;
        seed = Random.value * 100f;
    }

    // A mix of a regular sine wave and smooth noise, between -1 and 1.
    float Wave(float speed, float offset)
    {
        float t = Time.time;
        float sine = Mathf.Sin(t * speed * Mathf.PI * 2f + offset);
        float noise = Mathf.PerlinNoise(seed + offset, t * speed * 0.7f) * 2f - 1f;
        return Mathf.Lerp(sine, noise, roughness * 0.5f);
    }

    void FixedUpdate()
    {
        float k = rockingEnabled ? stormStrength : 0f;
        float roll = Wave(rollSpeed, 0f) * rollDegrees * k;
        float pitch = Wave(pitchSpeed, 1.7f) * pitchDegrees * k;
        float heave = Wave(heaveSpeed, 3.1f) * heaveUnits * k;

        Quaternion newRotation = baseRotation * Quaternion.Euler(roll, 0f, pitch);
        Vector3 newPosition = basePosition + new Vector3(0f, heave, 0f);

        CarryRiders(newPosition, newRotation);

        rb.MovePosition(newPosition);
        rb.MoveRotation(newRotation);
        lastPosition = newPosition;
        lastRotation = newRotation;
    }

    // Moves anyone standing on the ship by the same amount the ship is about to move.
    void CarryRiders(Vector3 newPosition, Quaternion newRotation)
    {
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 1f;
            riders.Clear();
            riders.AddRange(FindObjectsByType<CharacterController>(FindObjectsSortMode.None));
            // A CharacterController ignores moves smaller than minMoveDistance (default 0.001).
            // The ship moves less than that each step, so allow tiny moves or riders would never be carried.
            foreach (var c in riders) c.minMoveDistance = 0f;
        }

        Quaternion delta = newRotation * Quaternion.Inverse(lastRotation);
        foreach (var cc in riders)
        {
            if (cc == null || !cc.enabled) continue;
            Vector3 p = cc.transform.position;
            if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit,
                    rideCheckDistance, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.transform.IsChildOf(transform)) continue;

            Vector3 moved = newPosition + delta * (p - lastPosition);
            cc.Move(moved - p);
        }
    }
}
