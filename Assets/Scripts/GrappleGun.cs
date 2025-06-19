using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleGun : NetworkBehaviour
{
    [Header("General:")]
    public LineRenderer lineRenderer;
    public PlayerController playerController;
    [SerializeField] public Collider2D[] playerColliders;
    public LayerMask hitLayers;

    private UIController uiController;
    private Camera playerCamera;
    private int terrainLayer = 3;
    private int playersLayer = 7;

    [Header("Tongue Animation Settings:")]
    public AnimationCurve tongueAnimationCurve;
    public AnimationCurve tongueProgressionCurve;

    [Range(0.01f, 4)][SerializeField] private float startTongueWaveSize = 2;
    [Range(0, 50)][SerializeField] private float straightenLineSpeed = 5;
    [SerializeField][Range(1, 50)] private float tongueProgressionSpeed = 1;
    [SerializeField] private int tonguePrecisionPoints = 40;
    InputAction lookAction;
    private float tongueDrawTime = 0;
    private float tongueWaveSize = 0;
    private bool straightLine = true;
    private Vector2 tongueTarget;
    private Vector2 lastSentTongueTarget;
    private Vector2 lastGrappleLocation;
    private float tongueTargetSendInterval = 0.025f; // 40 times per second
    private float tongueTargetSendTimer = 0f;
    private float tongueTargetMinChange = 0.01f; // Only send if look dir changes at least this much


    [Header("Physics:")]
    public SpringJoint2D springJoint2D;
    public Rigidbody2D playerRigidbody;
    [SerializeField] private float grappleCooldown = 0.25f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float launchSpeed = 1f;
    [SerializeField] private float hangDistance = 2f;
    [SerializeField] private float popupForce = 500f;
    [SerializeField] private float minPopupDistance = 2f;

    private Vector2 grappleOffset;
    private GameObject grappleObject = null;
    private float lastGrappleTime = -10f;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        AssignColorToPlayer();
        uiController = FindAnyObjectByType<UIController>();
        playerCamera = FindAnyObjectByType<Camera>();
        lineRenderer.positionCount = tonguePrecisionPoints;
        lookAction = playerController.playerInput.actions["Look"];
    }


    private void Update()
    {
        if (uiController != null && !uiController.isPaused)
        {
            tongueDrawTime += Time.deltaTime;
            DrawTongue();

            CheckLookAction();
        }
    }
    private void CheckLookAction()
    {
        if (IsOwner)
        {
            if (grappleObject == null)
            {
                if (playerController.playerInput.currentControlScheme == "Gamepad")
                {
                    tongueTarget = (Vector2)transform.position + (lookAction.ReadValue<Vector2>().normalized * minDistance);
                }
                else
                {
                    Vector2 lookPosition = playerCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                    tongueTarget = (Vector2)transform.position + (lookPosition - (Vector2)transform.position).normalized * minDistance;
                }

            }
            else
            {
                tongueTarget = (Vector2)grappleObject.transform.TransformPoint(grappleOffset);
            }

            // Send the new look direction to the server
            if (Time.time - tongueTargetSendTimer >= tongueTargetSendInterval && Vector2.Distance(tongueTarget, lastSentTongueTarget) >= tongueTargetMinChange)
            {
                lastSentTongueTarget = tongueTarget;
                tongueTargetSendTimer = Time.time;
                TongueTargetRpc(tongueTarget);

            }

        }

    }

    [Rpc(SendTo.NotOwner)]
    private void TongueTargetRpc(Vector2 target)
    {
        tongueTarget = target;
    }

    public void OnGrapplingHook(InputAction.CallbackContext context)
    {
        if (IsOwner)
        {
            if (context.performed && Time.time - lastGrappleTime >= grappleCooldown)
            {
                StartGrapple();
            }
            else if (context.canceled)
            {
                StopGrapple();
            }

        }

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

    private void StartGrapple()
    {
        ToggleColliders(false);
        RaycastHit2D _hit = Physics2D.Raycast(transform.position, GetNormalizedTongueDirection(), maxDistance, hitLayers);
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

    private void StopGrapple()
    {
        if (grappleObject == null) return;

        UpdateGrapple(false, null, Vector2.zero);
        tongueTarget = GetNormalizedTongueDirection();
        grappleObject = null;
        lastGrappleTime = Time.time;

    }


    void OnGrappleHit(RaycastHit2D _hit)
    {
        int hitLayer = _hit.transform.gameObject.layer;
        tongueDrawTime = 0;
        tongueWaveSize = startTongueWaveSize;
        straightLine = false;

        float distance = Vector2.Distance(_hit.point, transform.position);
        if (_hit.transform.gameObject.layer == terrainLayer && playerController.isGrounded && distance <= minPopupDistance)
        {
            // If the player is grounded and the distance is less than the minimum, pop up the player, do not grapple
            PopupPlayer(_hit.point);
        }
        else if (_hit.transform.gameObject.layer == terrainLayer || _hit.transform.gameObject.layer == playersLayer)
        {

            // Grapple to terrain or player
            // Get the local position of the hit point on the hit object, so if the object moves, the grapple point will still be correct
            grappleOffset = _hit.transform.InverseTransformPoint(_hit.point);
            grappleObject = _hit.transform.gameObject;
            tongueTarget = (Vector2)grappleObject.transform.TransformPoint(grappleOffset);
            lastGrappleLocation = tongueTarget;

            // Otherwise, use the hierarchy path
            UpdateGrapple(true, GetHierarchyPath(_hit.transform.gameObject), grappleOffset);

        }
    }

    public void PopupPlayer(Vector2 point)
    {
        Vector2 distanceVector = (Vector2)transform.position - point;
        playerRigidbody.AddForce(distanceVector.normalized * popupForce, ForceMode2D.Force);
        lastGrappleLocation = point;

        if (IsOwner)
        {
            PopupPlayerRpc(point);
        }
    }

    [Rpc(SendTo.NotOwner)]
    public void PopupPlayerRpc(Vector2 point)
    {
        PopupPlayer(point);
    }


    void OnGrappleMiss()
    {
        tongueDrawTime = 0;
        tongueWaveSize = startTongueWaveSize;
        straightLine = false;
        tongueTarget = GetTargetPointAtDistance(maxDistance);
        lastGrappleLocation = tongueTarget;
    }

    public void UpdateGrapple(bool enabled, string hierarchyPath, Vector2 localHitPoint)
    {
        tongueDrawTime = 0;
        tongueWaveSize = startTongueWaveSize;
        straightLine = false;

        if (hierarchyPath != null)
        {
            grappleObject = FindByHierarchyPath(hierarchyPath);
            grappleOffset = localHitPoint;

            if (IsServer)
            {
                springJoint2D.connectedBody = grappleObject.GetComponent<Rigidbody2D>();
                springJoint2D.connectedAnchor = localHitPoint;
            }
        }
        else
        {
            springJoint2D.connectedBody = null;
            springJoint2D.connectedAnchor = Vector2.zero;
            grappleObject = null;
            grappleOffset = Vector2.zero;
        }

        springJoint2D.enabled = enabled;
        springJoint2D.autoConfigureDistance = false;
        springJoint2D.distance = hangDistance;
        springJoint2D.frequency = launchSpeed;

        if (IsOwner)
        {
            UpdateGrappleRpc(enabled, hierarchyPath, localHitPoint);
        }
    }


    [Rpc(SendTo.NotOwner)]
    public void UpdateGrappleRpc(bool enabled, string hierarchyPath, Vector2 localHitPoint)
    {
        UpdateGrapple(enabled, hierarchyPath, localHitPoint);
    }
    private void AssignColorToPlayer()
    {
        // Assign a color to the player based on the OwnerClientId or any other logic
        Dictionary<int, string> playerColors = new Dictionary<int, string>
        {
            { 0, "#EA61CC" },
            { 1, "#689581" },
            { 2, "#E9AC6F" },
            { 3, "#9576A6" },
        };

        string playerColor = playerColors[playerController.playerNumber];
        ColorUtility.TryParseHtmlString(playerColor, out Color color);

        GetComponent<LineRenderer>().colorGradient = new Gradient
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(color, 1f)
            },
            alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 1f)
            }
        };
    }

    // DRAW TONGUE
    void DrawTongue()
    {
        if (!straightLine)
        {
            if (Math.Abs(lineRenderer.GetPosition(tonguePrecisionPoints - 1).x - tongueTarget.x) <= 0.1f && Math.Abs(lineRenderer.GetPosition(tonguePrecisionPoints - 1).y - tongueTarget.y) <= 0.1f)
            {
                straightLine = true;
            }
            else
            {
                DrawTongueWaves(tongueTarget);
            }
        }
        else
        {
            if (tongueWaveSize > 0)
            {
                tongueWaveSize -= Time.deltaTime * straightenLineSpeed;
                DrawTongueWaves(tongueTarget);
            }
            else
            {
                tongueWaveSize = 0;
                DrawTongueNoWaves(tongueTarget);
            }
        }
    }

    void DrawTongueWaves(Vector2 tongueTarget)
    {
        Vector2 grappleDistanceVector = tongueTarget - (Vector2)transform.position;

        for (int i = 0; i < tonguePrecisionPoints; i++)
        {
            float delta = i / (tonguePrecisionPoints - 1f);
            Vector2 offset = Vector2.Perpendicular(grappleDistanceVector).normalized * tongueAnimationCurve.Evaluate(delta) * tongueWaveSize;
            Vector2 targetPosition, currentPosition;
            if (grappleObject != null)
            {
                targetPosition = Vector2.Lerp(transform.position, tongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(transform.position, targetPosition, tongueProgressionCurve.Evaluate(tongueDrawTime) * tongueProgressionSpeed);
            }
            else
            {
                targetPosition = Vector2.Lerp(transform.position, lastGrappleLocation, delta) + offset;
                currentPosition = Vector2.Lerp(targetPosition, tongueTarget, tongueProgressionCurve.Evaluate(tongueDrawTime) * tongueProgressionSpeed);
            }

            lineRenderer.SetPosition(i, currentPosition);
        }
    }

    void DrawTongueNoWaves(Vector2 tongueTarget)
    {
        lineRenderer.SetPosition(0, transform.position);

        for (int i = 1; i < tonguePrecisionPoints; i++)
        {
            lineRenderer.SetPosition(i, tongueTarget);
        }
    }


    // UTILS
    private Vector2 GetNormalizedTongueDirection()
    {
        return (tongueTarget - (Vector2)transform.position).normalized;
    }

    // Get the target point for the tongue at the given distance (could be maxDistance or minDistance)
    private Vector2 GetTargetPointAtDistance(float distance)
    {
        return (Vector2)transform.position + GetNormalizedTongueDirection() * distance;
    }

    private static string GetHierarchyPath(GameObject obj)
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

    private static GameObject FindByHierarchyPath(string path)
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


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        Gizmos.DrawWireSphere(tongueTarget, 0.1f);
    }

}