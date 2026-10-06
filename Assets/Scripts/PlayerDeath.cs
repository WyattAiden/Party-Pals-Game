using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] PlayerController controller;
    [SerializeField] CharacterController characterController;
    [SerializeField] GameObject visuals; // model + health bar canvas

    void OnEnable() => health.Died += OnDied;
    void OnDisable() => health.Died -= OnDied;
    [SerializeField] Renderer bodyRenderer;   // the capsule's MeshRenderer
    [SerializeField] GameObject healthBarCanvas;

    void OnDied()
    {
        controller.enabled = false;
        characterController.enabled = false;
        bodyRenderer.enabled = false;
        healthBarCanvas.SetActive(false);
    }
}