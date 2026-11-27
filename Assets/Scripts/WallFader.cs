using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Cuts a dithered see-through "tunnel" through anything between the camera and a player.
/// Only the part of the mesh in the line of sight fades, so one-mesh levels
/// (e.g. the space station) stay solid everywhere else.
/// Auto-attaches to Camera.main in every scene; no setup needed.
/// Only affects renderers using the Built-in "Standard" shader.
/// </summary>
[DisallowMultipleComponent]
public class WallFader : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Fraction of pixels kept inside the see-through area (0 = fully open hole).")]
    [Range(0f, 1f)] public float fadedAlpha = 0.25f;
    [Tooltip("Radius of the see-through area around each player's line of sight (world units).")]
    public float holeRadius = 3f;
    [Tooltip("Surfaces closer than this to the player (along the camera line) never fade, so the floor under them stays solid.")]
    public float playerMargin = 1.5f;
    [Tooltip("How fast the hole opens/closes (per second).")]
    public float fadeSpeed = 6f;

    [Header("Detection")]
    [Tooltip("Radius of the sphere cast from the camera to each player.")]
    public float castRadius = 0.4f;
    [Tooltip("Objects with this tag never fade (leave empty to disable).")]
    public string neverFadeTag = "";
    [Tooltip("Ignore colliders with more child renderers than this.")]
    public int maxChildRenderers = 8;

    const string VariantPath = "WallFadeVariant";
    const string ShaderName = "PartyPals/WallFadeStandard";
    const int MaxPlayers = 4;

    static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");
    static readonly int PosId = Shader.PropertyToID("_WallFadePos");
    static readonly int CountId = Shader.PropertyToID("_WallFadeCount");
    static readonly int RadiusId = Shader.PropertyToID("_WallFadeRadius");
    static readonly int MarginId = Shader.PropertyToID("_WallFadeMargin");
    static readonly int AlphaId = Shader.PropertyToID("_WallFadeAlpha");

    class FadeEntry
    {
        public Renderer renderer;
        public Material[] originalShared;
        public Material[] fadeMats;
        public float amount;
        public bool blocking;
    }

    readonly Dictionary<Renderer, FadeEntry> entries = new Dictionary<Renderer, FadeEntry>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly List<Renderer> toRemove = new List<Renderer>();
    readonly List<Renderer> childBuffer = new List<Renderer>();
    readonly Vector4[] targets = new Vector4[MaxPlayers];
    Camera cam;
    int mask;
    static Material variantRef;
    static Shader fadeShader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // The Resources material keeps the shader included in builds.
        if (variantRef == null) variantRef = Resources.Load<Material>(VariantPath);
        fadeShader = variantRef != null ? variantRef.shader : Shader.Find(ShaderName);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToMainCamera();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => AttachToMainCamera();

    static void AttachToMainCamera()
    {
        var c = Camera.main;
        if (c != null && c.GetComponent<WallFader>() == null) c.gameObject.AddComponent<WallFader>();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        int playerLayer = LayerMask.NameToLayer("Player");
        mask = Physics.DefaultRaycastLayers;
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
    }

    void LateUpdate()
    {
        if (cam == null || fadeShader == null) return;

        foreach (var e in entries.Values) e.blocking = false;

        Vector3 camPos = cam.transform.position;
        int count = 0;
        foreach (var pi in PlayerInput.all)
        {
            if (pi == null || !pi.isActiveAndEnabled) continue;
            Vector3 center, head;
            GetTargets(pi, out center, out head);
            CastTo(camPos, center);
            CastTo(camPos, head);
            if (count < MaxPlayers) targets[count++] = Vector3.Lerp(center, head, 0.5f);
        }
        for (int i = count; i < MaxPlayers; i++) targets[i] = Vector4.zero;

        Shader.SetGlobalVectorArray(PosId, targets);
        Shader.SetGlobalFloat(CountId, count);
        Shader.SetGlobalFloat(RadiusId, holeRadius);
        Shader.SetGlobalFloat(MarginId, playerMargin);
        Shader.SetGlobalFloat(AlphaId, fadedAlpha);

        float step = fadeSpeed * Time.deltaTime;
        toRemove.Clear();
        foreach (var kv in entries)
        {
            var e = kv.Value;
            if (e.renderer == null) { DestroyMats(e); toRemove.Add(kv.Key); continue; }
            e.amount = Mathf.MoveTowards(e.amount, e.blocking ? 1f : 0f, step);
            for (int i = 0; i < e.fadeMats.Length; i++)
                if (e.fadeMats[i] != null && e.fadeMats[i] != e.originalShared[i])
                    e.fadeMats[i].SetFloat(FadeAmountId, e.amount);
            if (!e.blocking && e.amount <= 0f)
            {
                Restore(e);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var r in toRemove) entries.Remove(r);
    }

    void GetTargets(PlayerInput pi, out Vector3 center, out Vector3 head)
    {
        Collider body = pi.GetComponentInChildren<CharacterController>();
        if (body == null) body = pi.GetComponentInChildren<Collider>();

        if (body != null)
        {
            var b = body.bounds;
            center = b.center;
            head = b.center + Vector3.up * (0.8f * b.extents.y);
        }
        else
        {
            center = pi.transform.position + Vector3.up;
            head = center + Vector3.up * 0.8f;
        }
    }

    void CastTo(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float full = dir.magnitude;
        float dist = full - castRadius;
        if (dist <= 0f) return;
        dir /= full;

        int n = Physics.SphereCastNonAlloc(from, castRadius, dir, hits, dist, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = hits[i].collider;
            if (col == null) continue;
            if (col.GetComponentInParent<PlayerInput>() != null) continue;
            if (!string.IsNullOrEmpty(neverFadeTag) && col.CompareTag(neverFadeTag)) continue;

            var r = col.GetComponent<Renderer>();
            if (r != null) { MarkBlocking(r); continue; }

            childBuffer.Clear();
            col.GetComponentsInChildren(childBuffer);
            if (childBuffer.Count == 0 || childBuffer.Count > maxChildRenderers) continue;
            foreach (var cr in childBuffer) MarkBlocking(cr);
        }
    }

    void MarkBlocking(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) return;
        if (r.GetComponentInParent<PlayerInput>() != null) return;

        FadeEntry e;
        if (!entries.TryGetValue(r, out e))
        {
            var shared = r.sharedMaterials;
            bool any = false;
            var fade = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                var m = shared[i];
                if (m != null && m.shader != null && m.shader.name == "Standard")
                {
                    var f = new Material(m) { name = m.name + " (WallFade)" };
                    int queue = m.renderQueue;
                    f.shader = fadeShader;           // keeps matching properties (_Color, _MainTex, ...)
                    f.renderQueue = queue < 2450 ? -1 : queue;
                    f.SetFloat(FadeAmountId, 0f);
                    fade[i] = f;
                    any = true;
                }
                else fade[i] = m;
            }
            if (!any) return;

            e = new FadeEntry { renderer = r, originalShared = shared, fadeMats = fade };
            r.sharedMaterials = fade;
            entries[r] = e;
        }
        e.blocking = true;
    }

    void Restore(FadeEntry e)
    {
        if (e.renderer != null) e.renderer.sharedMaterials = e.originalShared;
        DestroyMats(e);
    }

    void DestroyMats(FadeEntry e)
    {
        for (int i = 0; i < e.fadeMats.Length; i++)
        {
            var m = e.fadeMats[i];
            if (m != null && m != e.originalShared[i]) Destroy(m);
        }
    }

    void OnDisable()
    {
        foreach (var e in entries.Values) Restore(e);
        entries.Clear();
        Shader.SetGlobalFloat(CountId, 0);
    }
}
