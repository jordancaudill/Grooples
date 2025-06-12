using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleGun : MonoBehaviour
{

    [Header("General References:")]
    public LineRenderer m_lineRenderer;

    [Header("General Settings:")]
    [SerializeField] private int precision = 40;
    [Range(0, 50)][SerializeField] private float straightenLineSpeed = 5;

    [Header("Tongue Animation Settings:")]
    public AnimationCurve tongueAnimationCurve;
    [Range(0.01f, 4)][SerializeField] private float StartWaveSize = 2;
    float waveSize = 0;

    [Header("Tongue Progression:")]
    public AnimationCurve tongueProgressionCurve;
    [SerializeField][Range(1, 50)] private float tongueProgressionSpeed = 1;

    private float moveTime = 0;

    private bool straightLine = true;

    private int groundLayer = 3;

    [Header("Main Camera:")]
    public Camera m_camera;

    [Header("Physics Ref:")]
    public SpringJoint2D m_springJoint2D;
    public Rigidbody2D m_rigidbody;


    [Header("Distance:")]
    [SerializeField] private float maxDistance = 10;
    [SerializeField] private float minDistance = 1f;

    InputAction grapplingHookAction;

    [Header("Launching:")]
    [SerializeField] private float launchSpeed = 1;
    [SerializeField] private float hangDistance = 2f;

    private Vector2 tongueTarget;
    private Vector2 grapplePoint;
    public bool isGrappling = false;

    public float grappleCooldown = 0.25f;
    private float lastGrappleTime = -10f;

    // https://bitbucket.org/Vespper/grappling-hook/src/master/GrappleRope


    private void Start()
    {
        grapplingHookAction = InputSystem.actions.FindAction("GrapplingHook");
        m_springJoint2D.enabled = false;
        m_lineRenderer.positionCount = precision;

    }

    private void Update()
    {

        moveTime += Time.deltaTime;
        CheckGrappleHookAction();
        UpdateTongueTarget();
        DrawTongue();

    }

    // Get the normalized direction of the tongue towards the mouse position
    private Vector2 GetNormalizedTongueDirection()
    {
        return (GetMousePos() - (Vector2)transform.position).normalized;
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
            StartGrapple();
        }
        else if (grapplingHookAction.IsPressed() && isGrappling)
        {
            UpdateGrapple();
        }
        else if (grapplingHookAction.WasReleasedThisFrame())
        {
            StopGrapple();
        }

    }

    private void UpdateTongueTarget()
    {
        if (!isGrappling)
        {
            tongueTarget = GetTargetPointAtDistance(minDistance);
        }

    }

    private Vector2 GetMousePos()
    {
        return m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    }

    void StartGrapple()
    {
        m_lineRenderer.positionCount = precision;
        Vector2 distanceVector = GetMousePos() - (Vector2)transform.position;
        RaycastHit2D _hit = Physics2D.Raycast(transform.position, distanceVector.normalized, maxDistance, ~groundLayer);
        if (_hit.transform != null)
        {
            OnGrappleHit(_hit);
        }
        else
        {
            OnGrappleMiss(distanceVector);
        }

    }

    public void UpdateGrapple()
    {
        m_springJoint2D.autoConfigureDistance = false;
        m_springJoint2D.connectedAnchor = grapplePoint;
        m_springJoint2D.distance = hangDistance;
        m_springJoint2D.frequency = launchSpeed;
        m_springJoint2D.enabled = true;
    }


    public void StopGrapple()
    {
        if (isGrappling)
        {
            m_springJoint2D.enabled = false;
            moveTime = 0;
            waveSize = StartWaveSize;
            straightLine = false;
            tongueTarget = GetTargetPointAtDistance(minDistance);
            isGrappling = false;
            lastGrappleTime = Time.time;

        }
    }


    void OnGrappleHit(RaycastHit2D _hit)
    {
        moveTime = 0;
        waveSize = StartWaveSize;
        straightLine = false;
        isGrappling = true;
        grapplePoint = _hit.point;
        tongueTarget = grapplePoint;
        m_lineRenderer.positionCount = precision;

    }

    void OnGrappleMiss(Vector2 distanceVector)
    {
        moveTime = 0;
        waveSize = StartWaveSize;
        straightLine = false;
        isGrappling = false;
        tongueTarget = GetTargetPointAtDistance(maxDistance);
        grapplePoint = tongueTarget;
        m_lineRenderer.positionCount = precision;

    }



    void DrawTongue()
    {
        if (!straightLine)
        {
            if (Math.Abs(m_lineRenderer.GetPosition(precision - 1).x - tongueTarget.x) <= 0.1 && Math.Abs(m_lineRenderer.GetPosition(precision - 1).y - tongueTarget.y) <= 0.1)
            {
                straightLine = true;
            }
            else
            {
                DrawTongueWaves();
            }
        }
        else
        {
            if (waveSize > 0)
            {
                waveSize -= Time.deltaTime * straightenLineSpeed;
                DrawTongueWaves();
            }
            else
            {
                waveSize = 0;

                DrawTongueNoWaves();
            }
        }

    }

    void DrawTongueWaves()
    {
        m_lineRenderer.positionCount = precision;
        Vector2 grappleDistanceVector = tongueTarget - (Vector2)transform.position;

        for (int i = 0; i < precision; i++)
        {
            float delta = (float)i / ((float)precision - 1f);
            Vector2 offset = Vector2.Perpendicular(grappleDistanceVector).normalized * tongueAnimationCurve.Evaluate(delta) * waveSize;
            Vector2 targetPosition, currentPosition;
            if (isGrappling)
            {
                targetPosition = Vector2.Lerp(transform.position, tongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(transform.position, targetPosition, tongueProgressionCurve.Evaluate(moveTime) * tongueProgressionSpeed);
            }
            else
            {
                targetPosition = Vector2.Lerp(grapplePoint, tongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(targetPosition, tongueTarget, tongueProgressionCurve.Evaluate(moveTime) * tongueProgressionSpeed);
            }

            m_lineRenderer.SetPosition(i, currentPosition);
        }
    }

    void DrawTongueNoWaves()
    {

        m_lineRenderer.SetPosition(0, transform.position);

        for (int i = 1; i < precision; i++)
        {
            m_lineRenderer.SetPosition(i, tongueTarget);

        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        Gizmos.DrawWireSphere(tongueTarget, 0.1f);

    }

}
