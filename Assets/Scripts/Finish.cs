using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Finish : NetworkBehaviour
{
    private GameManager gameManager;
    private HashSet<GameObject> playersInFinish = new HashSet<GameObject>();
    private UIController uiController;

    void Start()
    {
        uiController = FindFirstObjectByType<UIController>();

        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider belongs to a player
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {

            playersInFinish.Add(player.gameObject);

            // If all players are inside, go to next level
            if (playersInFinish.Count == gameManager._players.Count)
            {
                uiController.ShowSuccessScreen();
                // Wait 5 seconds then load the next level
                StartCoroutine(LoadNextLevelAfterDelay(5f));

            }
        }
    }

    IEnumerator LoadNextLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Load the next level
        uiController.LoadNextLevel();
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
