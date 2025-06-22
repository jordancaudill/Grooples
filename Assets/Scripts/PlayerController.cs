using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
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

    private Vector2 upcomingInput;

    GameManager gameManager;

    //private Queue<StateSnapshot> stateBuffer = new Queue<StateSnapshot>();

    public struct StateSnapshot : INetworkSerializable
    {
        public Vector2 position;
        public float rotation;
        public Vector2 velocity;
        public float angularVelocity;
        public float timestamp;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref angularVelocity);
            serializer.SerializeValue(ref timestamp);
        }
    }

    public StateSnapshot stateSnapshot = new StateSnapshot
    {
        position = Vector2.zero,
        rotation = 0f,
        velocity = Vector2.zero,
        angularVelocity = 0f,
        timestamp = 0f
    };

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        playerNumber = gameManager.players.Count;
        name = "Player_" + playerNumber;
        gameManager.AddPlayer(name);
        AssignSpriteToPlayer();

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


        if (IsOwner)
        {
            // Check if grounded
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, terrainLayer);

            if (CheckOutOfBounds())
            {
                UpdatePlayerTransformServerRpc(new StateSnapshot
                {
                    position = spawnPoint.position,
                    rotation = 0f,
                    velocity = Vector2.zero,
                    angularVelocity = 0f,
                    timestamp = Time.time
                });
            }


            if (IsServer)
            {
                // Owner and server
                // Move and update all clients
                MovePlayer(moveInput);
                MovePlayerClientRpc(new StateSnapshot
                {
                    position = rb.position,
                    rotation = rb.rotation,
                    velocity = rb.linearVelocity,
                    angularVelocity = rb.angularVelocity,
                    timestamp = Time.time
                });

            }
            else
            {
                // Owner but not server
                // Move locally for responsiveness, then send input to server for validation
                if (stateSnapshot.timestamp != 0f)
                {
                    ReconcileTransform(stateSnapshot);
                }
                MovePlayer(moveInput);
                MovePlayerServerRpc(moveInput);

            }

        }
        else
        {
            if (IsServer)
            {
                // Server but not owner
                if (upcomingInput != Vector2.zero)
                {
                    MovePlayer(upcomingInput);
                    upcomingInput = Vector2.zero;
                }
                MovePlayerClientRpc(new StateSnapshot
                {
                    position = rb.position,
                    rotation = rb.rotation,
                    velocity = rb.linearVelocity,
                    angularVelocity = rb.angularVelocity,
                    timestamp = Time.time
                });

            }
            else
            {
                // Not owner and not server
                InterpolateTransform();
            }
        }



    }

    // Interpolate transform for non-owner (remote) clients
    private void InterpolateTransform()
    {
        if (!IsOwner && !IsServer)
        {

            //If we've teleported the player, snap to the position and rotation
            if (stateSnapshot.velocity == Vector2.zero && stateSnapshot.angularVelocity == 0f)
            {
                rb.position = stateSnapshot.position;
                rb.rotation = stateSnapshot.rotation;
                rb.linearVelocity = stateSnapshot.velocity;
                rb.angularVelocity = stateSnapshot.angularVelocity;
                return;
            }

            rb.position = Vector2.Lerp(rb.position, stateSnapshot.position, 0.5f);
            rb.rotation = Mathf.LerpAngle(rb.rotation, stateSnapshot.rotation, 0.5f);
            // maybe should try MovePosition and MoveRotation again ?
        }

    }

    public void ReconcileTransform(StateSnapshot serverSnapshot)
    {
        float error = Vector2.Distance(rb.position, serverSnapshot.position);
        float rotError = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, serverSnapshot.rotation));

        // Snap if error is very large, otherwise smooth
        if (error > 3f || rotError > 60f)
        {
            rb.position = serverSnapshot.position;
            rb.rotation = serverSnapshot.rotation;
        }
        else if (error > 1.5f || rotError > 25f)
        {
            rb.position = Vector2.Lerp(rb.position, serverSnapshot.position, 0.5f);
            rb.rotation = Mathf.LerpAngle(rb.rotation, serverSnapshot.rotation, 0.5f);
        }

        rb.linearVelocity = serverSnapshot.velocity;
        rb.angularVelocity = serverSnapshot.angularVelocity;

        ClearSnapshot();
    }

    private void ClearSnapshot()
    {
        stateSnapshot = new StateSnapshot
        {
            position = Vector2.zero,
            rotation = 0f,
            velocity = Vector2.zero,
            angularVelocity = 0f,
            timestamp = 0f
        };
    }

    /////////////////////// MovePlayer
    public void MovePlayer(Vector2 input)
    {
        rb.AddForce(new Vector2(input.x * moveSpeed, 0), ForceMode2D.Force);
    }


    [Rpc(SendTo.Server)]
    public void MovePlayerServerRpc(Vector2 input)
    {
        upcomingInput = input;
    }

    [Rpc(SendTo.NotServer)]
    public void MovePlayerClientRpc(StateSnapshot snapshot)
    {
        //if (IsOwner)
        //{
        //    ReconcileTransform(snapshot);
        //}
        //else
        //{
        stateSnapshot = snapshot;
        //}
    }

    /////////////////////// UpdatePlayerTransform
    public void UpdatePlayerTransform(StateSnapshot snapshot)
    {
        rb.position = snapshot.position;
        rb.rotation = snapshot.rotation;
        rb.linearVelocity = snapshot.velocity;
        rb.angularVelocity = snapshot.angularVelocity;
    }


    [Rpc(SendTo.Server)]
    public void UpdatePlayerTransformServerRpc(StateSnapshot snapshot)
    {
        UpdatePlayerTransform(snapshot);
        UpdatePlayerTransformClientRpc(snapshot);
    }

    [Rpc(SendTo.NotServer)]
    public void UpdatePlayerTransformClientRpc(StateSnapshot snapshot)
    {
        //if (IsOwner)
        //{
        //    ReconcileTransform(snapshot);
        //}
        //else
        //{
        stateSnapshot = snapshot;
        //}
    }



    /////////////////////// Utils

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