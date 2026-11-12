using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Water Village hazard: every so often a big wave rises at one side of the platform,
// sweeps across it, and carries any player it catches off the edge into the water
// (the Water object's DeathPlane does the killing).
//
// The wave is a generated mesh made of thin slices. Water that hits a building rides up and spills
// over the roof, piles up at the corners, slopes around the sides and leaves a shallow wake behind,
// instead of clipping through.
// Players standing behind a building (in that gap) are sheltered.
//
// Setup: put this on an empty GameObject with a child "Wave Visual" that has a MeshFilter and a
// MeshRenderer with two materials: [0] water, [1] foam. No collider. The mesh is built at runtime.
public class TidalWave : MonoBehaviour
{
    public enum WaveSide { FromBack, FromFront, Random }

    [Header("Timing (seconds)")]
    [SerializeField] private float firstWaveDelay = 15f;   // grace period at round start
    [SerializeField] private float minInterval = 12f;
    [SerializeField] private float maxInterval = 25f;
    [SerializeField] private float warningTime = 2.5f;     // wave rises out of the sea before it moves
    [SerializeField] private float sinkTime = 1f;

    [Header("Wave movement")]
    [SerializeField] private WaveSide side = WaveSide.FromBack;
    [Tooltip("World Z just past the BACK edge of the platform (far from the camera).")]
    [SerializeField] private float backEdgeZ = 16f;
    [Tooltip("World Z just past the FRONT edge of the platform (closest to the camera).")]
    [SerializeField] private float frontEdgeZ = -21f;
    [SerializeField] private float waveSpeed = 14f;        // faster than a running player (8)
    [Tooltip("How deep the 'grab' zone is behind the front of the wave.")]
    [SerializeField] private float waveThickness = 5f;
    [SerializeField] private float pushStrength = 1.1f;    // 1 = carried at wave speed

    [Header("Shelter")]
    [SerializeField] private bool buildingsGiveShelter = true;
    [Tooltip("How far behind a building players (and the gap in the water) stay protected.")]
    [SerializeField] private float shelterCheckDistance = 10f;
    [SerializeField] private LayerMask shelterLayers = ~0;

    [Header("Wave shape")]
    [SerializeField] private Transform waveVisual;         // needs MeshFilter + MeshRenderer (water, foam)
    [SerializeField] private float waveCenterX = 2.5f;
    [SerializeField] private float waveLength = 110f;      // side to side
    [SerializeField] private float waveHeight = 12f;
    [SerializeField] private float waveDepth = 5f;         // front to back
    [SerializeField] private float sliceWidth = 0.5f;      // smaller = smoother parting, more work
    [SerializeField] private float hiddenY = -14f;         // wave centre height when under the sea
    [SerializeField] private float raisedY = -1.5f;        // wave centre height when up
    [Tooltip("Lowest point (world Y) checked for buildings, kept above the floor.")]
    [SerializeField] private float buildingCheckBottom = -5.5f;
    [SerializeField] private float buildingCheckTop = 5f;
    [SerializeField] private float dropSpeed = 10f;        // how quickly the water reshapes when it hits a building
    [SerializeField] private float refillSpeed = 4f;       // how quickly water flows back in behind it

    [Header("Flow around buildings")]
    [Tooltip("Off: the wave keeps its full height over and behind buildings (just foams where it hits).\nOn: it dips over roofs and leaves a shallow wake behind buildings.")]
    [SerializeField] private bool lowerAroundBuildings = false;
    [Tooltip("How thick the sheet of water spilling over a roof is.")]
    [SerializeField] private float overtopDepth = 0.8f;
    [Tooltip("World Y of the shallow water left right behind a building (floor is about -6.1).")]
    [SerializeField] private float wakeWaterY = -5.6f;
    [Tooltip("How steeply the water slopes around the sides of a building (height fraction per metre). Lower = wider, gentler curve.")]
    [SerializeField] private float sideSlope = 0.18f;
    [Tooltip("Blur passes over the wave shape. More = rounder curves.")]
    [SerializeField] private int smoothPasses = 4;
    [Tooltip("Extra height where water piles up against a building's corners.")]
    [SerializeField] private float pileUp = 0.12f;

    [Header("Warning banner (optional)")]
    [SerializeField] private GameObject warningBanner;      // shown while the wave rises
    [SerializeField] private TMP_Text warningText;          // blinks inside the banner
    [SerializeField, TextArea] private string warningMessage = "TSUNAMI WARNING!\n<size=60%>Get to shelter behind a building!</size>";

    [Header("Optional")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningSound;
    [SerializeField] private AudioClip crashSound;

    [Header("Don't send waves while these are showing")]
    [SerializeField] private GameObject[] pauseWhileActive; // e.g. RoundScreen, WinScreen

    public int waveCount;           // handy for testing
    public bool IsWaveActive { get; private set; }

    private float frontZ;           // current Z of the wave's leading face
    private Vector3 dir = Vector3.back; // travel direction (+Z or -Z)

    // Mesh state
    private Mesh mesh;
    private float[] fill;           // 0..1 height of each slice edge
    private float[] target, shaped;
    private bool[] crash, foam;
    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<int> waterTris = new List<int>();
    private readonly List<int> foamTris = new List<int>();
    private readonly Collider[] overlap = new Collider[16];

    void Awake()
    {
        int samples = Mathf.Max(2, Mathf.CeilToInt(waveLength / sliceWidth) + 1);
        fill = new float[samples];
        target = new float[samples]; shaped = new float[samples];
        crash = new bool[samples]; foam = new bool[samples];
        ResetFill();

        if (waveVisual != null)
        {
            mesh = new Mesh { name = "Tidal Wave Mesh" };
            mesh.MarkDynamic();
            var mf = waveVisual.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = mesh;
            waveVisual.rotation = Quaternion.identity;
            waveVisual.localScale = Vector3.one;
        }
    }

    void Start()
    {
        frontZ = backEdgeZ;
        SetVisual(hiddenY, backEdgeZ);
        RebuildMesh();
        ShowWarning(false);
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstWaveDelay);
        while (true)
        {
            while (IsPaused() || IsWaveActive) yield return null;
            yield return StartCoroutine(DoWave());
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    // Can also be called from other scripts / a debug button
    public void TriggerWaveNow()
    {
        if (!IsWaveActive) StartCoroutine(DoWave());
    }

    IEnumerator DoWave()
    {
        IsWaveActive = true;
        waveCount++;

        bool fromBack = side == WaveSide.FromBack || (side == WaveSide.Random && Random.value < 0.5f);
        float startZ = fromBack ? backEdgeZ : frontEdgeZ;
        float endZ = fromBack ? frontEdgeZ : backEdgeZ;
        dir = fromBack ? Vector3.back : Vector3.forward;
        frontZ = startZ;
        ResetFill();

        // 1) Warning: the wave rises out of the water at the starting side
        if (warningText != null && !string.IsNullOrEmpty(warningMessage)) warningText.text = warningMessage;
        ShowWarning(true);
        Play(warningSound);
        for (float t = 0f; t < warningTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / warningTime);
            SetVisual(Mathf.Lerp(hiddenY, raisedY, k), startZ);
            UpdateShape();
            if (warningText != null) warningText.enabled = Mathf.Repeat(t, 0.4f) < 0.25f; // blink
            yield return null;
        }
        ShowWarning(false);
        Play(crashSound);

        // 2) Sweep across the platform, carrying players with it
        float distance = Mathf.Abs(endZ - startZ) + waveDepth;
        for (float travelled = 0f; travelled < distance; )
        {
            float step = waveSpeed * Time.deltaTime;
            travelled += step;
            frontZ += dir.z * step;
            SetVisual(raisedY, frontZ);
            UpdateShape();
            PushPlayers();
            yield return null;
        }

        // 3) Sink back into the water
        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            frontZ += dir.z * waveSpeed * Time.deltaTime;
            SetVisual(Mathf.Lerp(raisedY, hiddenY, t / sinkTime), frontZ);
            UpdateShape();
            PushPlayers();
            yield return null;
        }
        SetVisual(hiddenY, startZ);
        IsWaveActive = false;
    }

    void PushPlayers()
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.TryGetComponent(out Health h) && h.IsDead) continue;

            // How far the player is behind the wave's leading face (positive = inside the wave)
            float behindFront = (frontZ - player.transform.position.z) * dir.z;
            if (behindFront < -0.5f || behindFront > waveThickness) continue;
            if (buildingsGiveShelter && IsSheltered(player)) continue;

            // PlayerController.knockbackVelocity decays by knockbackDecay per second, so adding
            // speed * decay * dt every frame holds the player at roughly 'speed'.
            float target = waveSpeed * pushStrength;
            player.ApplyKnockBack(dir * target * player.knockbackDecay * Time.deltaTime);
        }
    }

    // Sheltered if something solid (a building) sits between the player and the oncoming wave.
    // Checks three rays across the player's width so a doorway doesn't let the wave straight through.
    bool IsSheltered(PlayerController player)
    {
        Vector3 sideways = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 centre = player.transform.position + Vector3.up * 1f;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 origin = centre + sideways * (0.8f * i);
            var hits = Physics.RaycastAll(origin, -dir, shelterCheckDistance, shelterLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<PlayerController>() != null) continue; // other players don't count
                return true;
            }
        }
        return false;
    }

    // ---------- Wave shape ----------

    void ResetFill()
    {
        if (fill == null) return;
        for (int i = 0; i < fill.Length; i++) { fill[i] = 1f; crash[i] = false; foam[i] = false; }
    }

    // Work out how each slice of water meets the buildings, ease the heights, then rebuild the mesh.
    //  - Slices hitting a building ride up the wall and spill over the roof in a thin foamy sheet.
    //  - Slices next to a building pile up a little at the corners.
    //  - Slices that have passed a building leave a shallow wake that refills the further behind it they are.
    //  - The edges of every gap slope off over a few slices so the water wraps around the sides.
    void UpdateShape()
    {
        if (waveVisual == null) return;

        int count = fill.Length;
        float x0 = waveCenterX - waveLength * 0.5f;
        float bottomY = waveVisual.position.y - waveHeight * 0.5f;
        float yCentre = (buildingCheckBottom + buildingCheckTop) * 0.5f;
        float halfY = (buildingCheckTop - buildingCheckBottom) * 0.5f;
        float ahead = 1.5f;
        float lee = buildingsGiveShelter ? shelterCheckDistance : 6f;

        // Region the wave body covers right now (plus a little ahead so it reacts before contact)
        float zA = frontZ + dir.z * ahead;
        float zB = frontZ - dir.z * waveDepth;
        float bodyCentreZ = (zA + zB) * 0.5f;
        float bodyHalfZ = Mathf.Abs(zA - zB) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x = x0 + i * sliceWidth;
            crash[i] = false;
            target[i] = 1f;

            // 1) Is this slice running into a building? If so, flow over its roof.
            int n = Physics.OverlapBoxNonAlloc(new Vector3(x, yCentre, bodyCentreZ),
                new Vector3(sliceWidth * 0.5f, halfY, bodyHalfZ), overlap, Quaternion.identity,
                shelterLayers, QueryTriggerInteraction.Ignore);
            float roof = float.MinValue;
            for (int j = 0; j < n; j++)
            {
                var c = overlap[j];
                if (!IsBuilding(c)) continue;
                roof = Mathf.Max(roof, c.bounds.max.y);
            }
            if (roof > float.MinValue)
            {
                crash[i] = true;
                if (lowerAroundBuildings) target[i] = Mathf.Clamp01((roof + overtopDepth - bottomY) / waveHeight);
                continue;
            }
            if (!lowerAroundBuildings) continue;

            // 2) Has the front already passed a building? Shallow wake that refills with distance.
            int h = Physics.BoxCastNonAlloc(new Vector3(x, yCentre, frontZ),
                new Vector3(sliceWidth * 0.45f, halfY, 0.05f), -dir, castHits, Quaternion.identity,
                lee, shelterLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int j = 0; j < h; j++)
                if (IsBuilding(castHits[j].collider)) nearest = Mathf.Min(nearest, castHits[j].distance);
            if (nearest < float.MaxValue)
            {
                float wakeFill = Mathf.Clamp01((wakeWaterY - bottomY) / waveHeight);
                float t = Mathf.Clamp01(nearest / lee);
                target[i] = Mathf.Lerp(wakeFill, 1f, t * t);   // stays low close behind, closes in further back
            }
        }

        // 3) Slope the sides of every dip so the water wraps around instead of having sheer walls
        float slopePerSlice = sideSlope * sliceWidth;
        for (int i = 0; i < count; i++) shaped[i] = target[i];
        for (int i = 1; i < count; i++) shaped[i] = Mathf.Min(shaped[i], shaped[i - 1] + slopePerSlice);
        for (int i = count - 2; i >= 0; i--) shaped[i] = Mathf.Min(shaped[i], shaped[i + 1] + slopePerSlice);

        // 4) Water piles up against the corners of buildings it is hitting
        int pileSlices = Mathf.Max(1, Mathf.RoundToInt(1.5f / sliceWidth));
        for (int i = 0; i < count; i++)
        {
            foam[i] = crash[i];
            if (crash[i] || !lowerAroundBuildings) continue;
            for (int k = 1; k <= pileSlices; k++)
            {
                bool nextToCrash = (i - k >= 0 && crash[i - k]) || (i + k < count && crash[i + k]);
                if (!nextToCrash) continue;
                shaped[i] = Mathf.Max(shaped[i], 1f + pileUp * (1f - (k - 1f) / pileSlices));
                foam[i] = true;
                break;
            }
        }

        // 5) Blur so every bend is a smooth curve rather than a kink
        for (int pass = 0; pass < smoothPasses; pass++)
        {
            for (int i = 0; i < count; i++) target[i] = shaped[i];
            for (int i = 1; i < count - 1; i++) shaped[i] = target[i - 1] * 0.25f + target[i] * 0.5f + target[i + 1] * 0.25f;
        }

        // 6) Ease toward the new shape (reacts fast when it hits, flows back in more gently)
        float dt = Time.deltaTime;
        for (int i = 0; i < count; i++)
        {
            float rate = shaped[i] < fill[i] ? dropSpeed : refillSpeed;
            fill[i] = Mathf.Lerp(fill[i], shaped[i], 1f - Mathf.Exp(-rate * dt));
        }

        RebuildMesh();
    }

    bool IsBuilding(Collider c)
    {
        if (c == null) return false;
        if (c.attachedRigidbody != null) return false;                        // projectiles, props
        if (c.GetComponentInParent<PlayerController>() != null) return false; // players
        return true;
    }

    // Cross-section of the wave, from the bottom of the back (u=0) over the crest to the bottom of the
    // leading face (u=1). v is height as a fraction of the slice's current fill.
    static readonly Vector2[] Profile =
    {
        new Vector2(0.00f, 0.00f), new Vector2(0.04f, 0.30f), new Vector2(0.15f, 0.60f), new Vector2(0.32f, 0.84f),
        new Vector2(0.50f, 0.96f), new Vector2(0.66f, 1.00f), new Vector2(0.80f, 0.98f), new Vector2(0.91f, 0.88f),
        new Vector2(0.98f, 0.68f), new Vector2(1.00f, 0.40f), new Vector2(1.00f, 0.00f)
    };
    const int CrestFrom = 6;   // profile points from here up to the lip get foam
    const int CrestTo = 8;

    void RebuildMesh()
    {
        if (mesh == null) return;
        verts.Clear(); waterTris.Clear(); foamTris.Clear();

        int count = fill.Length, rows = Profile.Length;
        float x0 = waveCenterX - waveLength * 0.5f;
        float yb = -waveHeight * 0.5f;                    // bottom (local)
        float lead = dir.z < 0f ? -1f : 1f;               // which local Z side is the front
        bool flip = lead < 0f;                            // keep faces pointing outward either way

        for (int i = 0; i < count; i++)
        {
            float x = x0 + i * sliceWidth;
            float f = Mathf.Max(0f, fill[i]);
            for (int k = 0; k < rows; k++)
            {
                float z = (-0.5f + Profile[k].x) * waveDepth * lead;
                verts.Add(new Vector3(x, yb + waveHeight * f * Profile[k].y, z));
            }
        }

        for (int i = 0; i < count - 1; i++)
        {
            if (fill[i] < 0.01f && fill[i + 1] < 0.01f) continue;  // flat under the sea: nothing to draw
            bool splashing = foam[i] || foam[i + 1];
            for (int k = 0; k < rows - 1; k++)
            {
                int a0 = i * rows + k, a1 = a0 + 1, b0 = a0 + rows, b1 = b0 + 1;
                bool isFoam = (k >= CrestFrom && k < CrestTo) || (splashing && k >= 4 && k < CrestTo);
                var tris = isFoam ? foamTris : waterTris;
                if (flip) { tris.Add(a0); tris.Add(b1); tris.Add(a1); tris.Add(a0); tris.Add(b0); tris.Add(b1); }
                else      { tris.Add(a0); tris.Add(a1); tris.Add(b1); tris.Add(a0); tris.Add(b1); tris.Add(b0); }
            }
        }

        // Caps on the two far ends so the wave isn't hollow
        Cap(0, -1f, rows);
        Cap(count - 1, 1f, rows);

        mesh.Clear();
        mesh.subMeshCount = 2;
        mesh.SetVertices(verts);
        mesh.SetTriangles(waterTris, 0);
        mesh.SetTriangles(foamTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Fan of triangles across the profile at one end; winding chosen so the face points outward (±X)
    void Cap(int slice, float outward, int rows)
    {
        int first = verts.Count;
        for (int k = 0; k < rows; k++) verts.Add(verts[slice * rows + k]);   // own verts: hard edge at the end
        for (int k = 1; k < rows - 1; k++)
        {
            int a = first, b = first + k, c = first + k + 1;
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (n.x * outward >= 0f) { waterTris.Add(a); waterTris.Add(b); waterTris.Add(c); }
            else                     { waterTris.Add(a); waterTris.Add(c); waterTris.Add(b); }
        }
    }

    void SetVisual(float y, float leadingZ)
    {
        if (waveVisual == null) return;
        // Mesh is centred, so shift it back by half its depth from the leading face
        waveVisual.position = new Vector3(0f, y, leadingZ - dir.z * waveDepth * 0.5f);
    }

    void ShowWarning(bool show)
    {
        if (warningBanner != null) warningBanner.SetActive(show);
        else if (warningText != null) warningText.gameObject.SetActive(show);
        if (warningText != null) warningText.enabled = true;
    }

    bool IsPaused()
    {
        if (pauseWhileActive == null) return false;
        foreach (var go in pauseWhileActive)
            if (go != null && go.activeInHierarchy) return true;
        return false;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        float x0 = waveCenterX - waveLength * 0.5f, x1 = waveCenterX + waveLength * 0.5f;
        Gizmos.DrawLine(new Vector3(x0, raisedY, backEdgeZ), new Vector3(x1, raisedY, backEdgeZ));
        Gizmos.DrawLine(new Vector3(x0, raisedY, frontEdgeZ), new Vector3(x1, raisedY, frontEdgeZ));
    }
}
