using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class UIController : NetworkBehaviour
{

    public GameObject levelPanel;
    public GameObject pauseCanvas; 
    public GameObject mainMenuButtons;
    public GameObject playButton;
    public GameObject connectedButtons;
    public GameObject unconnectedButtons;

    public NetworkManager _networkManager;

    InputAction pauseAction;
    private string ipInput;

    public bool isPaused = false;


    void Start()
    {
        _networkManager = FindFirstObjectByType<NetworkManager>();
        _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
        _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;

        pauseAction = InputSystem.actions.FindAction("Pause");
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
        _networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
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

    }

    public void HideLevelPanel()
    {
        levelPanel.SetActive(false);
        mainMenuButtons.SetActive(true);
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
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();

    }

    public void SetIPInput(string value)
    {
        ipInput = value;
    }

    public void ConnectToIP()
    {
        _networkManager.ClientManager.StartConnection(ipInput);
    }

    public void LeaveParty()
    {
        _networkManager.ClientManager.StopConnection();
        if (_networkManager.ServerManager.Started)
        {
            _networkManager.ServerManager.StopConnection(true);
        }

    }



    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        print(obj.ConnectionState);
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


    private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        //_serverState = obj.ConnectionState;
        //UpdateColor(obj.ConnectionState, ref _serverIndicator);
        print(obj.ConnectionState);
        if (playButton)
        {
            playButton.GetComponent<Button>().interactable = obj.ConnectionState == LocalConnectionState.Started;

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
