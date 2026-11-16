using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] Image fill;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        health.Changed += OnChanged;
        fill.fillAmount = 1f;
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= OnChanged;
    }

    void OnChanged(float current, float max) => fill.fillAmount = current / max;

    void LateUpdate() => transform.forward = cam.transform.forward;
}