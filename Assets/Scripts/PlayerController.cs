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

    PlayerInput playerInput;
    InputAction lookAction;
    InputAction moveAction;

    public bool isUsingGamepad = false;

    // Sync moveInput from owner to server
    private NetworkVariable<Vector2> syncedMoveInput = new NetworkVariable<Vector2>(writePerm: NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        spawnPoint = GameObject.Find("Spawn").transform;
        transform.position = spawnPoint.position;
    }

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
        if (playerInput != null)
        {
            lookAction = InputSystem.actions.FindAction("Look");
            moveAction = InputSystem.actions.FindAction("Move");
        }
    }

    public void OnLook()
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme == "Gamepad")
        {
            isUsingGamepad = true;
        }
        else
        {
            isUsingGamepad = false;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (isUsingGamepad)
        {
            moveInput = lookAction.ReadValue<Vector2>();
        }
        else
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        syncedMoveInput.Value = moveInput;
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
}