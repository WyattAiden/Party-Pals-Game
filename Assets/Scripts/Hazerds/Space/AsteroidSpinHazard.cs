using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// SPACE STATION HAZARD
// About once a minute of played round time (randomly a bit earlier or later) an asteroid smashes
// into the ship and sends it spinning. A glow flashes at the edge of the screen where it's coming from.
// The ship spins for a random 5-10 seconds, slows down, and stops at a random angle.
//
// Put this on the parent object of all the ship pieces (it rotates around its own position,
// so place that object at the middle of the ship). Players, glue puddles, placed mines and
// weapon pickups standing on the ship are carried around with it.
//
// Testing: right-click the component header > "Trigger impact now" while in Play mode.
[DefaultExecutionOrder(-50)] // spin before players move each frame
public class AsteroidSpinHazard : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Average seconds of played round time between impacts (pausing doesn't count).")]
    [SerializeField, Min(5f)] private float impactInterval = 60f;
    [Tooltip("Each gap is randomly up to this many seconds earlier or later than the average.")]
    [SerializeField, Min(0f)] private float intervalVariation = 15f;
    [Tooltip("How long the ship spins after a hit (min, max seconds).")]
    [SerializeField] private Vector2 spinDuration = new Vector2(5f, 10f);
    [Tooltip("Roughly how fast the ship turns on average while spinning (degrees per second).")]
    [SerializeField, Min(5f)] private float averageSpinSpeed = 45f;
    [Tooltip("How quickly the spin reaches full speed after the hit (seconds).")]
    [SerializeField, Min(0.05f)] private float spinUpTime = 0.35f;

    [Header("Asteroid")]
    [Tooltip("The rock that flies in. Any model works - it just needs to look like an asteroid.")]
    [SerializeField] private GameObject asteroidPrefab;
    [Tooltip("Seconds of warning (edge flash + incoming rock) before the hit.")]
    [SerializeField, Min(0.5f)] private float warningTime = 2.5f;
    [SerializeField] private float asteroidStartDistance = 45f;
    [SerializeField] private float asteroidStartHeight = 22f;
    [Tooltip("Hits closer than this to the middle of the ship are skipped (they wouldn't spin it).")]
    [SerializeField] private float minImpactDistanceFromCenter = 8f;
    [SerializeField, Min(0)] private int debrisPieces = 7;

    [Header("Warning flash (screen edge)")]
    [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.1f, 1f);
    [Tooltip("Size of the glow, as a fraction of the screen height.")]
    [SerializeField, Range(0.1f, 1f)] private float flashSize = 0.6f;

    [Header("Feel")]
    [SerializeField] private float cameraShakeStrength = 0.6f;
    [SerializeField] private float cameraShakeTime = 0.5f;
    [SerializeField] private AudioClip warningSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Keep the ship on screen")]
    [Tooltip("After the first hit, pull the camera back just enough that the whole ship stays in view at any angle.")]
    [SerializeField] private bool keepShipOnScreen = true;
    [Tooltip("Empty space kept at the screen edges (0.03 = 3% of the screen).")]
    [SerializeField, Range(0f, 0.2f)] private float screenMargin = 0.03f;
    [Tooltip("How smoothly the camera moves in and out (seconds).")]
    [SerializeField, Min(0.05f)] private float cameraSmoothTime = 0.6f;
    [SerializeField, Min(1f)] private float maxCameraPullBack = 80f;

    [Header("Carrying things on the deck")]
    [Tooltip("How far below a player/object to look for the ship floor.")]
    [SerializeField] private float rideCheckDistance = 4f;

    public bool IsSpinning { get; private set; }

    private float playedTime;
    private float nextImpactTime;
    private bool sequenceRunning;
    private Rigidbody rb;

    private readonly List<CharacterController> riders = new List<CharacterController>();
    private readonly List<Transform> looseObjects = new List<Transform>();
    private readonly List<WeaponPickup> pickups = new List<WeaponPickup>();
    private float nextCarryRefresh;

    // Camera framing
    private Camera cam;
    private Vector3 camBasePos;
    private Quaternion camBaseRot;
    private Vector3 shakeOffset;
    private float pullBack, pullBackVelocity, targetPullBack;
    private Vector2 pan, panVelocity, targetPan;
    private bool framingActive;
    private readonly List<Vector3> shipOutline = new List<Vector3>(); // in this object's local space

    void Awake()
    {
        // Kinematic body so the ship's colliders move properly while spinning
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        nextImpactTime = NextGap();

        cam = Camera.main;
        if (cam != null)
        {
            camBasePos = cam.transform.position;
            camBaseRot = cam.transform.rotation;
        }
        BuildShipOutline();
    }

    void Update()
    {
        if (sequenceRunning || RoundIsOver()) return;

        playedTime += Time.deltaTime; // timeScale 0 while paused, so pause doesn't count
        if (playedTime >= nextImpactTime)
        {
            nextImpactTime = playedTime + NextGap();
            StartCoroutine(ImpactSequence());
        }
    }

    private float NextGap()
    {
        float v = Mathf.Min(intervalVariation, impactInterval - 5f);
        return impactInterval + Random.Range(-v, v);
    }

    [ContextMenu("Trigger impact now")]
    private void TriggerNow()
    {
        if (!Application.isPlaying || sequenceRunning) return;
        StartCoroutine(ImpactSequence());
    }

    // ---------- The whole event: warning -> asteroid flies in -> hit -> spin ----------

    private IEnumerator ImpactSequence()
    {
        sequenceRunning = true;

        if (!TryPickImpactPoint(out Vector3 impact))
        {
            sequenceRunning = false;
            yield break;
        }

        Vector3 pivot = transform.position;
        Vector3 fromCenter = impact - pivot; fromCenter.y = 0f;

        // Come in roughly sideways to the ship so the hit gives it a good spin
        Vector3 side = Vector3.Cross(Vector3.up, fromCenter.normalized) * (Random.value < 0.5f ? 1f : -1f);
        Vector3 outward = fromCenter.normalized;
        Vector3 flatDir = (side + outward * Random.Range(0.2f, 0.8f)).normalized; // where it comes FROM
        Vector3 start = impact + flatDir * asteroidStartDistance + Vector3.up * asteroidStartHeight;
        // Make sure it starts off-screen, so the edge flash always has somewhere to point
        for (int i = 0; i < 20 && cam != null && IsOnScreen(start); i++) start += (flatDir + Vector3.up * 0.5f) * 8f;

        // Spin direction follows the push of the rock: clockwise or anticlockwise
        Vector3 travel = (impact - start); travel.y = 0f;
        float spinSign = Mathf.Sign(Vector3.Cross(fromCenter, travel).y);
        if (spinSign == 0f) spinSign = 1f;

        if (warningSound != null) AudioSource.PlayClipAtPoint(warningSound, impact, soundVolume);

        // Fly the asteroid in
        GameObject rock = asteroidPrefab != null ? Instantiate(asteroidPrefab, start, Random.rotation) : null;
        Vector3 tumbleAxis = Random.onUnitSphere;
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / warningTime);
            if (rock != null)
            {
                rock.transform.position = Vector3.Lerp(start, impact, k * k); // speeds up as it closes in
                rock.transform.Rotate(tumbleAxis, 200f * Time.deltaTime, Space.World);
            }
            UpdateEdgeFlash(rock != null ? rock.transform.position : start, t, k);
            yield return null;
        }

        // Hit!
        HideEdgeFlash();
        if (rock != null)
        {
            SpawnDebris(rock, impact, travel.normalized);
            Destroy(rock);
        }
        if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, impact, soundVolume);
        StartCoroutine(ShakeCamera());

        framingActive = true;
        yield return Spin(spinSign);
        sequenceRunning = false;
    }

    // Spins quickly after the hit, then slows smoothly to a stop at a random angle.
    private IEnumerator Spin(float sign)
    {
        IsSpinning = true;

        float duration = Random.Range(Mathf.Min(spinDuration.x, spinDuration.y), Mathf.Max(spinDuration.x, spinDuration.y));
        float finalOffset = Random.Range(0f, 360f);                       // where it ends up
        int extraTurns = Mathf.Max(0, Mathf.RoundToInt((averageSpinSpeed * duration - finalOffset) / 360f));
        float totalAngle = finalOffset + 360f * extraTurns;
        if (totalAngle < 90f) totalAngle += 360f;                           // always a proper spin

        float rampUp = Mathf.Min(spinUpTime, duration * 0.5f);
        float peakSpeed = 2f * totalAngle / duration;                        // speed curve: quick ramp up, long slow-down

        RefreshCarried();
        float t = 0f, done = 0f;
        while (t < duration)
        {
            // Pause (timeScale 0) freezes the spin too
            t = Mathf.Min(t + Time.deltaTime, duration);
            float target = AngleAt(t, duration, rampUp, peakSpeed);
            RotateShip(sign * (target - done));
            done = target;
            yield return null;
        }

        IsSpinning = false;
    }

    // Total degrees turned after time t (area under the speed curve).
    private static float AngleAt(float t, float duration, float rampUp, float peak)
    {
        if (t <= rampUp) return peak * t * t / (2f * rampUp);
        float slow = duration - rampUp;
        float u = t - rampUp;
        return peak * rampUp * 0.5f + peak * (u - u * u / (2f * slow));
    }

    private void RotateShip(float degrees)
    {
        if (Mathf.Approximately(degrees, 0f)) return;
        Vector3 pivot = transform.position;
        Quaternion delta = Quaternion.AngleAxis(degrees, Vector3.up);

        if (Time.time >= nextCarryRefresh) RefreshCarried();

        // Carry players: move them exactly with the floor (no collision sweep, so walls can't snag them)
        foreach (var cc in riders)
        {
            if (cc == null || !cc.enabled) continue;
            Transform tr = cc.transform;
            tr.SetPositionAndRotation(pivot + delta * (tr.position - pivot), delta * tr.rotation);
        }
        foreach (var obj in looseObjects)
        {
            if (obj == null) continue;
            obj.SetPositionAndRotation(pivot + delta * (obj.position - pivot), delta * obj.rotation);
        }
        foreach (var p in pickups)
        {
            if (p != null) p.CarryAround(pivot, delta);
        }

        transform.rotation = delta * transform.rotation;
        Physics.SyncTransforms();
    }

    // ---------- What rides on the ship ----------

    private void RefreshCarried()
    {
        nextCarryRefresh = Time.time + 0.25f;

        riders.Clear();
        foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
        {
            if (cc.transform.IsChildOf(transform)) continue;
            if (IsAboveShip(cc.transform.position)) riders.Add(cc);
        }

        looseObjects.Clear();
        pickups.Clear();
        foreach (var p in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
            if (!p.transform.IsChildOf(transform) && IsAboveShip(p.transform.position)) pickups.Add(p);
        foreach (var g in FindObjectsByType<GluePuddle>(FindObjectsSortMode.None)) AddLoose(g.transform);
        foreach (var s in FindObjectsByType<WeaponSpawnPoint>(FindObjectsSortMode.None)) AddLoose(s.transform);
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is IPlacedByPlayer) AddLoose(mb.transform);
    }

    private void AddLoose(Transform t)
    {
        if (t == null || t.IsChildOf(transform)) return;
        if (t.GetComponentInParent<CharacterController>() != null) return; // held by a player, moves with them
        // only move the top-most object, once
        Transform top = t;
        while (top.parent != null && top.parent.GetComponentInParent<CharacterController>() == null && !top.parent.IsChildOf(transform)) top = top.parent;
        if (looseObjects.Contains(top)) return;
        if (IsAboveShip(top.position)) looseObjects.Add(top);
    }

    private bool IsAboveShip(Vector3 pos)
    {
        var hits = Physics.RaycastAll(pos + Vector3.up * 0.5f, Vector3.down, rideCheckDistance + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.collider.transform.IsChildOf(transform)) return true;
        return false;
    }

    // ---------- Helpers ----------

    private bool TryPickImpactPoint(out Vector3 point)
    {
        point = default;
        Bounds b = ShipBounds();
        for (int i = 0; i < 40; i++)
        {
            Vector3 from = new Vector3(Random.Range(b.min.x, b.max.x), b.max.y + 5f, Random.Range(b.min.z, b.max.z));
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.normal.y < 0.5f) continue;
            Vector3 flat = hit.point - transform.position; flat.y = 0f;
            if (flat.magnitude < minImpactDistanceFromCenter) continue;
            point = hit.point;
            return true;
        }
        return false;
    }

    private Bounds ShipBounds()
    {
        var cols = GetComponentsInChildren<Collider>();
        Bounds b = new Bounds(transform.position, Vector3.one);
        bool first = true;
        foreach (var c in cols)
        {
            if (c.isTrigger) continue;
            if (first) { b = c.bounds; first = false; } else b.Encapsulate(c.bounds);
        }
        return b;
    }

    private void SpawnDebris(GameObject rock, Vector3 at, Vector3 travelDir)
    {
        for (int i = 0; i < debrisPieces; i++)
        {
            var piece = Instantiate(rock, at + Vector3.up * 0.5f, Random.rotation);
            piece.transform.localScale = rock.transform.localScale * Random.Range(0.15f, 0.3f);
            foreach (var c in piece.GetComponentsInChildren<Collider>()) Destroy(c); // just for show, don't block players
            var body = piece.AddComponent<Rigidbody>();
            Vector3 dir = (travelDir * 0.6f + Random.insideUnitSphere + Vector3.up * 1.2f).normalized;
            body.linearVelocity = dir * Random.Range(6f, 14f);
            body.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(piece, 1.5f);
        }
    }

    private IEnumerator ShakeCamera()
    {
        if (cam == null || cameraShakeStrength <= 0f) yield break;
        float t = 0f;
        while (t < cameraShakeTime)
        {
            t += Time.deltaTime;
            float fade = 1f - t / cameraShakeTime;
            shakeOffset = Random.insideUnitSphere * cameraShakeStrength * fade;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }

    // ---------- Warning: glow at the screen edge where the asteroid is coming from ----------

    private UnityEngine.UI.Image flashImage;

    private void UpdateEdgeFlash(Vector3 asteroidPos, float time, float progress)
    {
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(asteroidPos);
        if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }  // behind the camera: mirror it

        bool onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        if (onScreen) { HideEdgeFlash(); return; }   // you can see the rock now, no need for the flash

        if (flashImage == null) CreateFlashUI();

        // Push the point out from the screen centre until it touches the edge
        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        float scale = 0.5f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), 0.0001f);
        Vector2 edge = new Vector2(0.5f, 0.5f) + d * scale;

        var rt = flashImage.rectTransform;
        rt.anchorMin = rt.anchorMax = edge;
        rt.anchoredPosition = Vector2.zero;
        float size = Screen.height * flashSize;
        rt.sizeDelta = new Vector2(size, size);

        // Pulses, and pulses faster as the rock gets close
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(8f, 22f, progress));
        Color c = flashColor;
        c.a = flashColor.a * Mathf.Lerp(0.55f, 1f, pulse);
        flashImage.color = c;
        flashImage.enabled = true;
    }

    private void HideEdgeFlash()
    {
        if (flashImage != null) flashImage.enabled = false;
    }

    private bool IsOnScreen(Vector3 world)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
    }

    private void CreateFlashUI()
    {
        var canvasGO = new GameObject("AsteroidWarningUI", typeof(Canvas));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var imgGO = new GameObject("EdgeFlash", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        imgGO.transform.SetParent(canvasGO.transform, false);
        flashImage = imgGO.GetComponent<UnityEngine.UI.Image>();
        flashImage.sprite = MakeGlowSprite();
        flashImage.raycastTarget = false;
        flashImage.enabled = false;
    }

    // Soft round glow, bright in the middle and fading out (made in code, so no texture asset needed)
    private static Sprite MakeGlowSprite()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(a, 0.8f));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // ---------- Camera: keep the whole ship on screen ----------

    void LateUpdate()
    {
        if (cam == null || !framingActive) return;

        if (keepShipOnScreen && !cam.orthographic)
        {
            if (IsSpinning)
            {
                // While spinning: hold the sideways position and only ever pull back
                // (no zooming in and out every turn).
                targetPan = pan;
                targetPullBack = Mathf.Max(targetPullBack, RequiredPullBack(pan));
            }
            else
            {
                // Stopped: centre the ship and come in as close as possible while it all fits.
                BestFraming(out targetPan, out targetPullBack);
            }

            pan = Vector2.SmoothDamp(pan, targetPan, ref panVelocity, cameraSmoothTime);
            pullBack = Mathf.SmoothDamp(pullBack, targetPullBack, ref pullBackVelocity, cameraSmoothTime);
            if (!IsSpinning) pullBack = Mathf.Max(pullBack, RequiredPullBack(pan)); // never cut the ship off while settling
        }

        cam.transform.position = CameraPos(pan, pullBack) + shakeOffset;
    }

    private Vector3 CameraPos(Vector2 sideways, float back)
    {
        return camBasePos
             + camBaseRot * Vector3.right * sideways.x
             + camBaseRot * Vector3.up * sideways.y
             - camBaseRot * Vector3.forward * back;
    }

    // Finds the sideways shift that centres the ship on screen, and the pull-back it then needs.
    private void BestFraming(out Vector2 bestPan, out float bestBack)
    {
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * cam.aspect;
        Quaternion toCam = Quaternion.Inverse(camBaseRot);
        bestPan = Vector2.zero;
        bestBack = 0f;

        for (int iter = 0; iter < 4; iter++)
        {
            bestBack = RequiredPullBack(bestPan);
            Vector3 c = CameraPos(bestPan, bestBack);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, depth = 0f;
            foreach (var local in shipOutline)
            {
                Vector3 v = toCam * (transform.TransformPoint(local) - c);
                if (v.z <= 0.1f) continue;
                float x = v.x / (v.z * tanH), y = v.y / (v.z * tanV);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                depth += v.z;
            }
            if (shipOutline.Count == 0) return;
            depth /= shipOutline.Count;
            bestPan.x += (minX + maxX) * 0.5f * depth * tanH;
            bestPan.y += (minY + maxY) * 0.5f * depth * tanV;
        }
        bestBack = RequiredPullBack(bestPan);
    }

    // How far the camera has to back away (along its view direction) so every part of the ship fits.
    private float RequiredPullBack(Vector2 sideways)
    {
        if (FitsOnScreen(CameraPos(sideways, 0f))) return 0f;
        if (!FitsOnScreen(CameraPos(sideways, maxCameraPullBack))) return maxCameraPullBack;
        float lo = 0f, hi = maxCameraPullBack;
        for (int i = 0; i < 18; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (FitsOnScreen(CameraPos(sideways, mid))) hi = mid; else lo = mid;
        }
        return hi;
    }

    private bool FitsOnScreen(Vector3 camPos)
    {
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * cam.aspect;
        float limit = 1f - 2f * screenMargin;
        Quaternion toCam = Quaternion.Inverse(camBaseRot);
        foreach (var local in shipOutline)
        {
            Vector3 v = toCam * (transform.TransformPoint(local) - camPos);
            if (v.z <= 0.1f) return false;
            if (Mathf.Abs(v.x / (v.z * tanH)) > limit || Mathf.Abs(v.y / (v.z * tanV)) > limit) return false;
        }
        return true;
    }

    // Outline of the ship: its real corner points, found by taking the outline (convex hull, seen from above)
    // of each thin height slice. Inner points can never touch the screen edge first, so they're dropped.
    private void BuildShipOutline()
    {
        shipOutline.Clear();
        const float sliceHeight = 0.5f;
        var slices = new Dictionary<int, List<Vector3>>();

        void AddPoint(Vector3 local)
        {
            int key = Mathf.FloorToInt(local.y / sliceHeight);
            if (!slices.TryGetValue(key, out var list)) slices[key] = list = new List<Vector3>();
            list.Add(local);
        }

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                foreach (var v in mf.sharedMesh.vertices) AddPoint(transform.InverseTransformPoint(r.transform.TransformPoint(v)));
            }
            else
            {
                Bounds bb = r.bounds;
                for (int i = 0; i < 8; i++)
                    AddPoint(transform.InverseTransformPoint(new Vector3(
                        (i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z)));
            }
        }

        foreach (var slice in slices.Values)
        {
            // hull in x/z, but keep each kept point's real height
            var flat = new List<Vector2>(slice.Count);
            var heightOf = new Dictionary<Vector2, float>();
            foreach (var pt in slice)
            {
                var f = new Vector2(pt.x, pt.z);
                if (heightOf.TryGetValue(f, out float y)) { if (pt.y > y) heightOf[f] = pt.y; }
                else { heightOf[f] = pt.y; flat.Add(f); }
            }
            foreach (var h in ConvexHull(flat)) shipOutline.Add(new Vector3(h.x, heightOf[h], h.y));
        }
    }

    private static List<Vector2> ConvexHull(List<Vector2> pts)
    {
        if (pts.Count < 3) return new List<Vector2>(pts);
        pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var hull = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pass == 0 ? pts[i] : pts[pts.Count - 1 - i];
                while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    private bool RoundIsOver()
    {
        // Same rule as the RoundManager: with 2+ players, the round is decided once 1 or fewer are alive
        var all = PlayerInput.all;
        if (all.Count < 2) return false;
        int alive = 0;
        foreach (var p in all)
            if (p != null && p.TryGetComponent(out Health h) && !h.IsDead) alive++;
        return alive <= 1;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 6f);
    }
}
