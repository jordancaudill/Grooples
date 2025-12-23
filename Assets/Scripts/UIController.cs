using System.Threading.Tasks;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.UTP;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public GameObject levelPanel;
    public GameObject startGameButton;
    public GameObject copyCodeInputField;
    public GameObject enterCodeInputField;
    public GameObject pauseCanvas;
    public GameObject mainMenuButtons;
    public GameObject connectedButtons;
    public GameObject unconnectedButtons;
    public GameObject successScreen;
    public NetworkObject playerPrefab;
    public NetworkObject lobbyPlayerPrefab;
    public GameObject lobbyPlayerGrid;
    public NetworkObject gameManagerPrefab;
    private GameManager gameManager;
    public GameObject keyboardIcons;
    public GameObject exitButton;
    public GameObject gamepadIcons;
    public NetworkManager _networkManager;
    public PlayerInput playerInput;
    public FishNet.Managing.Scened.SceneManager sceneManager;
    public int levelCount = 5; // Assuming there are 5 levels in the game
    InputAction pauseAction;
    private string joinCode;
    public bool isPaused = false;

    void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        _networkManager = FindFirstObjectByType<NetworkManager>();

        _networkManager.ClientManager.OnClientConnectionState +=
            ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState +=
            ServerManager_OnServerConnectionState;

        sceneManager = FindFirstObjectByType<FishNet.Managing.Scened.SceneManager>();
        pauseAction = InputSystem.actions.FindAction("Pause");
        sceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
        SetConnectedButtonStates();
        SetCopyableJoinCode(gameManager?.joinCode?.Value);

#if UNITY_WEBGL
        // Disable exit and copy buttons on WebGL
        if (exitButton != null)
        {
            exitButton.SetActive(false);
        }
#endif
    }

    public void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        SetConnectedButtonStates();
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ClientManager.OnClientConnectionState -=
            ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -=
            ServerManager_OnServerConnectionState;
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

    public async void LoadLevel(int levelNumber)
    {
        // If not in a client-server session, start one up
        if (!_networkManager.ClientManager.Started)
        {
            await SetupGameAsHost();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        gameManager.currentLevel = levelNumber;

        LoadScene("Level" + levelNumber.ToString());
        if (successScreen != null)
        {
            successScreen.SetActive(false);
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
        }
        else
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
        pauseCanvas.SetActive(false);
        isPaused = false;
    }

    public void PauseGame()
    {
        pauseCanvas.SetActive(true);
        isPaused = true;
    }

    public void HostGame()
    {
        _ = SetupGameAsHost();
        // _networkManager.ServerManager.StartConnection();
        // _networkManager.ClientManager.StartConnection();
    }

    public void CopyJoinCodeToClipboard()
    {
        GUIUtility.systemCopyBuffer = copyCodeInputField.GetComponent<TMP_InputField>().text;
    }

    public async Task SetupGameAsHost()
    {
        string code = await StartHostWithRelay();
        await StartClientWithRelay(code);
    }

    public async Task<string> StartHostWithRelay()
    {
        //Initialize the Unity Services engine
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            //If not already logged, log the user in
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(8); // 8 is the max number of players
        UnityTransport transport = _networkManager.TransportManager.GetTransport<UnityTransport>();
        string connectionType;
#if UNITY_WEBGL
        connectionType = "wss";
        transport.UseWebSockets = true;
#else
        connectionType = "dtls";
        transport.UseWebSockets = false;
#endif
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        SetCopyableJoinCode(code);
        joinCode = code;
        return _networkManager.ServerManager.StartConnection() ? code : null;
    }

    public async Task<bool> StartClientWithRelay(string code)
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        var allocation = await RelayService.Instance.JoinAllocationAsync(code);
        UnityTransport transport = _networkManager.TransportManager.GetTransport<UnityTransport>();
        string connectionType;
#if UNITY_WEBGL
        connectionType = "wss";
        transport.UseWebSockets = true;
#else
        connectionType = "dtls";
        transport.UseWebSockets = false;
#endif
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
        SetCopyableJoinCode(code);
        joinCode = code;
        return !string.IsNullOrEmpty(code) && _networkManager.ClientManager.StartConnection();
    }

    public void ConnectToJoinCode()
    {
        Disconnect();
        _ = StartClientWithRelay(enterCodeInputField.GetComponent<TMP_InputField>().text);
    }

    public void SetCopyableJoinCode(string code)
    {
        if (copyCodeInputField != null)
        {
            copyCodeInputField.GetComponent<TMP_InputField>().text = code;
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

    public void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        SetConnectedButtonStates();
    }

    private void SetConnectedButtonStates()
    {
        if (connectedButtons && unconnectedButtons)
        {
            bool isInParty = _networkManager.ClientManager.Started;
            connectedButtons.SetActive(isInParty);
            unconnectedButtons.SetActive(!isInParty);
            startGameButton.GetComponent<Button>().interactable = _networkManager.IsServerStarted;
        }
    }

    public void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        if (obj.ConnectionState == LocalConnectionState.Started)
        {
            CreateGameManager();
        }
    }

    public void CreateGameManager()
    {
        NetworkObject gameManager = Instantiate(gameManagerPrefab);
        gameManager.name = "GameManager";
        _networkManager.ServerManager.Spawn(gameManager);
        gameManager.GetComponent<GameManager>().SetJoinCode(joinCode);
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
