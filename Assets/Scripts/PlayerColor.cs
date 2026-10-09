using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerColor : MonoBehaviour
{
    [SerializeField] private Color[] playerColor;
    [SerializeField] private Renderer bodyrenderer;

    // Start is called before the first frame update
    void Start()
    {
        int Palet = GetComponent<PlayerInput>().playerIndex;
        Color playercolor = playerColor[Palet];
        bodyrenderer.material.color = playercolor;

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
