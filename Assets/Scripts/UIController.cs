using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public GameObject pauseCanvas;
    public Slider volumeSlider;
    public GameObject successScreen;
    public GameObject keyboardIcons;

    // public PlayerInput playerInput;
    public int levelCount = 5; // Assuming there are 5 levels in the game
    InputAction pauseAction;
    public bool isPaused = false;

    private static NetworkManager _networkManager;
    private static FishNet.Managing.Scened.SceneManager _sceneManager;
    private static MultiplayerManager _multiplayerManager;

    void Start()
    {
        _networkManager = NetworkManager.Instances[0];
        _sceneManager = _networkManager.SceneManager;
        _multiplayerManager = MultiplayerManager.Instance;
        pauseAction = InputSystem.actions.FindAction("Pause");

#if !UNITY_WEBGL
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
        }
#endif
    }

    // Update is called once per frame
    void Update()
    {
        CheckForPause();
        CheckUIButtons();
    }

    void CheckUIButtons()
    {
        if (keyboardIcons != null)
        {
            // bool isGamepad = playerInput.currentControlScheme == "Gamepad";
            // gamepadIcons.SetActive(isGamepad && _networkManager.ClientManager.Started);
            keyboardIcons.SetActive(true);
        }
    }

    void CheckForPause()
    {
        if (pauseCanvas != null && pauseAction.WasPressedThisFrame())
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

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _sceneManager.LoadGlobalScenes(sld);
    }

    public void QuitToLobby()
    {
        if (_networkManager.ServerManager.IsAnyServerStarted())
        {
            LoadScene("Lobby");
        }
        else
        {
            LeaveParty();
            // Load scene locally
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }
    }

    public void LoadNextLevel()
    {
        if (_multiplayerManager.currentLevel >= levelCount) // Assuming 5 is the last level
        {
            LoadScene("Lobby");
        }
        else
        {
            _multiplayerManager.currentLevel++;
            LoadScene("Level" + _multiplayerManager.currentLevel.ToString());
            successScreen.SetActive(false);
        }
    }

    public void ShowSuccessScreen()
    {
        if (successScreen != null)
        {
            successScreen.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        pauseCanvas.SetActive(false);
        isPaused = false;
    }

    public void PauseGame()
    {
        pauseCanvas.SetActive(true);
        isPaused = true;
    }

    public void OnVolumeChange()
    {
        if (volumeSlider != null)
        {
            AudioListener.volume = volumeSlider.value;
        }
    }

    public void LeaveParty()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        _networkManager.ClientManager.StopConnection();
        if (_networkManager.ServerManager.Started)
        {
            _networkManager.ServerManager.StopConnection(true);
        }
    }

    public void ExitGame()
    {
#if UNITY_WEBGL
        return;
#endif

#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
