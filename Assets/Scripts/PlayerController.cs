using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Grapple")]
    public GrappleGun grappleGun;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Bounds Check")]
    public LayerMask boundsLayer;
    public Transform spawnPoint;

    private Rigidbody2D rb;

    public bool isGrounded;
    private Vector2 moveInput;

    InputAction movementAction;
    InputAction jumpAction;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        movementAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
    }

    void Update()
    {
        // Get movement input
        moveInput = movementAction.ReadValue<Vector2>();

        // Jump input
        if (jumpAction.triggered && (isGrounded || grappleGun.isGrappling))
        {
            if (grappleGun.isGrappling)
            {
                grappleGun.StopGrapple(); // Stop grappling if jumping
            }
        }

    }

    void FixedUpdate()
    {
        if (CheckOutOfBounds())
        {
            rb.position = spawnPoint.position; // Reset position if out of bounds
            rb.linearVelocity = new Vector2(0, 0);
        }
        else
        {

            // Check if grounded
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            // Apply horizontal movement
            rb.AddForce(new Vector2(moveInput.x * moveSpeed, 0), ForceMode2D.Force);

        }

    }

    bool CheckOutOfBounds()
    {
        return Physics2D.OverlapCircle(rb.position, 1f, boundsLayer);
    }


}