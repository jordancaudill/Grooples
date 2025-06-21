using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

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

    public Rigidbody2D rb;

    public bool isGrounded;
    private Vector2 moveInput;

    private NetworkObject networkObject;
    public PlayerInput playerInput;
    public PlayerInputManager playerInputManager;
    public int playerNumber;


    GameManager gameManager;

    private float reconciliationTime = 0.5f;
    public Vector2 lastServerPosition;
    public float lastServerRotation;
    public Vector2 lastServerVelocity;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        playerNumber = gameManager.players.Count;
        name = "Player_" + playerNumber;
        gameManager.AddPlayer(name);
        AssignSpriteToPlayer();

        if (IsServer)
        {
            lastServerPosition = rb.position;
            lastServerRotation = rb.rotation;
            lastServerVelocity = rb.linearVelocity;
        }

    }

    void Start()
    {
        playerInputManager = GameObject.Find("PlayerInputManager").GetComponent<PlayerInputManager>();
        spawnPoint = GameObject.Find("Spawn").transform;
        rb.position = spawnPoint.position;

        if (!IsHost && !IsServer && !IsClient)
        {
            // New local player probably joined so we need to spawn the NetworkObject
            networkObject = GetComponent<NetworkObject>();
            networkObject.Spawn();
        }
    }


    public void OnLook(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme == "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
        }

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme != "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
        }


    }

    void FixedUpdate()
    {
        if (rb == null) return;

        ReconcileTransform();

        if (IsOwner)
        {
            // Check if grounded
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, terrainLayer);

            if (CheckOutOfBounds())
            {
                UpdatePlayerTransformPredictive(spawnPoint.position, 0f, Vector2.zero);
            }
            MovePlayerPredictive(moveInput);
            
        }


    }

    // Position and Rotation Reconciliation
    private void ReconcileTransform()
    {
        if (!IsServer)
        {
            // On non-owner clients, interpolate to server position for smoothness
            rb.position = Vector3.Lerp(transform.position, lastServerPosition, reconciliationTime);
            rb.rotation = Mathf.LerpAngle(rb.rotation, lastServerRotation, reconciliationTime);
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, lastServerVelocity, reconciliationTime);
        }

    }

    // MovePlayer
    public void MovePlayer(Vector2 input)
    {
        rb.AddForce(new Vector2(input.x * moveSpeed, 0), ForceMode2D.Force);
    }

    public void MovePlayerPredictive(Vector2 input)
    {
        if (IsOwner)
        {
            MovePlayer(input);
            MovePlayerServerRpc(input);
        }
    }

    [Rpc(SendTo.Server)]
    public void MovePlayerServerRpc(Vector2 input)
    {
        MovePlayer(input);
        UpdatePlayerTransformClientRpc(rb.position, rb.rotation, rb.linearVelocity);
    }

    // UpdatePlayerTransform
    public void UpdatePlayerTransform(Vector2 newPosition, float newRotation, Vector2 newVelocity)
    {
        rb.position = newPosition;
        rb.rotation = newRotation;
        rb.linearVelocity = newVelocity;
    }

    public void UpdatePlayerTransformPredictive(Vector2 newPosition, float newRotation, Vector2 newVelocity)
    {
        if (IsOwner)
        {
            UpdatePlayerTransform(newPosition, newRotation, newVelocity);
            UpdatePlayerTransformServerRpc(newPosition, newRotation, newVelocity);
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdatePlayerTransformServerRpc(Vector2 newPosition, float newRotation, Vector2 newVelocity)
    {
        UpdatePlayerTransform(newPosition, newRotation, newVelocity);
        UpdatePlayerTransformClientRpc(newPosition, newRotation, newVelocity);
    }

    // Reusable
    [Rpc(SendTo.NotServer)]
    public void UpdatePlayerTransformClientRpc(Vector2 serverPosition, float serverRotation, Vector2 serverVelocity)
    {
        lastServerPosition = serverPosition;
        lastServerRotation = serverRotation;
        lastServerVelocity = serverVelocity;
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