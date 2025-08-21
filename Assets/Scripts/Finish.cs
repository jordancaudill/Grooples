using FishNet.Managing;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Finish : NetworkBehaviour
{
    private GameManager gameManager;
    private HashSet<GameObject> playersInFinish = new HashSet<GameObject>();
    private UIController uiController;
    private bool isFinished = false;
    private NetworkManager _networkManager;
    void Start()
    {
        uiController = FindFirstObjectByType<UIController>();

        gameManager = FindFirstObjectByType<GameManager>();

        _networkManager = FindFirstObjectByType<NetworkManager>();

    }

    private void Update()
    {
        CheckForFinish();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider belongs to a player
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            playersInFinish.Add(player.gameObject);
        }
    }

    private void CheckForFinish()
    {
        if (_networkManager.IsServerStarted && !isFinished && playersInFinish.Count == gameManager._players.Count)
        {
            // If all player inside the finish area and are not moving, show success screen
            foreach (GameObject p in playersInFinish)
            {
                PlayerController pc = p.GetComponent<PlayerController>();
                if (pc != null && pc.rb.linearVelocity.magnitude > 0.5f)
                {
                    return; // If any player is moving, do not proceed
                }
            }
            isFinished = true;
            ObserverRpcShowSuccessScreen();
            // Wait 5 seconds then load the next level
            StartCoroutine(LoadNextLevelAfterDelay(5f));


        }
    }

    IEnumerator LoadNextLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Load the next level
        uiController.LoadNextLevel();
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserverRpcShowSuccessScreen()
    {
        uiController.ShowSuccessScreen();

    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            playersInFinish.Remove(player.gameObject);
        }
    }

}
