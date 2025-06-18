using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{

    PlayerController[] players = {};

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //NetworkManager.Singleton.StartHost();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddPlayer(PlayerController player) {        
        players.Append(player);
    }

    public void RemovePlayer(PlayerController player) {        
        players = players.Where(p => p != player).ToArray();
    }

}
