using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIController : MonoBehaviour
{

    public GameObject levelPanel;
    public GameObject pauseCanvas;
    public GameObject mainMenuButtons;
    public GameObject playButton;
    public GameObject connectedButtons;
    public GameObject unconnectedButtons;
    public GameObject successScreen;
    public NetworkObject playerPrefab;
    public NetworkObject lobbyPlayerPrefab;
    public GameObject lobbyPlayerGrid;
    public NetworkObject gameManagerPrefab;
    private GameManager gameManager;
    public GameObject keyboardIcons;
    public GameObject gamepadIcons;
    public NetworkManager _networkManager;
    public PlayerInput playerInput;
    public FishNet.Managing.Scened.SceneManager sceneManager;
    public int levelCount = 5; // Assuming there are 5 levels in the game

    InputAction pauseAction;
    private string ipInput;

    public bool isPaused = false;

    void Start()
    {
        _networkManager = FindFirstObjectByType<NetworkManager>();
        _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;

        sceneManager = FindFirstObjectByType<FishNet.Managing.Scened.SceneManager>();
        pauseAction = InputSystem.actions.FindAction("Pause");
        sceneManager.OnLoadEnd += SceneManager_OnLoadEnd;

    }

    public void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {

        if (connectedButtons != null && unconnectedButtons != null)
        {
            if (_networkManager.ClientManager.Connection.IsValid)
            {
                unconnectedButtons.SetActive(false);
                connectedButtons.SetActive(true);
                if (_networkManager.IsServerStarted)
                {
                    playButton.GetComponent<Button>().interactable = true;
                }

            }
            else
            {
                unconnectedButtons.SetActive(true);
                connectedButtons.SetActive(false);
            }

        }


    }



    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
        sceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;

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
            bool isGamepad = playerInput.currentControlScheme == "Gamepad";
            gamepadIcons.SetActive(isGamepad && _networkManager.ClientManager.Started);
            keyboardIcons.SetActive(!isGamepad && _networkManager.ClientManager.Started);

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

    public void ShowLevelPanel()
    {
        levelPanel.SetActive(true);
        mainMenuButtons.SetActive(false);

    }

    public void HideLevelPanel()
    {
        levelPanel.SetActive(false);
        mainMenuButtons.SetActive(true);
    }


    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        sceneManager.LoadGlobalScenes(sld);
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
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        if (gameManager.currentLevel >= levelCount) // Assuming 5 is the last level
        {
            LoadScene("Lobby");
        } else
        {
            gameManager.currentLevel++;
            LoadScene("Level" + gameManager.currentLevel.ToString());
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
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();

    }

    public void SetIPInput(string value)
    {
        ipInput = value;
    }

    public void ConnectToIP()
    {
        if (ipInput == null)
        {
            _networkManager.ClientManager.StartConnection();
        }
        else
        {
            _networkManager.ClientManager.StartConnection(ipInput);
        }
    }

    public void LeaveParty()
    {
        _networkManager.ClientManager.StopConnection();
        if (_networkManager.ServerManager.Started)
        {
            _networkManager.ServerManager.StopConnection(true);
        }

    }



    public void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        if (connectedButtons && unconnectedButtons)
        {
            if (obj.ConnectionState == LocalConnectionState.Started)
            {
                unconnectedButtons.SetActive(false);
                connectedButtons.SetActive(true);
            }
            else
            {
                unconnectedButtons.SetActive(true);
                connectedButtons.SetActive(false);
            }

        }
    }

    public void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        if (obj.ConnectionState == LocalConnectionState.Started)
        {
            NetworkObject gameManager = Instantiate(gameManagerPrefab);
            gameManager.name = "GameManager";
            _networkManager.ServerManager.Spawn(gameManager);

            if (playButton)
            {
                playButton.GetComponent<Button>().interactable = obj.ConnectionState == LocalConnectionState.Started;
            }

        }

    }

    public void OnNextColor(InputAction.CallbackContext context)
    {

        if (context.performed)
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            if (gameManager)
            {
                gameManager.SetNextColorRpc();
            }
        }
    }

    public void OnPreviousColor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {

            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            if (gameManager)
            {
                gameManager.SetPreviousColorRpc();
            }
        }
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
