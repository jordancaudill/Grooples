using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayer : NetworkBehaviour
{
    private GameObject lobbyPlayerGrid;
    public TextMeshProUGUI playerName;
    public Image playerImage;
    private MultiplayerManager multiplayerManager;

    public override void OnStartClient()
    {
        StartCoroutine(WaitForMultiplayerManager());
    }

    private void OnDestroy()
    {
        multiplayerManager._players.OnChange -= OnPlayersChange;
    }

    IEnumerator WaitForMultiplayerManager()
    {
        while (FindFirstObjectByType<MultiplayerManager>() == null)
            yield return null;

        multiplayerManager = FindFirstObjectByType<MultiplayerManager>();
        multiplayerManager._players.OnChange += OnPlayersChange;
        lobbyPlayerGrid = GameObject.Find("PlayerGrid");
        if (lobbyPlayerGrid != null)
        {
            transform.SetParent(lobbyPlayerGrid.transform, false);
        }
        transform.localScale = Vector3.one;
        if (IsOwner)
        {
            playerName.text = "Me";
        }
        else
        {
            playerName.text = "";
        }

        SetInitialColor();
    }

    private void SetInitialColor()
    {
        ColorUtility.TryParseHtmlString(
            PlayerColors.playerColors[multiplayerManager.GetPlayerColor(OwnerId)],
            out Color newPlayerColor
        );
        playerImage.color = newPlayerColor;
    }

    // Update the player image color when the player list changes
    private void OnPlayersChange(
        SyncListOperation op,
        int index,
        Player prev,
        Player next,
        bool asServer
    )
    {
        if (op == SyncListOperation.Set && next != null && next.clientId == OwnerId && !asServer)
        {
            //print(next.clientId + " | " + next.color);

            ColorUtility.TryParseHtmlString(
                PlayerColors.playerColors[next.color],
                out Color newPlayerColor
            );
            playerImage.color = newPlayerColor;
        }
    }
}
