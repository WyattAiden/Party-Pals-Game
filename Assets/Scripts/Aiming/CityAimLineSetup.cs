using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lives in the CityLevel scene only. Gives every player that spawns here an
// adjustable aim line + reticle (AimLineController). Other levels are untouched.
public class CityAimLineSetup : MonoBehaviour
{
    [Header("Length")]
    [SerializeField] private float minLength = 1f;
    [SerializeField] private float maxLength = 12f;
    [SerializeField] private float startLength = 3f;
    [SerializeField] private float adjustSpeed = 8f;

    [Header("Walls")]
    [SerializeField] private bool stopAtWalls = true;
    [SerializeField] private LayerMask wallMask = ~0;

    [Header("Reticle")]
    [SerializeField] private float reticleRadius = 0.35f;
    [SerializeField] private float reticleLineWidth = 0.06f;
    [SerializeField] private bool usePlayerColor = true;
    [SerializeField] private Color reticleColor = Color.white;

    private readonly List<AimLineController> added = new List<AimLineController>();

    void OnEnable()
    {
        PlayerInputManagerV2.OnPlayerJoined += AddTo;
    }

    void OnDisable()
    {
        PlayerInputManagerV2.OnPlayerJoined -= AddTo;
    }

    void Start()
    {
        // Catch any players that already exist (e.g. spawned before this was enabled)
        foreach (var p in PlayerInput.all)
            if (p.GetComponent<PlayerController>() != null) AddTo(p);
    }

    private void AddTo(PlayerInput player)
    {
        if (player == null || player.GetComponent<AimLineController>() != null) return;

        var aim = player.gameObject.AddComponent<AimLineController>();
        added.Add(aim);
        aim.minLength = minLength;
        aim.maxLength = maxLength;
        aim.startLength = startLength;
        aim.adjustSpeed = adjustSpeed;
        aim.stopAtWalls = stopAtWalls;
        aim.wallMask = wallMask;
        aim.reticleRadius = reticleRadius;
        aim.reticleLineWidth = reticleLineWidth;
        aim.usePlayerColor = usePlayerColor;
        aim.reticleColor = reticleColor;
    }

    // If players ever survive into another scene, take the feature off them
    void OnDestroy()
    {
        foreach (var aim in added)
            if (aim != null) Destroy(aim);
        added.Clear();
    }
}
