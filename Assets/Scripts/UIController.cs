using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIController : MonoBehaviour
{

    public GameObject levelPanel;
    public GameObject pauseCanvas; 
    public GameObject mainMenuButtons;
    public GameObject playButton;
    public GameObject level1Button;

    InputAction pauseAction;

    public bool isPaused = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pauseAction = InputSystem.actions.FindAction("Pause");
    }

    // Update is called once per frame
    void Update()
    {
        CheckForPause();
    }

    void CheckForPause()
    {
        if (pauseCanvas != null && pauseAction.WasPressedThisFrame() )
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

    public void ShowLevelPanel()
    {
        levelPanel.SetActive(true);
        mainMenuButtons.SetActive(false);
        //EventSystem.current.SetSelectedGameObject(null);
        //EventSystem.current.SetSelectedGameObject(level1Button.gameObject);

    }

    public void HideLevelPanel()
    {
        levelPanel.SetActive(false);
        mainMenuButtons.SetActive(true);
        //EventSystem.current.SetSelectedGameObject(null);
        //EventSystem.current.SetSelectedGameObject(playButton.gameObject);


    }

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        pauseCanvas.SetActive(false);
        isPaused = false;
    }
    public void PauseGame()
    {

        Time.timeScale = 0;
        pauseCanvas.SetActive(true);
        isPaused = true;

    }

    public void HostGame()
    {
        //NetworkManager.Singleton.StartHost();
        ResumeGame();
    }

    public void JoinGame()
    {
        //NetworkManager.Singleton.StartClient();
        ResumeGame();

    }


    public void ExitGame()
    {
#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

}
