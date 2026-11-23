using TMPro;
using UnityEngine;
using UnityEngine.UI;





public class HealthPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image Ring;
    [SerializeField] private Image panelArt;
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
        int chunks = Mathf.CeilToInt(current / max * 4);
        panelArt.sprite = panelFrames [4 - chunks];
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= OnChanged;
    }
}
