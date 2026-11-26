using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private HealthPanel[] panels;

    private void Awake()
    {
        foreach (var panel in panels)
        {
            panel.gameObject.SetActive(false);
        }
            
    }

    private void OnEnable()
    {
        PlayerInputManagerV2.OnPlayerJoined += OnPlayerJoined;
    }

    private void OnDisable()
    {
        PlayerInputManagerV2.OnPlayerJoined -= OnPlayerJoined;
    }

    private void OnPlayerJoined(PlayerInput playerInput)
    {
        int index = GameSettings.Players.FindIndex(p => p.device == playerInput.devices[0]);
        if (index >= 0 && index < panels.Length)
        {
            var panel = panels[index];
            panel.gameObject.SetActive(true);
            panel.Setup(playerInput.GetComponent<Health>(), GameSettings.GetColors(index), index + 1);
            panel.GetComponent<AmmoDisplay>().Setup(playerInput.GetComponent<PlayerWeaponHandler>());
        }
    }
}
