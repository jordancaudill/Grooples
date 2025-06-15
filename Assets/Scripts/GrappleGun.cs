using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleGun : NetworkBehaviour
{
    [Header("General References:")]
    public LineRenderer m_lineRenderer;
    public PlayerController m_player;
    private UIController uiController;

    [Header("General Settings:")]
    [SerializeField] private int precision = 40;
    [Range(0, 50)][SerializeField] private float straightenLineSpeed = 5;

    [Header("Tongue Animation Settings:")]
    public AnimationCurve tongueAnimationCurve;
    [Range(0.01f, 4)][SerializeField] private float StartWaveSize = 2;
    float waveSize = 0;

    // An array of colliders that the tongue should ignore
    [SerializeField] public Collider2D[] ignoreColliders;

    [Header("Tongue Progression:")]
    public AnimationCurve tongueProgressionCurve;
    [SerializeField][Range(1, 50)] private float tongueProgressionSpeed = 1;

    private float moveTime = 0;

    private bool straightLine = true;

    private int terrainLayer = 3;
    private int playersLayer = 7;
    public LayerMask hitLayers;

    private Camera m_camera;

    [Header("Physics Ref:")]
    public SpringJoint2D m_springJoint2D;
    public Rigidbody2D m_rigidbody;

    [Header("Distance:")]
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float minDistance = 1f;

    InputAction grapplingHookAction;
    InputAction lookAction;

    [Header("Launching:")]
    [SerializeField] private float launchSpeed = 1f;
    [SerializeField] private float hangDistance = 2f;

    private NetworkVariable<Vector2> tongueTarget = new NetworkVariable<Vector2>(writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<Vector2> grapplePoint = new NetworkVariable<Vector2>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isGrappling = new NetworkVariable<bool>(false, writePerm: NetworkVariableWritePermission.Owner);

    private GameObject grappleObject = null;
    public float jumpForce = 500f;
    public float minJumpDistance = 2f;
    public float grappleCooldown = 0.25f;
    private float lastGrappleTime = -10f;

    // https://bitbucket.org/Vespper/grappling-hook/src/master/GrappleRope

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();


        if (IsOwner)
        {
            isGrappling.Value = false;
        }
        if (IsServer)
        {
            grapplePoint.Value = transform.position;

        }

    }


    private void Start()
    {

        uiController = FindAnyObjectByType<UIController>();
        m_camera = FindAnyObjectByType<Camera>();

        grapplingHookAction = InputSystem.actions.FindAction("GrapplingHook");
        lookAction = InputSystem.actions.FindAction("Look");
        //UpdateGrappleJointServerRpc(false, null, transform.position);
        m_lineRenderer.positionCount = precision;

    }

    private void Update()
    {
        if (uiController != null && !uiController.isPaused)
        {
            moveTime += Time.deltaTime;
            if (IsOwner)
            {
                CheckGrappleHookAction();
                UpdateTongueTarget();
            }
            // Does not matter if owner or not, we always draw the tongue
            DrawTongue();
        }
        print(tongueTarget.Value);
    }

    private Vector2 GetNormalizedTongueDirection()
    {
        return (GetLookPos() - (Vector2)transform.position).normalized;
    }

    // Get the target point for the tongue at the given distance (could be maxDistance or minDistance)
    private Vector2 GetTargetPointAtDistance(float distance)
    {
        return (Vector2)transform.position + GetNormalizedTongueDirection() * distance;
    }

    private void CheckGrappleHookAction()
    {
        if (grapplingHookAction.WasPressedThisFrame() && Time.time - lastGrappleTime >= grappleCooldown)
        {
            StartGrapple(GetLookPos());
        }
        else if (!grapplingHookAction.IsPressed())
        {
            StopGrapple();
        }
    }

    private void UpdateTongueTarget()
    {
        if (IsOwner)
        {
            if (isGrappling.Value)
            {
                tongueTarget.Value = (Vector2)grappleObject.transform.TransformPoint(grapplePoint.Value);
            }
            else
            {
                tongueTarget.Value = GetTargetPointAtDistance(minDistance);
            }
        }
    }

    private Vector2 GetLookPos()
    {
        if (m_player.isUsingGamepad)
        {
            Vector2 direction = lookAction.ReadValue<Vector2>();
            return (Vector2)transform.position + direction.normalized * maxDistance;
        }
        else
        {
            return m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        }
    }

    private void ToggleColliders(bool enable)
    {
        foreach (Collider2D collider in ignoreColliders)
        {
            if (collider != null)
            {
                collider.enabled = enable;
            }
        }
    }

    private void StartGrapple(Vector2 lookPos)
    {
        Vector2 distanceVector = lookPos - (Vector2)transform.position;
        ToggleColliders(false);
        RaycastHit2D _hit = Physics2D.Raycast(transform.position, distanceVector.normalized, maxDistance, hitLayers);
        ToggleColliders(true);
        if (_hit.transform != null)
        {
            OnGrappleHit(_hit);
        }
        else
        {
            OnGrappleMiss(distanceVector);
        }
    }

    private void StopGrapple()
    {
        if (isGrappling.Value)
        {
            UpdateGrappleJointServerRpc(false, null, Vector2.zero);
            moveTime = 0;
            waveSize = StartWaveSize;
            straightLine = false;
            tongueTarget.Value = GetTargetPointAtDistance(minDistance);
            isGrappling.Value = false;
            grappleObject = null;
            lastGrappleTime = Time.time;
        }
    }


    void OnGrappleHit(RaycastHit2D _hit)
    {
        // Get the local position of the hit point on the hit object, so if the object moves, the grapple point will still be correct
        Vector2 localHitPoint = _hit.transform.InverseTransformPoint(_hit.point);
        int hitLayer = _hit.transform.gameObject.layer;
        moveTime = 0;
        waveSize = StartWaveSize;
        straightLine = false;
        //tongueTarget.Value = _hit.point;

        float distance = Vector2.Distance(_hit.point, transform.position);
        if (hitLayer == terrainLayer && m_player.isGrounded && distance <= minJumpDistance)
        {
            // If the player is grounded and the distance is less than the minimum, pop up the player, do not grapple
            PopupPlayerServerRpc(_hit.point);
            if (IsServer)
            {
                grapplePoint.Value = transform.position;
            }
        }
        else if (hitLayer == terrainLayer || hitLayer == playersLayer)
        {

            // Grapple to terrain or player
            //grappleObject.Value = _hit.transform.gameObject;
            isGrappling.Value = true;
            // Otherwise, use the hierarchy path
            UpdateGrappleJointServerRpc(true, GetHierarchyPath(_hit.transform.gameObject), localHitPoint);

        }
    }

    [Rpc(SendTo.Server)]
    public void PopupPlayerServerRpc(Vector2 point)
    {
        Vector2 distanceVector = (Vector2)transform.position - point;
        m_rigidbody.AddForce(distanceVector.normalized * jumpForce, ForceMode2D.Force);

    }


    void OnGrappleMiss(Vector2 distanceVector)
    {
        moveTime = 0;
        waveSize = StartWaveSize;
        straightLine = false;
        isGrappling.Value = false;
        //tongueTarget.Value = GetTargetPointAtDistance(minDistance);
        //grapplePoint.Value = tongueTarget.Value;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateGrappleJointServerRpc(bool enabled, string hierarchyPath, Vector2 localHitPoint)
    {

        if (hierarchyPath != null)
        {
            grappleObject = FindByHierarchyPath(hierarchyPath);

            if (IsServer)
            {
                m_springJoint2D.connectedBody = grappleObject.GetComponent<Rigidbody2D>();
                m_springJoint2D.connectedAnchor = localHitPoint;
                grapplePoint.Value = localHitPoint;  
            }


        }
        else
        {
            m_springJoint2D.connectedBody = null;
            m_springJoint2D.connectedAnchor = localHitPoint;
            grappleObject = null;
            grapplePoint.Value = localHitPoint;
        }

        m_springJoint2D.enabled = enabled;
        //m_springJoint2D.connectedAnchor = anchor;
        m_springJoint2D.autoConfigureDistance = false;
        m_springJoint2D.distance = hangDistance;
        m_springJoint2D.frequency = launchSpeed;

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



    void DrawTongue()
    {
        Vector2 drawTongueTarget = tongueTarget.Value;
        Vector2 drawGrapplePoint = grapplePoint.Value;
        bool drawIsGrappling = isGrappling.Value;
        DrawTongueNoWaves(drawTongueTarget);
        //if (!straightLine)
        //{
        //    if (Math.Abs(m_lineRenderer.GetPosition(precision - 1).x - drawTongueTarget.x) <= 0.1f && Math.Abs(m_lineRenderer.GetPosition(precision - 1).y - drawTongueTarget.y) <= 0.1f)
        //    {
        //        straightLine = true;
        //    }
        //    else
        //    {
        //        DrawTongueWaves(drawTongueTarget, drawGrapplePoint, drawIsGrappling);
        //    }
        //}
        //else
        //{
        //    if (waveSize > 0)
        //    {
        //        waveSize -= Time.deltaTime * straightenLineSpeed;
        //        DrawTongueWaves(drawTongueTarget, drawGrapplePoint, drawIsGrappling);
        //    }
        //    else
        //    {
        //        waveSize = 0;
        //        DrawTongueNoWaves(drawTongueTarget);
        //    }
        //}
    }

    void DrawTongueWaves(Vector2 drawTongueTarget, Vector2 drawGrapplePoint, bool drawIsGrappling)
    {
        Vector2 grappleDistanceVector = drawTongueTarget - (Vector2)transform.position;

        for (int i = 0; i < precision; i++)
        {
            float delta = (float)i / ((float)precision - 1f);
            Vector2 offset = Vector2.Perpendicular(grappleDistanceVector).normalized * tongueAnimationCurve.Evaluate(delta) * waveSize;
            Vector2 targetPosition, currentPosition;
            if (drawIsGrappling)
            {
                targetPosition = Vector2.Lerp(transform.position, drawTongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(transform.position, targetPosition, tongueProgressionCurve.Evaluate(moveTime) * tongueProgressionSpeed);
            }
            else
            {
                targetPosition = Vector2.Lerp(drawGrapplePoint, drawTongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(targetPosition, drawTongueTarget, tongueProgressionCurve.Evaluate(moveTime) * tongueProgressionSpeed);
            }

            m_lineRenderer.SetPosition(i, currentPosition);
        }
    }

    void DrawTongueNoWaves(Vector2 drawTongueTarget)
    {
        m_lineRenderer.SetPosition(0, transform.position);

        for (int i = 1; i < precision; i++)
        {
            m_lineRenderer.SetPosition(i, drawTongueTarget);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        Gizmos.DrawWireSphere(tongueTarget.Value, 0.1f);
    }
}