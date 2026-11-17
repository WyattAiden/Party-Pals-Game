using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Draws a Scene-view gizmo for a spawn point so it is easy to see and select.
/// Editor-only visuals: this has no effect in builds or at runtime.
/// </summary>
public class SpawnPointGizmo : MonoBehaviour
{
    public enum SpawnKind { Player, Bomb, Cannonball, Other }

    [SerializeField] private SpawnKind kind = SpawnKind.Player;
    [SerializeField] private bool useCustomColor = false;
    [SerializeField] private Color customColor = Color.white;
    [SerializeField] private float radius = 0.5f;

    [Tooltip("World-space direction to draw an arrow in (e.g. the direction a projectile travels). Leave at zero for no arrow.")]
    [SerializeField] private Vector3 arrowDirection = Vector3.zero;
    [SerializeField] private float arrowLength = 2f;

    [SerializeField] private bool showLabel = true;
    [Tooltip("Label text. Leave blank to use the GameObject's name.")]
    [SerializeField] private string label = "";

    public SpawnKind Kind => kind;

    public void Configure(SpawnKind newKind, Vector3 newArrowDirection, float newRadius)
    {
        kind = newKind;
        arrowDirection = newArrowDirection;
        radius = newRadius;
    }

    private Color GizmoColor
    {
        get
        {
            if (useCustomColor) return customColor;
            switch (kind)
            {
                case SpawnKind.Player:     return new Color(0.2f, 0.9f, 0.3f);
                case SpawnKind.Bomb:       return new Color(1f, 0.3f, 0.2f);
                case SpawnKind.Cannonball: return new Color(1f, 0.65f, 0.1f);
                default:                   return new Color(0.3f, 0.7f, 1f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Color c = GizmoColor;
        Vector3 pos = transform.position;

        // Translucent fill + solid outline
        Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
        Gizmos.DrawSphere(pos, radius);
        Gizmos.color = c;
        Gizmos.DrawWireSphere(pos, radius);

        if (kind == SpawnKind.Player)
        {
            // Ground ring + facing line so player spawns read clearly
            DrawCircle(pos, radius * 1.6f, c);
            Gizmos.DrawLine(pos, pos + transform.forward * radius * 2f);
        }

        if (arrowDirection.sqrMagnitude > 0.0001f)
            DrawArrow(pos, arrowDirection.normalized * arrowLength, c);

#if UNITY_EDITOR
        if (showLabel)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = c } };
            Handles.Label(pos + Vector3.up * (radius + 0.4f),
                string.IsNullOrEmpty(label) ? gameObject.name : label, style);
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, radius * 1.15f);
    }

    private static void DrawCircle(Vector3 center, float r, Color c, int segments = 32)
    {
        Gizmos.color = c;
        Vector3 prev = center + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private static void DrawArrow(Vector3 from, Vector3 dir, Color c)
    {
        Gizmos.color = c;
        Vector3 to = from + dir;
        Gizmos.DrawLine(from, to);

        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(dir, Vector3.right);
        side = side.normalized * dir.magnitude * 0.15f;
        Vector3 back = -dir * 0.25f;
        Gizmos.DrawLine(to, to + back + side);
        Gizmos.DrawLine(to, to + back - side);
    }
}
