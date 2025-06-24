using FishNet.Object;
using FishNet.Object.Prediction;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Tongue : NetworkBehaviour
{
    [Header("Tongue Animation Settings:")]
    public LineRenderer lineRenderer;
    public AnimationCurve tongueAnimationCurve;
    public AnimationCurve tongueProgressionCurve;

    [Range(0.01f, 4)][SerializeField] private float startTongueWaveSize = 2;
    [Range(0, 50)][SerializeField] private float straightenLineSpeed = 5;
    [SerializeField][Range(1, 50)] private float tongueProgressionSpeed = 1;
    [SerializeField] private int tonguePrecisionPoints = 40;
    public float defaultTongueLength = 2f;

    private float tongueDrawTime = 0;
    private float tongueWaveSize = 0;
    private bool straightLine = true;

    public PlayerMovement playerMovement;
    //private Vector2 tongueTarget;
    //private Vector2 lastSentTongueTarget;
    //public Vector2 lastGrappleLocation;
    //private float tongueTargetSendInterval = 0.016667f; // 60 times per second
    //private float tongueTargetSendTimer = 0f;
    //private float tongueTargetMinChange = 0.01f; // Only send if look dir changes at least this much



    private void Start()
    {
        lineRenderer.positionCount = tonguePrecisionPoints;
        tongueWaveSize = 0;
        tongueDrawTime = 0;
    }

    private void Update()
    {
        //// Set the tongue progression speed based on the current network latency
        //if (NetworkManager.Singleton.NetworkConfig.NetworkTransport != null)
        //{
        //    float currentRtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.NetworkConfig.NetworkTransport.ServerClientId);
        //    tongueProgressionSpeed = Mathf.Clamp(40f - (currentRtt / 10f), 20f, 40f); // Adjust the range as needed
        //}
        if (IsOwner)
        {
            DrawTongue();

        }

    }


    void DrawTongue()
    {
        // Todo set tongueTarget based on grapple state
        Vector2 tongueTarget = playerMovement.grappleObject != null ? (Vector2)playerMovement.lastGrappleObject.transform.position - playerMovement.grappleOffset : (Vector2)transform.position - (playerMovement.aimDirection * defaultTongueLength * -1);

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
            if (playerMovement.grappleObject != null)
            {
                targetPosition = Vector2.Lerp(transform.position, tongueTarget, delta) + offset;
                currentPosition = Vector2.Lerp(transform.position, targetPosition, tongueProgressionCurve.Evaluate(tongueDrawTime) * tongueProgressionSpeed);
            }
            else
            {
                targetPosition = Vector2.Lerp(transform.position, (Vector2)playerMovement.lastGrappleObject.transform.position - playerMovement.grappleOffset, delta) + offset;
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


}