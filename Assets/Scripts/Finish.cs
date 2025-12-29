using System.Collections;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

public class Finish : NetworkBehaviour
{
    private MultiplayerManager multiplayerManager;
    private HashSet<PlayerController> playersInFinish = new HashSet<PlayerController>();
    private UIController uiController;
    private bool isFinished = false;
    public GameObject platform;
    private NetworkManager _networkManager;
    private float timeOnPlatform = 0f;
    public Color startColor;
    public Color endColor;
    private float timeToWin = 3f; // Time required on platform to win

    void Start()
    {
        uiController = FindFirstObjectByType<UIController>();

        multiplayerManager = FindFirstObjectByType<MultiplayerManager>();

        _networkManager = FindFirstObjectByType<NetworkManager>();
    }

    private void FixedUpdate()
    {
        CheckForPlayersOnPlatform();
        UpdatePlatformColor();
        CheckForFinish();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider belongs to a player
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            playersInFinish.Add(player);
        }
    }

    private void UpdatePlatformColor()
    {
        // Update platform color gradient based on timeOnPlatform
        float t = Mathf.Clamp01(timeOnPlatform / timeToWin);
        Color currentColor = Color.Lerp(startColor, endColor, t);
        platform.GetComponent<SpriteRenderer>().color = currentColor;
    }

    private void CheckForPlayersOnPlatform()
    {
        if (playersInFinish.Count == multiplayerManager._players.Count)
        {
            timeOnPlatform += Time.fixedDeltaTime;
        }
        else
        {
            timeOnPlatform = 0f; // Reset timer if no players are on the platform
        }
    }

    private int GetClientIdFromPlayerController(PlayerController pc)
    {
        // Assuming PlayerController has a reference to its NetworkObject
        if (pc != null && pc.IsOwner)
        {
            int clientId = pc.Owner.ClientId;
            return clientId;
        }
        return -1; // Return -1 if not found or not owner
    }

    private void CheckForFinish()
    {
        // Check for final finish condition
        if (
            _networkManager.IsServerStarted
            && !isFinished
            && playersInFinish.Count == multiplayerManager._players.Count
            && timeOnPlatform >= timeToWin
        )
        {
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
            playersInFinish.Remove(player);
        }
    }
}
