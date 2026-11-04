using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapVote : MonoBehaviour
{

    [SerializeField] private Image[] optionImages;
    [SerializeField] private TMP_Text[] optionNames;
    [SerializeField] private Sprite[] mapSprites;


    public void ShowBallot()
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

    }
}
