using System.Collections;
using System.Linq;
using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Unity.Cinemachine;
using UnityEngine;

public class Player
{
    public int clientId;
    public string color; // color name such as "pink"
}

public class MultiplayerManager : NetworkBehaviour
{
    public static MultiplayerManager Instance { get; private set; }
    public readonly SyncList<Player> _players = new SyncList<Player>();
    public NetworkObject playerPrefab;
    public NetworkObject playerLobbyPrefab;
    public int currentLevel = 1;
    public readonly SyncVar<string> joinCode = new SyncVar<string>();
    private CinemachineTargetGroup cinemachineTargetGroup;

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Despawn();
            return;
        }

        Instance = this;

        NetworkManager.ServerManager.OnRemoteConnectionState +=
            ServerManager_OnRemoteConnectionState;
        NetworkManager.ServerManager.OnServerConnectionState +=
            ServerManager_OnServerConnectionState;
        NetworkManager.ClientManager.OnAuthenticated += ClientManager_OnAuthenticated;

        SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
    }

    private void OnDestroy()
    {
        NetworkManager.ServerManager.OnRemoteConnectionState -=
            ServerManager_OnRemoteConnectionState;
        NetworkManager.ServerManager.OnServerConnectionState -=
            ServerManager_OnServerConnectionState;
        NetworkManager.ClientManager.OnAuthenticated -= ClientManager_OnAuthenticated;
        SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
    }

    public override void OnStartClient()
    {
        StartCoroutine(WaitForCinemachineTargetGroup());
    }

    public void SetJoinCode(string code)
    {
        joinCode.Value = code;
    }

    public void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        // Spawn player prefab when the scene is set
        if (args.LoadedScenes.Length > 0)
        {
            StartCoroutine(WaitForCinemachineTargetGroup());

            PlayerSpawner playerSpawner = FindFirstObjectByType<PlayerSpawner>();

            if (args.LoadedScenes[0].name == "Lobby")
            {
                if (GameObject.Find("PlayerGrid") == null)
                    return;
                playerSpawner.Spawns[0] = GameObject.Find("PlayerGrid").transform;
                playerSpawner.SetPlayerPrefab(playerLobbyPrefab);
                SpawnPlayerLobbyPrefabs();
            }
            else
            {
                playerSpawner.Spawns[0] = GameObject.Find("Spawn").transform;
                playerSpawner.SetPlayerPrefab(playerPrefab);

                SpawnPlayerPrefabs();
            }
        }
    }

    IEnumerator WaitForCinemachineTargetGroup()
    {
        while (FindFirstObjectByType<CinemachineTargetGroup>() == null)
            yield return null;

        cinemachineTargetGroup = FindFirstObjectByType<CinemachineTargetGroup>();

        // Add the spawn point and finish point to the cinemachine target group
        // Wait 2 seconds
        yield return new WaitForSeconds(2f);

        GameObject spawn = GameObject.Find("Spawn");
        GameObject finish = GameObject.Find("Finish");
        if (spawn != null && finish != null)
        {
            cinemachineTargetGroup.AddMember(spawn.transform, 1f, 1f);
            cinemachineTargetGroup.AddMember(finish.transform, 1f, 1f);
        }
    }

    public void ServerManager_OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs state
    )
    {
        if (state.ConnectionState == RemoteConnectionState.Started)
        {
            AddPlayer(connection.ClientId);
        }
        else if (state.ConnectionState == RemoteConnectionState.Stopped)
        {
            RemovePlayer(connection.ClientId);
        }
    }

    public void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        Debug.Log("woohoo");
    }

    public void ClientManager_OnAuthenticated()
    {
        Debug.Log("Client authenticated: ");
        // AddPlayer(1);
    }

    [Server]
    public void SpawnPlayerPrefabs()
    {
        // Spawn player prefab for each player
        foreach (Player player in _players)
        {
            NetworkObject playerObject = Instantiate(playerPrefab);
            //playerObject.GetComponent<PlayerMovement>().clientId = player.clientId; // Set clientId on the player object
            //playerObject.GetComponent<PlayerMovement>().color = player.color; // Set color on the player object
            // Get the client connection for the player
            NetworkConnection conn = NetworkManager
                .ServerManager.Clients.First(c => c.Key == player.clientId)
                .Value;

            NetworkManager.ServerManager.Spawn(playerObject, conn);
        }
    }

    [Server]
    public void SpawnPlayerLobbyPrefabs()
    {
        // Spawn player prefab for each player
        foreach (Player player in _players)
        {
            NetworkObject playerObject = Instantiate(playerLobbyPrefab);
            //playerObject.GetComponent<PlayerMovement>().clientId = player.clientId; // Set clientId on the player object
            //playerObject.GetComponent<PlayerMovement>().color = player.color; // Set color on the player object
            // Get the client connection for the player
            NetworkConnection conn = NetworkManager
                .ServerManager.Clients.First(c => c.Key == player.clientId)
                .Value;

            NetworkManager.ServerManager.Spawn(playerObject, conn);
        }
    }

    [Server]
    public void AddPlayer(int newClientId)
    {
        Player newPlayer = new Player();
        newPlayer.clientId = newClientId;
        newPlayer.color = GetFirstAvailableColor();
        _players.Add(newPlayer);
        //Call dirty on an index after modifying an entries field to force a synchronize.
        _players.Dirty(0);
    }

    [Server]
    public void RemovePlayer(int clientId)
    {
        // Find the player with the given clientId
        int index = _players.FindIndex(p => p.clientId == clientId);
        if (index >= 0)
        {
            _players.RemoveAt(index);
            // Call dirty on an index after modifying an entries field to force a synchronize.
            if (_players.Count > 0)
                _players.Dirty(0);
        }
    }

    // Sets the given player's color to the next available color
    [ServerRpc(RequireOwnership = false)]
    public void SetNextColorRpc(NetworkConnection conn = null)
    {
        // Find the player with the given clientId
        int index = _players.FindIndex(p => p.clientId == conn.ClientId);
        if (index >= 0)
        {
            Player player = _players[index];
            string nextColor = GetNextAvailableColor(player.color);
            player.color = nextColor;
            _players[index] = player;
            _players.Dirty(index);
        }
    }

    // Sets the given player's color to the previous available color
    [ServerRpc(RequireOwnership = false)]
    public void SetPreviousColorRpc(NetworkConnection conn = null)
    {
        // Find the player with the given clientId
        int index = _players.FindIndex(p => p.clientId == conn.ClientId);
        if (index >= 0)
        {
            Player player = _players[index];
            string nextColor = GetPreviousAvailableColor(player.color);
            player.color = nextColor;
            _players[index] = player;
            _players.Dirty(index);
        }
    }

    public string GetFirstAvailableColor()
    {
        foreach (var color in PlayerColors.playerColors)
        {
            Player foundExistingPlayerWithColor = _players.Find(x => x.color == color.Key);
            if (foundExistingPlayerWithColor == null)
            {
                // color is available
                return color.Key;
            }
        }
        // If no available colors, just return pink.
        return "pink";
    }

    public string GetNextAvailableColor(string currentColor)
    {
        // Get all color keys in a list for index-based access
        var colorKeys = PlayerColors.playerColors.Keys.ToList();
        int colorCount = colorKeys.Count;
        int currentIndex = colorKeys.IndexOf(currentColor);

        // Start searching from the next color after currentColor
        for (int i = 1; i <= colorCount; i++)
        {
            int nextIndex = (currentIndex + i) % colorCount;
            string nextColor = colorKeys[nextIndex];
            // Check if this color is already taken
            if (_players.Find(x => x.color == nextColor) == null)
            {
                return nextColor;
            }
        }
        // If all colors are taken, return "pink"
        return "pink";
    }

    public string GetPreviousAvailableColor(string currentColor)
    {
        // Get all color keys in a list for index-based access
        var colorKeys = PlayerColors.playerColors.Keys.ToList();
        int colorCount = colorKeys.Count;
        int currentIndex = colorKeys.IndexOf(currentColor);

        // Start searching from the previous color before currentColor
        for (int i = 1; i <= colorCount; i++)
        {
            int prevIndex = (currentIndex - i + colorCount) % colorCount;
            string prevColor = colorKeys[prevIndex];
            // Check if this color is already taken
            if (_players.Find(x => x.color == prevColor) == null)
            {
                return prevColor;
            }
        }
        // If all colors are taken, return "pink"
        return "pink";
    }

    public string GetPlayerColor(int clientId)
    {
        Player player = _players.Find(p => p.clientId == clientId);
        return player != null ? player.color : "pink";
    }
}
