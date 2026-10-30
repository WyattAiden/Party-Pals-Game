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

    // Start is called before the first frame update
    void Start()
    {
        int Palet = GetComponent<PlayerInput>().playerIndex;
        Color playercolor = GameSettings.playerColors[Palet];
        if (bodyrenderer != null) bodyrenderer.material.color = playercolor;
        if (modelRenderers != null)
            foreach (var r in modelRenderers)
                if (r != null) r.material.color = playercolor;

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
