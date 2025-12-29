using System.Threading.Tasks;
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
using UnityEngine.UI;

public class LobbyUIController : MonoBehaviour
{
    public GameObject levelPanel;
    public GameObject startGameButton;
    public GameObject copyCodeInputField;
    public GameObject enterCodeInputField;
    public GameObject mainMenuButtons;
    public GameObject connectedButtons;
    public GameObject unconnectedButtons;
    public GameObject exitButton;
    public GameObject copyButton;
    private string joinCode;

    public MultiplayerManager MultiplayerManagerPrefab;
    private static NetworkManager _networkManager;
    private static SceneManager _sceneManager;
    private static MultiplayerManager _multiplayerManager;

    void Start()
    {
        _networkManager = NetworkManager.Instances[0];
        _sceneManager = _networkManager.SceneManager;

        _networkManager.ClientManager.OnClientConnectionState +=
            ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState +=
            ServerManager_OnServerConnectionState;

        _sceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
        SetConnectedButtonStates();
        SetCopyableJoinCode(_multiplayerManager?.joinCode?.Value);

#if !UNITY_WEBGL
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
        }
#endif

#if UNITY_WEBGL
        if (exitButton != null)
        {
            exitButton.SetActive(false);
        }
        if (copyButton != null)
        {
            copyButton.SetActive(false);
        }
#endif
    }

    public void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        SetConnectedButtonStates();
    }

    private void OnDestroy()
    {
        _networkManager.ClientManager.OnClientConnectionState -=
            ClientManager_OnClientConnectionState;
        _networkManager.ServerManager.OnServerConnectionState -=
            ServerManager_OnServerConnectionState;
        _sceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
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

    private void CreateMultiplayerManager()
    {
        if (FindFirstObjectByType<MultiplayerManager>() == null)
        {
            GameObject gmInstance = Instantiate(MultiplayerManagerPrefab.gameObject);
            _networkManager.ServerManager.Spawn(gmInstance);
            _multiplayerManager = gmInstance.GetComponent<MultiplayerManager>();
        }
    }

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _sceneManager.LoadGlobalScenes(sld);
    }

    public async void LoadLevel(int levelNumber)
    {
        // If not in a client-server session, start one up
        if (!_networkManager.ClientManager.Started)
        {
            await SetupGameAsHost();
        }

        _multiplayerManager.currentLevel = levelNumber;

        LoadScene("Level" + levelNumber.ToString());
    }

    public void HostGame()
    {
        _ = SetupGameAsHost();
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
            CreateMultiplayerManager();
            _multiplayerManager.SetJoinCode(joinCode);
        }
    }

    public void OnNextColor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _multiplayerManager.SetNextColorRpc();
        }
    }

    public void OnPreviousColor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _multiplayerManager.SetPreviousColorRpc();
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
