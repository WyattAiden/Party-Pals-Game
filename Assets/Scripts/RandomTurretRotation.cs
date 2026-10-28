using UnityEngine;

/// <summary>
/// Makes a turret turn to random angles around its Y axis, pausing between turns.
/// Put this on the turret object; children (like the barrel) turn with it.
/// </summary>
public class RandomTurretRotation : MonoBehaviour
{
    [Header("Turning")]
    [Tooltip("How fast the turret turns, in degrees per second.")]
    public float turnSpeed = 30f;

    [Tooltip("How far the turret can turn either side of its starting direction. 180 = full circle.")]
    [Range(0f, 180f)]
    public float maxAngleFromStart = 180f;

    [Tooltip("Smallest turn it will bother making, so it doesn't just twitch.")]
    public float minTurnAmount = 30f;

    [Header("Pausing")]
    [Tooltip("Shortest pause after reaching a direction, in seconds.")]
    public float minPause = 1f;

    [Tooltip("Longest pause after reaching a direction, in seconds.")]
    public float maxPause = 3f;

    private float startYaw;
    private float targetOffset;
    private float currentOffset;
    private float pauseTimer;

    void Start()
    {
        startYaw = transform.localEulerAngles.y;
        currentOffset = 0f;
        PickNewTarget();
        pauseTimer = Random.Range(minPause, maxPause);
    }

    void Update()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        currentOffset = Mathf.MoveTowards(currentOffset, targetOffset, turnSpeed * Time.deltaTime);
        ApplyRotation();

        if (Mathf.Approximately(currentOffset, targetOffset))
        {
            pauseTimer = Random.Range(minPause, maxPause);
            PickNewTarget();
        }
    }

    void PickNewTarget()
    {
        // Try a few times to find a target far enough away to be a real turn.
        for (int i = 0; i < 10; i++)
        {
            targetOffset = Random.Range(-maxAngleFromStart, maxAngleFromStart);
            if (Mathf.Abs(targetOffset - currentOffset) >= minTurnAmount) return;
        }
    }

    void ApplyRotation()
    {
        Vector3 e = transform.localEulerAngles;
        e.y = startYaw + currentOffset;
        transform.localEulerAngles = e;
    }
}
