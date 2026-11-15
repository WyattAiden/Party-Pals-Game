using UnityEngine;

// Goes on a glue droplet projectile. Stretches the droplet's visual along the way it's flying
// and draws a sticky strand back to the droplet fired just before it, so a squirt reads as one stream.
[RequireComponent(typeof(LineRenderer))]
public class GlueStrand : MonoBehaviour
{
    [Tooltip("Visual-only child (no collider) that gets stretched in the flight direction.")]
    [SerializeField] private Transform blob;
    [Tooltip("Extra length per m/s of speed.")]
    [SerializeField] private float stretchPerSpeed = 0.06f;
    [SerializeField] private float maxStretch = 2f;
    [SerializeField] private float strandWidth = 0.6f;
    // (strand width is multiplied by the droplet's size)
    [Tooltip("The strand snaps if the droplets get further apart than this.")]
    [SerializeField] private float maxGap = 2.5f;

    private GlueStrand previous;
    private LineRenderer line;
    private Vector3 lastPos;
    private Vector3 blobBaseScale;
    private bool snapped;

    public void Link(GlueStrand prev) => previous = prev;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        lastPos = transform.position;
        if (blob != null) blobBaseScale = blob.localScale;
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;
        Vector3 vel = (pos - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = pos;

        // Stretch the blob into a teardrop-ish streak along its velocity
        if (blob != null && vel.sqrMagnitude > 0.01f)
        {
            float s = Mathf.Clamp(1f + vel.magnitude * stretchPerSpeed, 1f, maxStretch);
            float thin = 1f / Mathf.Sqrt(s); // keep roughly the same volume
            blob.rotation = Quaternion.LookRotation(vel);
            blob.localScale = new Vector3(blobBaseScale.x * thin, blobBaseScale.y * thin, blobBaseScale.z * s);
        }

        // Strand back to the previous droplet (gone once that one lands or it stretches too far)
        if (snapped || previous == null) { line.positionCount = 0; return; }
        Vector3 other = previous.transform.position;
        float gap = Vector3.Distance(pos, other);
        if (gap > maxGap) { snapped = true; line.positionCount = 0; return; }

        float size = transform.lossyScale.x;
        float w = strandWidth * size * Mathf.Lerp(1f, 0.35f, gap / maxGap); // thins as it stretches
        line.positionCount = 2;
        line.SetPosition(0, pos);
        line.SetPosition(1, other);
        line.startWidth = w;
        line.endWidth = w * 0.8f;
    }
}
