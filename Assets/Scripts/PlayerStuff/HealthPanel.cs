using TMPro;
using UnityEngine;
using UnityEngine.UI;





public class HealthPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image Ring;
    [SerializeField] private Image panelArt;
    [SerializeField] private Image scribbleLayer;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Sprite[] panelFrames;

    private Health health;

    public void Setup(Health h, Color color, int number)
    {
        health = h;
        h.Changed += OnChanged;
        Ring.color = color;
        nameText.text = "Player " + number;
        panelArt.sprite = panelFrames[0];
    }

    void OnChanged(float current, float max)
    {
        float exact = current / max * 4;
        int chunks = Mathf.CeilToInt(exact);
        float progress = chunks - exact;
        panelArt.sprite = panelFrames [4 - chunks];
        if (chunks > 0)
        {
            scribbleLayer.sprite = panelFrames[5 - chunks];
            Color c = scribbleLayer.color;
            c.a = progress;
            scribbleLayer.color = c;
        }
        else
        {
            panelArt.sprite = panelFrames[5];
            Color c = scribbleLayer.color;
            c.a = 0;
            scribbleLayer.color = c;
        }
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= OnChanged;
    }
}
