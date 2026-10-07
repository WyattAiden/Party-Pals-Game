using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
   [SerializeField] List<PlayerInput> players;
   [SerializeField] private float WinScreenDelay = 3f;
   [SerializeField] private int MinimumPlayersBeforeWin = 2;
   [SerializeField] List<PlayerInputManagerV2> playerInputManagerV2;

    private void OnEnable()
    {
        
    }

}
