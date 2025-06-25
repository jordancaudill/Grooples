using FishNet.Object;
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

    // The object being grappled to, if any
    public GameObject grappleObject;
    // Offset from the grapple object's position to the grapple point
    public Vector2 grappleOffset;
    // The last object grappled to.
    public Vector2 lastGrapplePoint;


    public Tongue tongue;

    [SerializeField] public Collider2D[] playerColliders;

    public PlayerInput playerInput;
    public PlayerInputManager playerInputManager;
    [SerializeField] public float popupForce = 1200f;
    private float lastGrappleTime = -10f;
    [SerializeField] private float grappleCooldown = 0.25f;
    [SerializeField] public float maxDistance = 10f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] public float launchSpeed = 1f;
    [SerializeField] public float hangDistance = 2f;
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

    private void Update()
    {
        if (playerInput.currentControlScheme != "Gamepad")
        {
            // aim grapple on kbm
            Vector2 lookPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            aimDirection = (lookPosition - (Vector2)tongue.transform.position).normalized;

        }

        if (grappleObject == null)
        {
            tongue.SetTongueTargetDefault();
        }

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
            // kbm handled in Update()
        }
        if (grappleObject == null)
        {
            tongue.SetTongueTargetDefault();
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
            TryGrapple();
        }
        else if (context.canceled)
        {
            StopGrapple();
        }
    }

    private void TryGrapple()
    {
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

        lastGrappleTime = Time.time;
        grappleOffset = Vector2.zero;
        grappleObject = null;
        tongue.SetTongueTargetDefault();

    }

    void OnGrappleHit(RaycastHit2D _hit)
    {
        int hitLayer = _hit.transform.gameObject.layer;
        tongue.ResetTongueWaves();
        float distance = Vector2.Distance(_hit.point, tongue.transform.position);
        if (_hit.transform.gameObject.layer == terrainLayerNumber && distance <= minPopupDistance)
        {
            // If the distance is less than the minimum, popup the player
            popup = _hit.point;
            lastGrapplePoint = popup;
            tongue.SetTongueTargetHit();

        }
        else if (_hit.transform.gameObject.layer == terrainLayerNumber || _hit.transform.gameObject.layer == playersLayerNumber)
        {

            // Grapple to terrain or player
            // Get the local position of the hit point on the hit object, so if the object moves, the grapple point will still be correct
            grappleOffset = _hit.transform.InverseTransformPoint(_hit.point);
            grappleObject = _hit.transform.gameObject;
            lastGrapplePoint = (Vector2)grappleObject.transform.TransformPoint(grappleOffset);
            tongue.SetTongueTargetHit();

        }
    }

    void OnGrappleMiss()
    {
        lastGrapplePoint = (Vector2)tongue.transform.position + (aimDirection * maxDistance);
        tongue.SetTongueTargetMiss();

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


    public static string GetHierarchyPath(GameObject obj)
    {
        if (obj == null)
            return null;

        string path = obj.name;
        Transform current = obj.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;

    }

    public static GameObject FindByHierarchyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string[] names = path.Split('/');

        // Start by finding the root object(s) (those without a parent)
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        GameObject current = null;
        foreach (var root in rootObjects)
        {
            if (root.name == names[0])
            {
                current = root;
                break;
            }
        }
        if (current == null) return null;

        // Traverse down the hierarchy
        for (int i = 1; i < names.Length; i++)
        {
            Transform child = current.transform.Find(names[i]);
            if (child == null)
                return null;

            current = child.gameObject;
        }
        return current;
    }
}
