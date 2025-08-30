using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using System.Linq;

public class Player
{
    public int clientId;
    public string color; // color name such as "pink"
}

public class GameManager : NetworkBehaviour
{

    public readonly SyncList<Player> _players = new SyncList<Player>();
    private NetworkManager _networkManager;
    public NetworkObject playerPrefab;
    public NetworkObject playerLobbyPrefab;
    public readonly SyncVar<int> currentLevel = new SyncVar<int>(0);

    public SceneManager sceneManager;

    void Start()
    {
        _networkManager = FindFirstObjectByType<NetworkManager>();
        _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        sceneManager = FindFirstObjectByType<SceneManager>();
        sceneManager.OnLoadEnd += SceneManager_OnLoadEnd;


    }

    private void OnDestroy()
    {
        _networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        sceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;

    }

    public void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        // Spawn player prefab when the scene is set
        if (args.LoadedScenes.Length > 0)
        {
            if (args.LoadedScenes[0].name == "Lobby")
            {
                SpawnPlayerLobbyPrefabs();
               

            }
            else
            {
              
                SpawnPlayerPrefabs();

            }
        }

    }

    public void ServerManager_OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs state)
    {
        if (state.ConnectionState == RemoteConnectionState.Started)
        {
            AddPlayer(connection.ClientId);
            if (currentLevel.Value > 0)
            {
                SpawnPlayerPrefab(connection.ClientId);
            } else
            {
                SpawnPlayerLobbyPrefab(connection.ClientId);
            }
        }
        else
        {
            RemovePlayer(connection.ClientId);
        }
    }


    [Server]
    public void SpawnPlayerPrefabs()
    {
        // Spawn player prefab for each player
        foreach (Player player in _players)
        {
            SpawnPlayerPrefab(player.clientId);
        }
    }

    [Server]
    public void SpawnPlayerPrefab(int clientId)
    {
        NetworkObject playerObject = Instantiate(playerPrefab);
        NetworkConnection conn = _networkManager.ServerManager.Clients.First(c => c.Key == clientId).Value;
        _networkManager.ServerManager.Spawn(playerObject, conn);
    }

    [Server]
    public void SpawnPlayerLobbyPrefab(int clientId)
    {
        NetworkObject playerObject = Instantiate(playerLobbyPrefab);
        NetworkConnection conn = _networkManager.ServerManager.Clients.First(c => c.Key == clientId).Value;
        _networkManager.ServerManager.Spawn(playerObject, conn);
    }



    [Server]
    public void SpawnPlayerLobbyPrefabs()
    {
        // Spawn player prefab for each player
        foreach (Player player in _players)
        {
            SpawnPlayerLobbyPrefab(player.clientId);
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
        // If no available colors, just return white.
        return "white";
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
        // If all colors are taken, return "white"
        return "white";
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
        // If all colors are taken, return "white"
        return "white";
    }

    public string GetPlayerColor(int clientId)
    {
        Player player = _players.Find(p => p.clientId == clientId);
        return player != null ? player.color : "white";
    }


}


