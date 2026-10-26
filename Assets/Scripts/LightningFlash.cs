using System.Collections;
using UnityEngine;

// Random lightning: briefly spikes the sun's brightness (1-3 quick flickers), tints the fog,
// and throws the shadows in a new direction, then returns everything to normal.
public class LightningFlash : MonoBehaviour
{
    public Light sun;
    public float minDelay = 4f;
    public float maxDelay = 11f;
    public float peakIntensity = 3f;
    public Color flashColor = new Color(0.8f, 0.9f, 1f);
    public int strikeCount;   // how many lightning strikes have happened (handy for testing)

    Color baseLight, baseFog;
    float baseIntensity;
    Quaternion baseRotation;
    bool ready;

    void Start()
    {
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null) { enabled = false; return; }
        baseLight = sun.color;
        baseIntensity = sun.intensity;
        baseRotation = sun.transform.rotation;
        baseFog = RenderSettings.fogColor;
        ready = true;
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
            strikeCount++;
            sun.transform.rotation = Quaternion.Euler(Random.Range(35f, 70f), Random.Range(0f, 360f), 0f);
            int flickers = Random.Range(1, 4);
            for (int i = 0; i < flickers; i++)
            {
                SetFlash(1f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
                float dur = Random.Range(0.05f, 0.12f), t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    SetFlash(1f - Mathf.Clamp01(t / dur));
                    yield return null;
                }
                SetFlash(0f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }
            sun.transform.rotation = baseRotation;
        }
    }

    void SetFlash(float k)
    {
        sun.intensity = Mathf.Lerp(baseIntensity, peakIntensity, k);
        sun.color = Color.Lerp(baseLight, flashColor, k);
        RenderSettings.fogColor = Color.Lerp(baseFog, flashColor * 0.5f, k);
    }

    void OnDisable()
    {
        if (!ready || sun == null) return;
        SetFlash(0f);
        sun.transform.rotation = baseRotation;
    }
}
