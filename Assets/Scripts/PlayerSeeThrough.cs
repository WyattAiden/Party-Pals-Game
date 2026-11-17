using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Put this on one object in a level. Every player that spawns gets a
// see-through silhouette in their own colour, shown only when they are
// behind a wall, pillar, cart or anything else.
public class PlayerSeeThrough : MonoBehaviour
{
    [Header("Shaders (leave these set)")]
    [SerializeField] private Shader maskShader;
    [SerializeField] private Shader fillShader;

    [Header("Look")]
    [Tooltip("How see-through the silhouette is. 0 = invisible, 1 = solid.")]
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.45f;

    [Tooltip("How bright the glowing edge around the silhouette is.")]
    [Range(0f, 2f)]
    [SerializeField] private float edgeGlow = 0.6f;

    [Tooltip("How often (seconds) to look for newly spawned players.")]
    [SerializeField] private float checkInterval = 0.25f;

    private readonly HashSet<PlayerInput> donePlayers = new HashSet<PlayerInput>();
    private readonly List<Material> createdMaterials = new List<Material>();
    private Material maskMaterial;
    private float nextCheck;

    private void Awake()
    {
        if (maskShader == null) maskShader = Shader.Find("PartyPals/SilhouetteMask");
        if (fillShader == null) fillShader = Shader.Find("PartyPals/SilhouetteFill");

        if (maskShader == null || fillShader == null)
        {
            Debug.LogWarning("PlayerSeeThrough: silhouette shaders are missing, effect turned off.");
            enabled = false;
            return;
        }

        maskMaterial = new Material(maskShader) { name = "Player Silhouette Mask" };
        createdMaterials.Add(maskMaterial);
    }

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        foreach (PlayerInput player in PlayerInput.all)
        {
            if (player == null || donePlayers.Contains(player)) continue;
            AddSilhouette(player);
            donePlayers.Add(player);
        }
    }

    private void AddSilhouette(PlayerInput player)
    {
        Color color = GameSettings.GetColors(player.playerIndex);
        color.a = opacity;

        Material fill = new Material(fillShader) { name = "Player Silhouette " + player.playerIndex };
        fill.SetColor("_Color", color);
        fill.SetFloat("_RimBoost", edgeGlow);
        createdMaterials.Add(fill);

        foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
        {
            // Only the body parts, not the aim line
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;

            Material[] current = r.materials;
            Material[] withSilhouette = new Material[current.Length + 2];
            current.CopyTo(withSilhouette, 0);
            withSilhouette[current.Length] = maskMaterial;
            withSilhouette[current.Length + 1] = fill;
            r.materials = withSilhouette;
        }
    }

    private void OnDestroy()
    {
        foreach (Material m in createdMaterials)
            if (m != null) Destroy(m);
    }
}
