using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayer : NetworkBehaviour
{
    private GameObject lobbyPlayerGrid;
    public TextMeshProUGUI playerName;
    public Image playerImage;
    private GameManager gameManager;


    public override void OnStartClient()
    {
        lobbyPlayerGrid = GameObject.Find("PlayerGrid");
        transform.SetParent(lobbyPlayerGrid.transform, false);
        transform.localScale = Vector3.one;
        if (IsOwner)
        {
            playerName.text = "Me";
        }
        else
        {
            playerName.text = "";
        }
        print("OnStartClient");
        StartCoroutine(WaitForGameManager());
    }


    private void OnDestroy()
    {
        gameManager._players.OnChange -= OnPlayersChange;
    }

    IEnumerator WaitForGameManager()
    {
        while (FindFirstObjectByType<GameManager>() == null)
            yield return null;

        gameManager = FindFirstObjectByType<GameManager>();
        gameManager._players.OnChange += OnPlayersChange;
        SetInitialColor();
    }


    private void SetInitialColor()
    {
        
        ColorUtility.TryParseHtmlString(PlayerColors.playerColors[gameManager.GetPlayerColor(OwnerId)], out Color newPlayerColor);
        playerImage.color = newPlayerColor;

    }

    // Update the player image color when the player list changes
    private void OnPlayersChange(SyncListOperation op, int index,
    Player prev, Player next, bool asServer)
    {
        if (op == SyncListOperation.Set && next != null && next.clientId == OwnerId && !asServer)
        {
            //print(next.clientId + " | " + next.color);

            ColorUtility.TryParseHtmlString(PlayerColors.playerColors[next.color], out Color newPlayerColor);
            playerImage.color = newPlayerColor;
           
        }
    }

}
