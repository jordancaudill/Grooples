using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Grapple")]
    public GrappleGun grappleGun;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask terrainLayer;

    [Header("Bounds Check")]
    public LayerMask boundsLayer;
    private Transform spawnPoint;

    private Rigidbody2D rb;

    public bool isGrounded;
    private Vector2 moveInput;

    private NetworkObject networkObject;
    public PlayerInput playerInput;
    public PlayerInputManager playerInputManager;
    public int playerNumber;
    // Sync moveInput from owner to server
    private NetworkVariable<Vector2> syncedMoveInput = new NetworkVariable<Vector2>(writePerm: NetworkVariableWritePermission.Owner);

    GameManager gameManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        playerNumber = gameManager.players.Count;
        name = "Player_" + playerNumber;
        gameManager.AddPlayer(name);

    }

    void Start()
    {
        spawnPoint = GameObject.Find("Spawn").transform;

        if (!IsHost && !IsServer && !IsClient)
        {
            // New local player probably joined so we need to spawn the NetworkObject
            networkObject = GetComponent<NetworkObject>();
            networkObject.Spawn();
        }
        playerInputManager = GameObject.Find("PlayerInputManager").GetComponent<PlayerInputManager>();
        transform.position = spawnPoint.position;
        AssignSpriteToPlayer();

        rb = GetComponent<Rigidbody2D>();
    }


    public void OnLook(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme == "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
            syncedMoveInput.Value = moveInput;
        }

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme != "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
            syncedMoveInput.Value = moveInput;
        }


    }

    void FixedUpdate()
    {
        // Check if grounded
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, terrainLayer);

        if (!IsServer) return; // Only the server moves the player
        
        if (rb == null) return;

        if (CheckOutOfBounds())
        {
            rb.position = spawnPoint.position; // Reset position if out of bounds
            rb.linearVelocity = Vector2.zero;
        }
        else
        {

            // Apply horizontal movement based on most recent synced input
            rb.AddForce(new Vector2(syncedMoveInput.Value.x * moveSpeed, 0), ForceMode2D.Force);
        }
    }

    bool CheckOutOfBounds()
    {
        return Physics2D.OverlapCircle(rb.position, 1f, boundsLayer);
    }

    private void AssignSpriteToPlayer()
    {
        Dictionary<int, string> playerSprites = new Dictionary<int, string>
        {
            { 0, "Sprites/player_pink" },
            { 1, "Sprites/player_green" },
            { 2, "Sprites/player_yellow" },
            { 3, "Sprites/player_purple" },
        };
        string playerSprite = playerSprites[playerNumber];
        Sprite resource = Resources.Load<Sprite>(playerSprite);
        GetComponent<SpriteRenderer>().sprite = resource;
    }
}