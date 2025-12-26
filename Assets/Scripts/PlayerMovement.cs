using System.Collections;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float hangMoveSpeed = 15f;

    public LayerMask terrainLayer;

    [Header("Bounds Check")]
    public LayerMask boundsLayer;
    private Transform spawnPoint;
    public LayerMask hitLayers;

    public AudioSource audioSource;
    public AudioClip whooshSound;
    public AudioClip popSound;
    public AudioClip squelchSound;
    public AudioClip deathSound;

    public Rigidbody2D rb;

    private UIController uiController;
    public Vector2 moveInput;
    public Vector2 aimDirection;
    public Vector2 teleport;
    public bool popup;
    public bool jump;
    public bool startGrapple = false;
    public bool stopGrapple = false;

    // The object being grappled to, if any
    public GameObject grappleObject;

    // Offset from the grapple object's position to the grapple point
    public Vector2 grappleOffset;

    // The last object grappled to.
    public Vector2 lastGrapplePoint;

    public Tongue tongue;

    [SerializeField]
    public Collider2D[] playerColliders;

    public PlayerInput playerInput;

    [SerializeField]
    public float popupForce = 3f;
    private float lastGrappleTime = -10f;

    [SerializeField]
    private float grappleCooldown = 0.25f;

    [SerializeField]
    public float maxDistance = 10f;

    [SerializeField]
    private float minDistance = 1f;

    [SerializeField]
    public float launchSpeed = 1f;

    [SerializeField]
    public float hangDistance = 2f;

    [SerializeField]
    private float minPopupDistance = 1.25f;
    private int terrainLayerNumber = 3;
    private int playersLayerNumber = 7;
    GameObject KBMIcons;
    GameObject GamepadIcons;

    public void Start()
    {
        spawnPoint = GameObject.Find("Spawn").transform;
        transform.position = spawnPoint.position;
        uiController = FindFirstObjectByType<UIController>();
        KBMIcons = GameObject.Find("KBMIcons");
        GamepadIcons = GameObject.Find("GamepadIcons");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[PlayerMovement] OnStartClient called. IsOwner: {IsOwner}, OwnerId: {OwnerId}");

        if (IsOwner)
        {
            Debug.Log("[PlayerMovement] Enabling PlayerInput for owner");
            playerInput.enabled = true;

            // Explicitly enable the actions (WebGL fix)
            if (playerInput.actions != null)
            {
                playerInput.actions.Enable();
                Debug.Log("[PlayerMovement] Manually enabled Input Actions");
            }

#if UNITY_WEBGL
            // Force Keyboard&Mouse control scheme (WebGL fix)
            playerInput.SwitchCurrentControlScheme(
                "Keyboard&Mouse",
                Keyboard.current,
                Mouse.current
            );
            Debug.Log(
                $"[PlayerMovement] Switched to control scheme: {playerInput.currentControlScheme}"
            );
#endif

            // Add these diagnostic logs:
            Debug.Log($"[PlayerMovement] PlayerInput.enabled: {playerInput.enabled}");
            Debug.Log($"[PlayerMovement] PlayerInput.actions: {playerInput.actions}");
            Debug.Log(
                $"[PlayerMovement] PlayerInput.actions.enabled: {playerInput.actions?.enabled}"
            );
            Debug.Log(
                $"[PlayerMovement] PlayerInput.currentActionMap: {playerInput.currentActionMap}"
            );
            Debug.Log(
                $"[PlayerMovement] PlayerInput.currentControlScheme: {playerInput.currentControlScheme}"
            );

            StartCoroutine(WaitForUIController());
        }
        else
        {
            Debug.Log("[PlayerMovement] Not owner, PlayerInput stays disabled");
            playerInput.enabled = false;
        }
    }

    IEnumerator WaitForUIController()
    {
        while (FindFirstObjectByType<UIController>() == null)
            yield return null;

        uiController = FindFirstObjectByType<UIController>();
    }

    private void Update()
    {
        if (IsOwner && !playerInput.enabled)
        {
            Debug.LogWarning(
                "[PlayerMovement] PlayerInput was disabled for owner - enabling it now (WebGL timing fix)"
            );
            playerInput.enabled = true;
        }

        if (IsOwner && playerInput.currentControlScheme != "Gamepad" && !uiController.isPaused)
        {
            Vector2 lookPosition = Camera.main.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );
            aimDirection = (lookPosition - (Vector2)tongue.transform.position).normalized;
        }
        if (uiController.isPaused)
        {
            tongue.SetTongueTargetNone();
        }
        else if (grappleObject == null)
        {
            tongue.SetTongueTargetDefault();
        }
        else
        {
            tongue.SetTongueTargetHit();
        }

        CheckIcons();

    }

    private void CheckIcons()
    {
        if (
            KBMIcons != null
            && GamepadIcons != null
            && playerInput.currentControlScheme != "Gamepad"
        )
        {
            KBMIcons.SetActive(true);
            GamepadIcons.SetActive(false);
            Cursor.visible = true;
        }
        else if (
            KBMIcons != null
            && GamepadIcons != null
            && playerInput.currentControlScheme == "Gamepad"
        )
        {
            KBMIcons.SetActive(false);
            GamepadIcons.SetActive(true);
            Cursor.visible = false;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        if (CheckOutOfBounds())
        {
            if (spawnPoint == null)
            {
                spawnPoint = GameObject.Find("Spawn").transform;
            }
            teleport = spawnPoint.position;
            ServerRpcPlaySoundEffect("death");
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (playerInput.currentControlScheme == "Gamepad")
        {
            // moves player on gamepad
            moveInput = context.ReadValue<Vector2>();
            //Vector2 readInput = context.ReadValue<Vector2>();
            //if (readInput != Vector2.zero)
            //{
            //    aimDirection = (readInput.normalized);

            //}

            //if (grappleObject != null)
            //{
            //    moveInput = context.ReadValue<Vector2>();
            //}
            //else
            //{
            //    moveInput = Vector2.zero;

            //}
        }
        else
        {
            // kbm handled in Update()
        }
        //if (grappleObject == null)
        //{
        //    tongue.SetTongueTargetDefault();
        //}
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (uiController.isPaused)
            return;
        if (playerInput.currentControlScheme != "Gamepad")
        {
            // moves player on kbm\

            // If the player is grappling, allow movement input to control the grappled object
            //if (grappleObject != null)
            //{
            //    moveInput = context.ReadValue<Vector2>();
            //}
            //else
            //{
            //    moveInput = Vector2.zero;

            //}

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

        if (uiController.isPaused)
            return;

        if (context.performed)
        {
            TryGrapple();
        }
        else if (context.canceled)
        {
            StopGrapple();
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (uiController.isPaused)
            return;

        if (context.performed)
        {
            jump = true;
        }
    }

    private void TryGrapple()
    {
        if (!IsOwner)
            return;

        if (Time.time - lastGrappleTime >= grappleCooldown)
        {
            lastGrappleTime = Time.time;
            ToggleColliders(false);
            RaycastHit2D _hit = Physics2D.Raycast(
                tongue.transform.position,
                aimDirection,
                maxDistance,
                hitLayers
            );
            ToggleColliders(true);
            ServerRpcPlaySoundEffect("whoosh");
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
        stopGrapple = true;
        tongue.ResetTongueWaves();
        tongue.SetTongueTargetDefault();
    }

    [ServerRpc(RunLocally = true)]
    public void ServerRpcPlaySoundEffect(string clip)
    {
        if (IsOwner)
        {
            PlaySoundEffect(clip);
        }
        ObserversRpcPlaySoundEffect(clip);
    }

    [ObserversRpc]
    public void ObserversRpcPlaySoundEffect(string clip)
    {
        if (IsOwner)
            return; // Don't play sound on the owner to avoid double sound effects
        PlaySoundEffect(clip);
    }

    public void PlaySoundEffect(string clip)
    {
        // Randomize the pitch slightly for variation
        audioSource.pitch = Random.Range(0.5f, 1.5f);

        AudioClip clipToPlay;

        if (clip == "squelch")
        {
            clipToPlay = squelchSound;
        }
        else if (clip == "pop")
        {
            clipToPlay = popSound;
        }
        else if (clip == "whoosh")
        {
            clipToPlay = whooshSound;
        }
        else if (clip == "death")
        {
            clipToPlay = deathSound;
        }
        else
        {
            return; // Invalid clip name, do nothing
        }
        audioSource.PlayOneShot(clipToPlay);
    }

    void OnGrappleHit(RaycastHit2D _hit)
    {
        int hitLayer = _hit.transform.gameObject.layer;
        tongue.ResetTongueWaves();
        float distance = Vector2.Distance(_hit.point, tongue.transform.position);

        if (_hit.transform.gameObject.layer == terrainLayerNumber && distance <= minPopupDistance)
        {
            // If the distance is less than the minimum, popup the player
            ServerRpcPlaySoundEffect("pop");
            popup = true;
            lastGrapplePoint = _hit.point;
            tongue.SetTongueTargetHit();
        }
        else if (
            _hit.transform.gameObject.layer == terrainLayerNumber
            || _hit.transform.gameObject.layer == playersLayerNumber
        )
        {
            // Grapple to terrain or player
            // Get the local position of the hit point on the hit object, so if the object moves, the grapple point will still be correct
            ServerRpcPlaySoundEffect("squelch");
            grappleOffset = _hit.transform.InverseTransformPoint(_hit.point);
            grappleObject = _hit.transform.gameObject;
            lastGrapplePoint = (Vector2)grappleObject.transform.TransformPoint(grappleOffset);
            startGrapple = true;
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
        if (string.IsNullOrEmpty(path))
            return null;

        string[] names = path.Split('/');

        // Start by finding the root object(s) (those without a parent)
        GameObject[] rootObjects = UnityEngine
            .SceneManagement.SceneManager.GetActiveScene()
            .GetRootGameObjects();

        GameObject current = null;
        foreach (var root in rootObjects)
        {
            if (root.name == names[0])
            {
                current = root;
                break;
            }
        }
        if (current == null)
            return null;

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
