using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MapVote : MonoBehaviour
{

    [SerializeField] private Image[] optionImages;
    [SerializeField] private TMP_Text[] optionNames;
    [SerializeField] private Sprite[] mapSprites;
    private int[] choices = new int[4];
    private bool[] locked = new bool[4];
    private int optionCount;
    private bool voting;
    private List<PlayerInput> voters;

    public void ShowBallot(List<PlayerInput> players)
    {

        for ( int i = 0; i < optionImages.Length; i++ )
        {
            bool hasMap = i < GameSettings.mapNames.Length;
            optionImages[i].gameObject.SetActive(hasMap);
            if (hasMap)
            {
                optionImages[i].sprite = mapSprites[i];
                optionNames[i].text = GameSettings.mapNames[i];
            }
        }

        optionCount = Mathf.Min(optionImages.Length, GameSettings.mapNames.Length);
        choices = new int[4];
        locked = new bool[4];
        voters = players;
        foreach (PlayerInput voter in players)
        {
            voter.actions["MenuNavigate"].Enable();
            voter.actions["Confirm"].Enable();

            
        }
        voting = true;
    }

    private void Update()
    {
        if (!voting) return;
        for (int p = 0; p < voters.Count; p++)
        {
            if (voters[p] == null || locked[p]) continue;
            InputAction navigate = voters[p].actions["MenuNavigate"];
            InputAction confirm = voters[p].actions["Confirm"];

            if (navigate.WasPressedThisFrame())
            {

                float x = navigate.ReadValue<Vector2>().x;
                if(x < -0.5f && choices[p] > 0) choices[p]--;
                if (x > 0.5f && choices[p] < optionCount - 1) choices[p]++;
                Debug.Log("P" + (p + 1) +" option " + choices[p] + " Locked " + locked[p]);
            }
            if (confirm.WasPressedThisFrame())
            {
                locked[p] = true;
                Debug.Log("P" + (p + 1) + " Locked " + locked[p]);
            }
        }
        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        for (int i = 0; i < optionCount; i++)
        {
            Transform markers = optionImages[i].transform.Find("Markers");

            for (int p = 0; p < 4; p++)
            {
                Image dot = markers.GetChild(p).GetComponent<Image>();
                if (p >= voters.Count) { dot.color = Color.clear; continue; }
                Color c = GameSettings.playerColors[p];
                if (choices[p] != i) c.a = 0f;
                else if (locked[p]) c.a = 1f;
                else
                    c.a = 0.35f;
                    
                
               
                dot.color = c;
            }

        }
    }
}
