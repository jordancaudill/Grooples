using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleGun : MonoBehaviour
{
    [Header("Scripts Ref:")]
    public GrappleRope grappleRope;

    [Header("Layers Settings:")]
    [SerializeField] private bool grappleToAll = false;
    [SerializeField] private int grappableLayerNumber = 9;

    [Header("Main Camera:")]
    public Camera m_camera;

    [Header("Transform Ref:")]
    public Transform gunHolder;
    public Transform gunPivot;
    public Transform firePoint;

    [Header("Physics Ref:")]
    public SpringJoint2D m_springJoint2D;
    public Rigidbody2D m_rigidbody;

    [Header("Rotation:")]
    [SerializeField] private bool rotateOverTime = true;
    [Range(0, 60)][SerializeField] private float rotationSpeed = 4;

    [Header("Distance:")]
    [SerializeField] private float maxDistance = 20;

    InputAction grapplingHookAction;

    [Header("Launching:")]
    [SerializeField] private float launchSpeed = 1;
    [SerializeField] private float hangDistance = 2f;


    [HideInInspector] public Vector2 grapplePoint;
    [HideInInspector] public Vector2 grappleDistanceVector;
    [HideInInspector] public bool isGrappling = true;

    private void Start()
    {
        grapplingHookAction = InputSystem.actions.FindAction("GrapplingHook");
        grappleRope.enabled = false;
        m_springJoint2D.enabled = false;
    }

    private void Update()
    {
        if (grapplingHookAction.WasPressedThisFrame())
        {
            StartGrapple();
        }
        else if (grapplingHookAction.IsPressed() && isGrappling)
        {
            UpdateGrapple();

            if (grappleRope.enabled)
            {
                RotateGun(grapplePoint, false);
            }
            else
            {
                Vector2 mousePos = m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                RotateGun(mousePos, true);
            }

        }
        else if (grapplingHookAction.WasReleasedThisFrame())
        {
            StopGrapple();
        }
        else
        {
            Vector2 mousePos = m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            RotateGun(mousePos, true);
        }
    }

    public void StopGrapple()
    {
        grappleRope.enabled = false;
        isGrappling = false;
        m_springJoint2D.enabled = false;
        m_rigidbody.AddForce(firePoint.position - gunHolder.position);

    }

    void RotateGun(Vector3 lookPoint, bool allowRotationOverTime)
    {
        Vector3 distanceVector = lookPoint - gunPivot.position;

        float angle = Mathf.Atan2(distanceVector.y, distanceVector.x) * Mathf.Rad2Deg;
        if (rotateOverTime && allowRotationOverTime)
        {
            gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * rotationSpeed);
        }
        else
        {
            gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    void StartGrapple()
    {
        Vector2 distanceVector = m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - gunPivot.position;
        if (Physics2D.Raycast(firePoint.position, distanceVector.normalized))
        {
            RaycastHit2D _hit = Physics2D.Raycast(firePoint.position, distanceVector.normalized);
            grappleRope.enabled = true;

            if (CheckGrappleHit(_hit))
            {
                OnGrappleHit(_hit);
            }
            else
            {
                OnGrappleMiss(distanceVector);
            }
        }
    }

    bool CheckGrappleHit(RaycastHit2D _hit)
    {
        return (_hit.transform.gameObject.layer == grappableLayerNumber || grappleToAll) && (Vector2.Distance(_hit.point, firePoint.position) <= maxDistance);
    }

    void OnGrappleHit(RaycastHit2D _hit)
    {
        isGrappling = true;
        grapplePoint = _hit.point;
        grappleDistanceVector = grapplePoint - (Vector2)gunPivot.position;

    }

    void OnGrappleMiss(Vector2 distanceVector)
    {
        Vector3 mouseWorld = m_camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f; // Ensure 2D

        Vector3 beamStart = firePoint.position;
        Vector3 beamDirection = (mouseWorld - beamStart).normalized;

        // Calculate beam end at 10 units
        Vector3 beamEnd = beamStart + beamDirection * maxDistance;

        grapplePoint = beamEnd;

        isGrappling = false;
        // Set the grapplePoint so it is at the point that is the direction the gun is pointing at the max distance
        grappleDistanceVector = grapplePoint - (Vector2)gunPivot.position;

    }

    public void UpdateGrapple()
    {
        m_springJoint2D.autoConfigureDistance = false;
        m_springJoint2D.connectedAnchor = grapplePoint;

        Vector2 distanceVector = firePoint.position - gunHolder.position;

        if (distanceVector.magnitude > hangDistance)
        {
            m_springJoint2D.distance = distanceVector.magnitude;
        }
        else
        {
            m_springJoint2D.distance = hangDistance;
        }
        m_springJoint2D.frequency = launchSpeed;
        m_springJoint2D.enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (firePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(firePoint.position, maxDistance);
        }
    }

}
