using System;
using UnityEngine;

// Tweakable numbers for the Sticky Hand. Lives on a PowerWeaponData asset whose category is Defensive.
[Serializable]
public class StickyHandSettings
{
    [Header("Throw")]
    [Tooltip("How fast the hand flies out (units per second).")]
    public float throwSpeed = 24f;
    [Tooltip("How far the hand can reach before it snaps back empty.")]
    public float maxRange = 10f;
    [Tooltip("How fat the hand is for hitting players. Bigger = easier to land.")]
    public float handRadius = 0.35f;
    [Tooltip("How fast an empty hand snaps back to the thrower.")]
    public float retractSpeed = 32f;

    [Header("Grab")]
    [Tooltip("Longest a player can be held before the hand acts on its own. 0 = hold forever.")]
    public float maxGrabTime = 3f;
    [Tooltip("When the hold time runs out: pull them in (on) or just let go (off).")]
    public bool autoPullWhenTimeUp = true;
    [Tooltip("Extra slack past the grab distance before the rope snaps (e.g. the grabbed player is stuck behind a wall).")]
    public float breakDistance = 4f;

    [Header("Pull (press shoot again)")]
    [Tooltip("How fast the grabbed player is yanked in.")]
    public float pullSpeed = 22f;
    [Tooltip("Where the yanked player stops, measured from the thrower.")]
    public float pullStopDistance = 1.4f;
    [Tooltip("Damage dealt when the pull lands. 0 = none.")]
    public float pullDamage = 0f;
    [Tooltip("Seconds the yanked player still can't move or shoot after landing (a free hit, Scorpion style).")]
    public float stunAfterPull = 0.6f;

    [Header("Look")]
    [Tooltip("Optional model for the hand. Leave empty to use a simple coloured blob.")]
    public GameObject handModelPrefab;
    public Color handColor = new Color(1f, 0.25f, 0.6f);
    [Tooltip("Size of the default blob (ignored if a model is set).")]
    public float handSize = 0.45f;
    public float ropeWidth = 0.07f;
}

// The thrown hand. Created by PlayerWeaponHandler when a Defensive weapon is used.
// Flying -> (hits a player) Holding -> (shoot again / time up) Pulling -> done
//        -> (misses) Retracting -> done
public class StickyHand : MonoBehaviour
{
    private enum State { Flying, Holding, Pulling, Retracting }

    private State state;
    private StickyHandSettings s;
    private GameObject owner;
    private Transform firePoint;
    private Action<StickyHand> onFinished;
    private bool finished;

    private Vector3 dir;
    private float travelled;

    private PlayerController victim;
    private CharacterController victimCC;
    private PlayerStatusEffects victimStatus;
    private Health victimHealth;
    private float tetherLength;
    private float timer;
    private float stuckTimer;

    private LineRenderer rope;
    private Material handMat, ropeMat;

    public bool IsHolding => state == State.Holding;

    public static StickyHand Throw(StickyHandSettings settings, Transform firePoint, GameObject owner, Action<StickyHand> onFinished)
    {
        var go = new GameObject("StickyHand (" + owner.name + ")");
        go.transform.position = firePoint.position;
        var hand = go.AddComponent<StickyHand>();
        hand.Init(settings ?? new StickyHandSettings(), firePoint, owner, onFinished);
        return hand;
    }

    private void Init(StickyHandSettings settings, Transform fp, GameObject who, Action<StickyHand> done)
    {
        s = settings;
        firePoint = fp;
        owner = who;
        onFinished = done;

        dir = Flat(firePoint.forward);
        if (dir.sqrMagnitude < 0.001f) dir = Flat(owner.transform.forward);
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();
        transform.rotation = Quaternion.LookRotation(dir);

        BuildVisuals();
        state = State.Flying;
    }

    // ---------- called by PlayerWeaponHandler ----------

    // Shoot pressed again while the hand is out. Returns true if that started a pull.
    public bool PressShoot()
    {
        if (state != State.Holding) return false;
        StartPull();
        return true;
    }

    // Owner died / despawned: let go right away.
    public void Cancel() => Finish();

    // ---------- update ----------

    void Update()
    {
        if (finished) return;
        if (owner == null || !owner.activeInHierarchy || firePoint == null) { Finish(); return; }

        float dt = Time.deltaTime;
        switch (state)
        {
            case State.Flying:
            {
                float step = s.throwSpeed * dt;
                Vector3 start = transform.position;
                if (CheckHit(start, step)) break;
                transform.position = start + dir * step;
                travelled += step;
                if (travelled >= s.maxRange) StartRetract();
                break;
            }
            case State.Holding:
                if (!VictimValid()) { ReleaseVictim(0f); StartRetract(); break; }
                timer += dt;
                if (s.maxGrabTime > 0f && timer >= s.maxGrabTime)
                {
                    if (s.autoPullWhenTimeUp) StartPull();
                    else { ReleaseVictim(0f); StartRetract(); }
                }
                break;
            case State.Pulling:
                if (!VictimValid()) { ReleaseVictim(0f); StartRetract(); }
                break;
            case State.Retracting:
            {
                Vector3 target = firePoint.position;
                transform.position = Vector3.MoveTowards(transform.position, target, s.retractSpeed * dt);
                if ((transform.position - target).sqrMagnitude < 0.04f) Finish();
                break;
            }
        }
    }

    // Runs after the players have moved this frame, so the rope always wins.
    void LateUpdate()
    {
        if (finished) return;
        float dt = Time.deltaTime;

        if ((state == State.Holding || state == State.Pulling) && VictimValid())
        {
            Vector3 toOwner = Flat(owner.transform.position - victim.transform.position);
            float d = toOwner.magnitude;
            Vector3 toOwnerDir = d > 0.0001f ? toOwner / d : Vector3.zero;

            if (state == State.Holding)
            {
                // Tied together: if the thrower walks away, the grabbed player gets dragged along
                if (d > tetherLength + s.breakDistance) { ReleaseVictim(0f); StartRetract(); }
                else if (d > tetherLength) victimCC.Move(toOwnerDir * (d - tetherLength));
            }
            else // Pulling: GET OVER HERE
            {
                timer += dt;
                float step = Mathf.Min(s.pullSpeed * dt, d - s.pullStopDistance);
                if (step > 0f)
                {
                    Vector3 before = victim.transform.position;
                    victimCC.Move(toOwnerDir * step);
                    float moved = Flat(victim.transform.position - before).magnitude;
                    stuckTimer = moved < step * 0.2f ? stuckTimer + dt : 0f; // snagged on a wall
                }
                float maxPullTime = s.maxRange / Mathf.Max(1f, s.pullSpeed) + 0.75f;
                if (d - Mathf.Max(step, 0f) <= s.pullStopDistance + 0.05f || stuckTimer > 0.25f || timer > maxPullTime)
                    LandPull();
            }
        }

        if (finished) return;
        if ((state == State.Holding || state == State.Pulling) && victimCC != null)
            transform.position = victimCC.bounds.center;
        UpdateRope();
    }

    // ---------- helpers ----------

    private bool CheckHit(Vector3 start, float step)
    {
        var hits = Physics.SphereCastAll(start, s.handRadius, dir, step, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            Collider col = hit.collider;
            PlayerController pc = col.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                if (pc.gameObject == owner) continue;
                if (!TryGrab(pc)) StartRetract(); // dead or already grabbed by someone else
                return true;
            }

            if (col.GetComponentInParent<WeaponPickup>() != null) continue;
            if (col.GetComponent<Projectile>() != null) continue;
            if (col.GetComponent<GluePuddle>() != null) continue;
            if (hit.distance <= 0f) continue;          // something we started inside (e.g. the floor at point blank)
            if (hit.normal.y > 0.7f) continue;         // skimming along the floor

            // Hit a wall: stick for a moment then come back empty
            transform.position = start + dir * hit.distance;
            StartRetract();
            return true;
        }
        return false;
    }

    private bool TryGrab(PlayerController pc)
    {
        var status = pc.GetComponent<PlayerStatusEffects>();
        var cc = pc.GetComponent<CharacterController>();
        var hp = pc.GetComponent<Health>();
        if (status == null || cc == null || (hp != null && hp.IsDead)) return false;
        if (!status.TryGrab(owner)) return false;

        victim = pc;
        victimCC = cc;
        victimStatus = status;
        victimHealth = hp;
        tetherLength = Mathf.Max(Flat(owner.transform.position - pc.transform.position).magnitude, s.pullStopDistance);
        timer = 0f;
        state = State.Holding;
        transform.position = cc.bounds.center;
        return true;
    }

    private void StartPull()
    {
        timer = 0f;
        stuckTimer = 0f;
        state = State.Pulling;
    }

    private void LandPull()
    {
        if (victimHealth != null && s.pullDamage > 0f) victimHealth.TakeDamage(s.pullDamage, owner);
        ReleaseVictim(s.stunAfterPull);
        Finish();
    }

    private void StartRetract()
    {
        state = State.Retracting;
    }

    private bool VictimValid()
    {
        return victim != null && victim.isActiveAndEnabled
            && (victimHealth == null || !victimHealth.IsDead)
            && victimStatus != null && victimStatus.GrabbedBy == owner;
    }

    private void ReleaseVictim(float stun)
    {
        if (victimStatus != null) victimStatus.ReleaseGrab(owner, stun);
        victim = null; victimCC = null; victimStatus = null; victimHealth = null;
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        ReleaseVictim(0f);
        onFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private void BuildVisuals()
    {
        GameObject model;
        if (s.handModelPrefab != null)
        {
            model = Instantiate(s.handModelPrefab, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.name = "Hand";
            model.transform.SetParent(transform, false);
            model.transform.localScale = Vector3.one * s.handSize;
            var r = model.GetComponent<Renderer>();
            handMat = r.material;
            handMat.color = s.handColor;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // The hand never blocks shots or players - it only grabs through its own sweep
        foreach (var c in model.GetComponentsInChildren<Collider>()) DestroyImmediate(c);

        rope = gameObject.AddComponent<LineRenderer>();
        rope.positionCount = 2;
        rope.useWorldSpace = true;
        rope.startWidth = s.ropeWidth;
        rope.endWidth = s.ropeWidth * 1.4f;
        rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rope.receiveShadows = false;
        Shader ropeShader = Shader.Find("Sprites/Default");
        if (ropeShader != null) { ropeMat = new Material(ropeShader); rope.sharedMaterial = ropeMat; }
        else if (handMat != null) rope.sharedMaterial = handMat;
        rope.startColor = s.handColor;
        rope.endColor = s.handColor;
        UpdateRope();
    }

    private void UpdateRope()
    {
        if (rope == null || firePoint == null) return;
        rope.SetPosition(0, firePoint.position);
        rope.SetPosition(1, transform.position);
    }

    void OnDestroy()
    {
        if (!finished) { finished = true; ReleaseVictim(0f); onFinished?.Invoke(this); }
        if (handMat != null) Destroy(handMat);
        if (ropeMat != null) Destroy(ropeMat);
    }
}
