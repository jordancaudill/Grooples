using FishNet.Example.ColliderRollbacks;
using FishNet.Example.Scened;
using FishNet.Managing.Client;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    public LayerMask terrainLayer;

    [Header("Bounds Check")]
    public LayerMask boundsLayer;
    private Transform spawnPoint;
    public LayerMask hitLayers;

    public Rigidbody2D rb;

    public Vector2 moveInput;
    public Vector2 aimDirection;
    public Vector2 teleport;
    public Vector2 popup;

    // the object being grappled to, if any
    public GameObject grappleObject;
    // Offset from the grapple object's position to the grapple point
    public Vector2 grappleOffset;
    // The last object grappled to.
    public GameObject lastGrappleObject;


    public GameObject tongue;

    [SerializeField] public Collider2D[] playerColliders;

    public PlayerInput playerInput;
    public PlayerInputManager playerInputManager;
    [SerializeField] public float popupForce = 1200f;
    private float lastGrappleTime = -10f;
    [SerializeField] private float grappleCooldown = 0.25f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float launchSpeed = 1f;
    [SerializeField] private float hangDistance = 2f;
    [SerializeField] private float minPopupDistance = 1.25f;
    private int terrainLayerNumber = 3;
    private int playersLayerNumber = 7;


    public override void OnStartClient()
    {

        if (IsOwner)
        {
            playerInput.enabled = true;
        }
        playerInputManager = GameObject.Find("PlayerInputManager").GetComponent<PlayerInputManager>();
        spawnPoint = GameObject.Find("Spawn").transform;

    }

    private void FixedUpdate()
    {
        if (CheckOutOfBounds())
        {
            // If player is out of bounds, teleport them to the spawn point
            teleport = spawnPoint.position;
        }
    }


    public void OnLook(InputAction.CallbackContext context)
    {
        if (playerInput.currentControlScheme == "Gamepad")
        {
            // moves player on gamepad
            moveInput = context.ReadValue<Vector2>();
        }
        else
        {
            // aim grapple on kbm
            Vector2 lookPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            aimDirection = (lookPosition - (Vector2)tongue.transform.position).normalized;

        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (playerInput.currentControlScheme != "Gamepad")
        {
            // moves player on kbm
            moveInput = context.ReadValue<Vector2>();
        }
        else
        {
            // aim grapple on gamepad
            if (context.ReadValue<Vector2>().normalized != Vector2.zero)
            {
                aimDirection = (context.ReadValue<Vector2>().normalized);

            }

        }

    }

    public void OnGrapple(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            print("OnGrapple performed");
            TryGrapple();
        }
        //else if (context.canceled)
        //{
        //    grappleGun.StopGrapple();
        //}
    }

    private void TryGrapple()
    {
        print("TryGrapple");
        if (Time.time - lastGrappleTime >= grappleCooldown)
        {   
            lastGrappleTime = Time.time;
            ToggleColliders(false);
            RaycastHit2D _hit = Physics2D.Raycast(tongue.transform.position, aimDirection, maxDistance, hitLayers);
            ToggleColliders(true);
            if (_hit.transform != null)
            {
                OnGrappleHit(_hit);
            }
            else
            {
                OnGrappleMiss();
            }

        }
    }

    private void StopGrapple()
    {
        //if (grappleObject == null) return;

        //UpdateGrapple(false, null, Vector2.zero);
        //tongueTarget = GetNormalizedTongueDirection();
        //grappleObject = null;
        //lastGrappleTime = Time.time;

    }

    void OnGrappleHit(RaycastHit2D _hit)
    {
        print("OnGrappleHit");
        int hitLayer = _hit.transform.gameObject.layer;

        float distance = Vector2.Distance(_hit.point, tongue.transform.position);
        if (_hit.transform.gameObject.layer == terrainLayerNumber  && distance <= minPopupDistance)
        {
            // If the distance is less than the minimum, popup the player
            popup = _hit.point;

        }
        else if (_hit.transform.gameObject.layer == terrainLayerNumber || _hit.transform.gameObject.layer == playersLayerNumber)
        {

            // Grapple to terrain or player
            // Get the local position of the hit point on the hit object, so if the object moves, the grapple point will still be correct
            //grappleOffset = _hit.transform.InverseTransformPoint(_hit.point);
            //grappleObject = _hit.transform.gameObject;
            //tongueTarget = (Vector2)grappleObject.transform.TransformPoint(grappleOffset);
            //lastGrappleLocation = tongueTarget;

            //// Otherwise, use the hierarchy path
            //UpdateGrapple(true, GetHierarchyPath(_hit.transform.gameObject), grappleOffset);

        }
    }

    void OnGrappleMiss()
    {
        //tongueDrawTime = 0;
        //tongueWaveSize = startTongueWaveSize;
        //straightLine = false;
        //tongueTarget = GetTargetPointAtDistance(maxDistance);
        //lastGrappleLocation = tongueTarget;
    }




    bool CheckOutOfBounds()
    {
        return Physics2D.OverlapCircle(rb.position, 1f, boundsLayer);
    }

    private void ToggleColliders(bool enable)
    {
        foreach (Collider2D collider in playerColliders)
        {
            if (collider != null)
            {
                collider.enabled = enable;
            }
        }
    }



}
