using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a coloured pointer above the player whenever level geometry blocks
/// the camera's view of them. Put this on the player prefab - it builds its own UI.
/// </summary>
[DisallowMultipleComponent]
public class PlayerOcclusionIndicator : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Layers that can hide the player (walls, cubes, bridges...).")]
    [SerializeField] private LayerMask occluderLayers = ~0;
    [Tooltip("Other players walking in front don't count as hiding you.")]
    [SerializeField] private bool ignoreOtherPlayers = true;
    [Tooltip("How much of the player must be covered before the pointer shows (0.5 = half).")]
    [SerializeField, Range(0.1f, 1f)] private float hiddenFraction = 0.6f;
    [Tooltip("Seconds the player must stay hidden before the pointer appears (stops flicker).")]
    [SerializeField] private float showDelay = 0.1f;

    [Header("Pointer look")]
    [SerializeField] private float pointerSize = 48f;
    [Tooltip("Pixels between the top of the player and the arrow tip.")]
    [SerializeField] private float screenOffset = 30f;
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField] private float bobAmount = 6f;
    [SerializeField] private float bobSpeed = 5f;
    [SerializeField] private bool showPlayerNumber = true;

    // Shared between all players
    private static Canvas sharedCanvas;
    private static Sprite arrowSprite;
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    private Camera cam;
    private Collider bodyCollider;
    private RectTransform pointer;
    private CanvasGroup pointerGroup;
    private float hiddenTimer;
    private float targetAlpha;
    private int playerIndex;

    private void Start()
    {
        bodyCollider = GetComponent<CharacterController>();
        if (bodyCollider == null) bodyCollider = GetComponentInChildren<Collider>();

        var input = GetComponent<PlayerInput>();
        playerIndex = input != null ? input.playerIndex : 0;

        BuildPointer();
    }

    private void LateUpdate()
    {
        if (pointer == null || bodyCollider == null) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Bounds b = bodyCollider.bounds;
        Vector3 top = b.center + Vector3.up * b.extents.y;

        // Is the player covered?
        Vector3 camPos = cam.transform.position;
        int blocked = 0;
        Vector3[] samples =
        {
            b.center,
            b.center + Vector3.up * b.extents.y * 0.8f,
            b.center - Vector3.up * b.extents.y * 0.8f,
            b.center + cam.transform.right * b.extents.x * 0.8f,
            b.center - cam.transform.right * b.extents.x * 0.8f,
        };
        foreach (var p in samples)
            if (IsBlocked(camPos, p)) blocked++;

        bool hidden = blocked >= Mathf.CeilToInt(samples.Length * hiddenFraction);
        hiddenTimer = hidden ? hiddenTimer + Time.deltaTime : 0f;
        targetAlpha = hiddenTimer >= showDelay ? 1f : 0f;

        // Place the pointer above the player's head on screen
        Vector3 screen = cam.WorldToScreenPoint(top);
        if (screen.z < 0f) targetAlpha = 0f;

        float scale = Screen.height / 1080f;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        pointer.position = new Vector3(screen.x, screen.y + (screenOffset + bob) * scale, 0f);
        pointer.localScale = Vector3.one * scale;

        pointerGroup.alpha = Mathf.MoveTowards(pointerGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    private bool IsBlocked(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        int count = Physics.RaycastNonAlloc(from, dir / dist, hits, dist - 0.05f,
                                            occluderLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;                       // ourselves
            if (ignoreOtherPlayers && c.GetComponentInParent<PlayerInput>()) continue; // other players
            return true;
        }
        return false;
    }

    private void BuildPointer()
    {
        EnsureSharedResources();

        Color color = Color.white;
        if (GameSettings.playerColors != null && playerIndex < GameSettings.playerColors.Length)
            color = GameSettings.playerColors[playerIndex];
        color.a = 1f;

        var root = new GameObject($"P{playerIndex + 1} Pointer", typeof(RectTransform), typeof(CanvasGroup));
        pointer = (RectTransform)root.transform;
        pointer.SetParent(sharedCanvas.transform, false);
        pointer.sizeDelta = new Vector2(pointerSize, pointerSize);
        pointer.pivot = new Vector2(0.5f, 0f); // tip of the arrow sits on the target point
        pointerGroup = root.GetComponent<CanvasGroup>();
        pointerGroup.alpha = 0f;
        pointerGroup.blocksRaycasts = false;
        pointerGroup.interactable = false;

        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        var arrowRect = (RectTransform)arrow.transform;
        arrowRect.SetParent(pointer, false);
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.offsetMin = arrowRect.offsetMax = Vector2.zero;
        var img = arrow.GetComponent<Image>();
        img.sprite = arrowSprite;
        img.color = color;
        img.raycastTarget = false;

        if (showPlayerNumber)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(pointer, false);
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(100f, 40f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = $"P{playerIndex + 1}";
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.LowerCenter;
            text.color = color;
            text.raycastTarget = false;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    private static void EnsureSharedResources()
    {
        if (sharedCanvas == null)
        {
            var go = new GameObject("Player Pointer Canvas", typeof(Canvas));
            sharedCanvas = go.GetComponent<Canvas>();
            sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            sharedCanvas.sortingOrder = 100;
        }
        if (arrowSprite == null) arrowSprite = CreateArrowSprite(64);
    }

    // Draws a downward-pointing white triangle with a black outline.
    // White gets tinted by the player colour, the outline stays black.
    private static Sprite CreateArrowSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 a = new Vector2(size * 0.12f, size * 0.92f);  // top-left
        Vector2 b = new Vector2(size * 0.88f, size * 0.92f);  // top-right
        Vector2 c = new Vector2(size * 0.5f, size * 0.06f);   // tip (bottom)
        float outline = size * 0.08f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(EdgeDist(a, b, p), EdgeDist(b, c, p), EdgeDist(c, a, p));
            float alpha = Mathf.Clamp01(d + 0.5f);
            float white = Mathf.Clamp01(d - outline + 0.5f);
            tex.SetPixel(x, y, new Color(white, white, white, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), 100f);
    }

    // Signed distance from p to the edge a->b (positive = inside a clockwise triangle)
    private static float EdgeDist(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 e = b - a;
        return ((p.x - a.x) * e.y - (p.y - a.y) * e.x) / e.magnitude;
    }

    private void OnDisable()
    {
        if (pointerGroup != null) pointerGroup.alpha = 0f;
        hiddenTimer = 0f;
    }

    private void OnDestroy()
    {
        if (pointer != null) Destroy(pointer.gameObject);
    }
}
