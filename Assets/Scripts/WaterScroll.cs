using UnityEngine;

// Slowly drifts the water ripple texture so the surface looks like it is moving.
// Works on a runtime copy of the material, so the Water.mat asset is never changed.
// Note: the Standard shader uses the main texture's tiling/offset for the normal map too.
public class WaterScroll : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.02f, 0.012f);

    Material mat;
    Vector2 offset;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        offset += speed * Time.deltaTime;
        mat.SetTextureOffset("_MainTex", offset);
    }
}
