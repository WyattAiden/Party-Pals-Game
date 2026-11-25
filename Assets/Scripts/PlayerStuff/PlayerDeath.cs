using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] PlayerController controller;
    [SerializeField] CharacterController characterController;
    [SerializeField] GameObject visuals; // model  canvas
    [SerializeField] private LineRenderer aimline; // the line renderer aiming

    void OnEnable() => health.Died += OnDied;
    void OnDisable() => health.Died -= OnDied;
    [SerializeField] Renderer bodyRenderer;   // the capsule's MeshRenderer

    void OnDied()
    {
        controller.enabled = false;
        characterController.enabled = false;
        if (bodyRenderer != null) bodyRenderer.enabled = false;
        if (visuals != null) visuals.SetActive(false);
        aimline.enabled = false;
    }
}