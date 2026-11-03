using UnityEngine;

/// <summary>
/// Drives the object straight in one direction and destroys it
/// once it has travelled a set distance from where it started.
/// </summary>
public class CarMover : MonoBehaviour
{
    [Tooltip("How fast the car drives (units per second).")]
    [SerializeField] private float speed = 10f;

    [Tooltip("The car is destroyed after travelling this far from its starting point.")]
    [SerializeField] private float destroyDistance = 40f;

    [Tooltip("Direction to drive, relative to the car itself. (0,0,1) = the car's blue Z arrow, (0,0,-1) = the opposite way.")]
    [SerializeField] private Vector3 localDirection = Vector3.back;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.Translate(localDirection.normalized * speed * Time.deltaTime, Space.Self);

        if (Vector3.Distance(startPosition, transform.position) >= destroyDistance)
            Destroy(gameObject);
    }

    // Shows where the car will be destroyed when it's selected in the Scene view
    private void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? startPosition : transform.position;
        Vector3 to = from + transform.TransformDirection(localDirection.normalized) * destroyDistance;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.5f);
    }
}
