using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MapVote : MonoBehaviour
{

    [SerializeField] private Image[] optionImages;
    [SerializeField] private TMP_Text[] optionNames;
    [SerializeField] private Sprite[] mapSprites;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float voteTime = 15f;
    private int optionCount;
    private int[] choices = new int[4];
    private bool[] locked = new bool[4];
    private bool voting;
    private float timeLeft;
    private List<PlayerInput> voters;
    private int[] ballot = new int[3];

    public void ShowBallot(List<PlayerInput> players)
    {
        List<int> hat = new List<int>();
        string current = SceneManager.GetActiveScene().name;
        
        for (int m = 0; m < GameSettings.mapNames.Length; m++)
        {
            if (GameSettings.mapNames[m] != current) hat.Add(m);
        }
        optionCount = Mathf.Min(optionImages.Length, hat.Count);

        for ( int i = 0; i < optionImages.Length; i++ )
        {
            bool hasMap = i < optionCount;
            optionImages[i].gameObject.SetActive(hasMap);
            if (hasMap)
            {
                int pick = Random.Range(0, hat.Count);
                ballot[i] = hat[pick];
                hat.RemoveAt(pick);
                optionImages[i].sprite = mapSprites[ballot[i]];
                optionNames[i].text = GameSettings.mapNames[ballot[i]];
            }
        }

        
        choices = new int[4];
        locked = new bool[4];
        voters = players;
        foreach (PlayerInput voter in players)
        {
            voter.actions["MenuNavigate"].Enable();
            voter.actions["Confirm"].Enable();

            
        }
        timeLeft = voteTime;
        voting = true;
    }

    private void Update()
    {
        if (!voting || Time.timeScale ==0f) return;
        timeLeft -= Time.deltaTime;
        timerText.text = Mathf.CeilToInt(timeLeft).ToString();

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
                //Debug.Log("P" + (p + 1) +" option " + choices[p] + " Locked " + locked[p]);
                //Debug.Log("List" + p + "playerIndex" + voters[p].playerIndex + "Pressed by" + navigate.activeControl.device.name);
            }
            if (confirm.WasPressedThisFrame())
            {
                locked[p] = true;
                //Debug.Log("List" + p + "playerIndex" + voters[p].playerIndex + "Pressed by" + confirm.activeControl.device.name);
                //Debug.Log("P" + (p + 1) + " Locked " + locked[p]);
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
                Color c = GameSettings.GetColors(p);
                if (choices[p] != i) c.a = 0f;
                else if (locked[p]) c.a = 1f;
                else
                    c.a = 0.35f;
                    
                
               
                dot.color = c;
            }

        }
    }

    public bool IsDone()
    {
        if (timeLeft <= 0f) return true;
        for (int p = 0; p < voters.Count; p++)
        if (!locked[p]) return false;
        return true;
    }

    public string GetWinningMap()
    {
        voting = false;
        int[] votes = new int[optionCount];

        for (int p = 0; p < voters.Count; p++)
            votes[choices[p]]++;
        
        int most = 0;
        for (int i = 0; i < optionCount; i++)
        if (votes[i] > most) most = votes[i];

        List<int> tied = new List<int>();
        for (int i = 0; i < optionCount; i++)
            if (votes[i] == most) tied.Add(i);

        int winner = tied[Random.Range(0, tied.Count)];

        return GameSettings.mapNames[ballot[winner]];
    }
}
