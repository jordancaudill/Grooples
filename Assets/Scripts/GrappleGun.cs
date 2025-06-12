using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleGun : MonoBehaviour
{

    [Header("General References:")]
    public LineRenderer m_lineRenderer;
    public PlayerController m_player;
    public UIController m_ui;

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
    InputAction lookAction;


    [Header("Launching:")]
    [SerializeField] private float launchSpeed = 1;
    [SerializeField] private float hangDistance = 2f;


    private Vector2 tongueTarget;
    private Vector2 grapplePoint;
    public bool isGrappling = false;

    public float jumpForce = 500f;
    public float minJumpDistance = 2f;
    public float grappleCooldown = 0.25f;
    private float lastGrappleTime = -10f;

    // https://bitbucket.org/Vespper/grappling-hook/src/master/GrappleRope


    private void Start()
    {
        grapplingHookAction = InputSystem.actions.FindAction("GrapplingHook");
        lookAction = InputSystem.actions.FindAction("Look");
        tongueTarget = transform.position;
        grapplePoint = transform.position;
        isGrappling = false;
        m_springJoint2D.enabled = false;
        m_springJoint2D.autoConfigureDistance = false;
        m_springJoint2D.distance = hangDistance;
        m_springJoint2D.frequency = launchSpeed;
        m_lineRenderer.positionCount = precision;


    }

    private void Update()
    {
        if (!m_ui.isPaused)
        {
            moveTime += Time.deltaTime;
            CheckGrappleHookAction();
            UpdateTongueTarget();
            DrawTongue();
        }
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
            StartGrapple();
        }
        else if (!grapplingHookAction.IsPressed())
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

    void StartGrapple()
    {
        Vector2 distanceVector = GetLookPos() - (Vector2)transform.position;
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
        tongueTarget = _hit.point;

        float distance = Vector2.Distance(_hit.point, transform.position);

        if (m_player.isGrounded && distance <= minJumpDistance)
        {
            // If the player is grounded and the distance is less than the minimum, do not grapple
            Vector2 distanceVector = (Vector2)transform.position - _hit.point;
            m_rigidbody.AddForce(distanceVector.normalized * jumpForce, ForceMode2D.Force);
            grapplePoint = transform.position;
        }
        else
        {
            isGrappling = true;
            grapplePoint = _hit.point;
            m_springJoint2D.connectedAnchor = grapplePoint;
            m_springJoint2D.enabled = true;

        }

    }

    void OnGrappleMiss(Vector2 distanceVector)
    {
        moveTime = 0;
        waveSize = StartWaveSize;
        straightLine = false;
        isGrappling = false;
        tongueTarget = GetTargetPointAtDistance(maxDistance);
        grapplePoint = tongueTarget;

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
