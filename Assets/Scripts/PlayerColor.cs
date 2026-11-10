using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerColor : MonoBehaviour
{
    [SerializeField] private Color[] playerColor;
    [SerializeField] private Renderer bodyrenderer;
    [Tooltip("Extra renderers (e.g. the puppet model parts) to tint with the player colour")]
    [SerializeField] private Renderer[] modelRenderers;

    [Header("Damage Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;

    private Health health;
    private Color baseColor;
    private Coroutine flashRoutine;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;

        // If we get disabled mid-flash, don't leave the player stuck white.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            SetColor(baseColor);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        int Palet = GetComponent<PlayerInput>().playerIndex;
        baseColor = GameSettings.GetColors(Palet);
        SetColor(baseColor);
    }

    void SetColor(Color c)
    {
        if (bodyrenderer != null) bodyrenderer.material.color = c;
        if (modelRenderers != null)
            foreach (var r in modelRenderers)
                if (r != null) r.material.color = c;
    }

    void OnDamaged(float amount, GameObject source)
    {
        if (amount <= 0f) return;

        // Restart the flash if hit again while already flashing.
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        SetColor(flashColor);
        yield return new WaitForSeconds(flashDuration);
        SetColor(baseColor);
        flashRoutine = null;
    }
}
