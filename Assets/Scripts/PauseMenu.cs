using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("pause menu")]
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject firstSelectedButton;
    private bool isPaused;


    void Update()
    {
        if (GameSettings.Players.Count == 0) return;
        Gamepad pad = GameSettings.Players[0].device as Gamepad;

        if (pad !=null && pad.startButton.wasPressedThisFrame)
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }

        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pauseScreen.SetActive(false);
    }

    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        pauseScreen.SetActive(true);
        firstSelectedButton.GetComponent<Button>().Select();

    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        LoadSceneByName("StartScreen");
    }


    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
