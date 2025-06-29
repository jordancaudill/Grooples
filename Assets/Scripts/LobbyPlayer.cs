using FishNet.Object;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    private GameObject lobbyPlayerGrid;



    void OnNetworkSpawn()
    {
        print("spawned");
        //lobbyPlayerGrid = GameObject.Find("PlayerGrid");
        //transform.SetParent(lobbyPlayerGrid.transform, false);
    }

    // Update is called once per frame
    void Update()
    {

    }



}
