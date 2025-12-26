using System.Collections;
using System.Collections.Generic;
using FishNet.Managing.Scened;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public readonly SyncVar<int> playerNumber = new SyncVar<int>();

    public PlayerMovement playerMovement;
    public Rigidbody2D rb;
    public Tongue tongue;
    public SpringJoint2D springJoint2D;
    private GameObject localGrappleObject;
    private bool localPopup = false;
    private bool localJump = false;
    private bool localStartGrapple = false;
    private bool localStopGrapple = false;
    private GameManager gameManager;
    private SceneManager sceneManager;

    //private void OnPlayerNumberChanged(int previous, int next, bool asServer)
    //{
    //SetPlayerColor();
    //}

    public PredictionRigidbody2D PredictionRigidbody;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;

    public struct ReplicateData : IReplicateData
    {
        // The player's current movement input
        public Vector2 Move;

        // The position to teleport the player to (if any)
        public Vector2 Teleport;

        // The normalized aim direction (if any)
        public Vector2 Aim;

        // The point the player is popping from
        public bool Popup;

        // Jump input
        public bool Jump;

        // the network id of the object being grappled to, if any
        public int GrappleObjectId;

        // Offset from the grapple object's position to the grapple point
        public Vector2 GrappleOffset;
        public bool StartGrapple;
        public bool StopGrapple;

        public ReplicateData(
            Vector2 move,
            Vector2 teleport,
            Vector2 aim,
            bool popup,
            bool jump,
            int grappleObjectId,
            Vector2 grappleOffset,
            bool startGrapple,
            bool stopGrapple
        )
            : this()
        {
            Move = move;
            Teleport = teleport;
            Aim = aim;
            Popup = popup;
            Jump = jump;
            GrappleObjectId = grappleObjectId;
            GrappleOffset = grappleOffset;
            StartGrapple = startGrapple;
            StopGrapple = stopGrapple;
        }

        private uint _tick;

        public void Dispose() { }

        public uint GetTick() => _tick;

        public void SetTick(uint value) => _tick = value;
    }

    private GameObject GetObjectById(int networkObjectId)
    {
        NetworkObject found;
        Dictionary<int, NetworkObject> spawned = IsServerInitialized
            ? NetworkManager.ServerManager.Objects.Spawned
            : NetworkManager.ClientManager.Objects.Spawned;
        if (spawned.TryGetValue(networkObjectId, out found))
        {
            return found.gameObject;
        }
        return null;
    }

    private bool IsGrounded()
    {
        Vector2 position = rb.position;
        Vector2 direction = Vector2.down;
        RaycastHit2D hit = Physics2D.Raycast(position, direction, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    public struct ReconcileData : IReconcileData
    {
        //PredictionRigidbody is used to synchronize rigidbody states
        //and forces. This could be done manually but the PredictionRigidbody
        //type makes this process considerably easier. Velocities, kinematic state,
        //transform properties, pending velocities and more are automatically
        //handled with PredictionRigidbody.
        public PredictionRigidbody2D PredictionRigidbody;

        public ReconcileData(PredictionRigidbody2D pr)
            : this()
        {
            PredictionRigidbody = pr;
        }

        private uint _tick;

        public void Dispose() { }

        public uint GetTick() => _tick;

        public void SetTick(uint value) => _tick = value;
    }

    private void Awake()
    {
        PredictionRigidbody = ObjectCaches<PredictionRigidbody2D>.Retrieve();
        PredictionRigidbody.Initialize(GetComponent<Rigidbody2D>());
        //playerNumber.OnChange += OnPlayerNumberChanged;
    }

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        sceneManager = FindFirstObjectByType<SceneManager>();
        sceneManager.OnLoadStart += SceneManager_OnLoadStart;
        SetPlayerColor();
    }

    private void SceneManager_OnLoadStart(SceneLoadStartEventArgs args)
    {
        if (IsServerInitialized && args.QueueData.SceneLoadData.SceneLookupDatas[0].Name == "Lobby")
        {
            Despawn();
        }
    }

    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody2D>.StoreAndDefault(ref PredictionRigidbody);
        sceneManager.OnLoadStart -= SceneManager_OnLoadStart;
    }

    public override void OnStartNetwork()
    {
        TimeManager.OnTick += TimeManager_OnTick;
        TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        TimeManager.OnTick -= TimeManager_OnTick;
        TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // For single-frame inputs, capture the input in Update()
        if (!localPopup && playerMovement.popup)
        {
            localPopup = true;
            playerMovement.popup = false; // Reset popup after using it
        }
        // For single-frame inputs, capture the input in Update()
        if (!localJump && playerMovement.jump)
        {
            localJump = true;
            playerMovement.jump = false; // Reset jump after using it
        }
        // For single-frame inputs, capture the input in Update()
        if (!localStartGrapple && playerMovement.startGrapple)
        {
            localStartGrapple = true;
            playerMovement.startGrapple = false; // Reset startGrapple after using it
        }
        if (!localStopGrapple && playerMovement.stopGrapple)
        {
            localStopGrapple = true;
            playerMovement.stopGrapple = false; // Reset startGrapple after using it
        }
    }

    private void TimeManager_OnTick()
    {
        RunInputs(CreateReplicateData());
    }

    private ReplicateData CreateReplicateData()
    {
        if (!IsOwner)
            return default;

        //Build the replicate data with all inputs which affect the prediction.
        int grappleObjectNetworkId = 0;
        if (playerMovement.grappleObject != null)
        {
            NetworkObject networkObject =
                playerMovement.grappleObject.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                grappleObjectNetworkId = networkObject.ObjectId;
            }
        }

        ReplicateData md = new ReplicateData(
            playerMovement.moveInput,
            playerMovement.teleport,
            playerMovement.aimDirection,
            localPopup, // use the stored value
            localJump, // use the stored value
            grappleObjectNetworkId,
            playerMovement.grappleOffset,
            localStartGrapple, // use the stored value
            localStopGrapple // use the stored value
        );

        // Reset local single-frame inputs
        localPopup = false;
        localJump = false;
        localStartGrapple = false;
        localStopGrapple = false;
        return md;
    }

    [Replicate]
    private void RunInputs(
        ReplicateData data,
        ReplicateState state = ReplicateState.Invalid,
        Channel channel = Channel.Unreliable
    )
    {
        //If in the future then simply zero out velocities
        //and exit the method to prevent checking any input logic.
        //Using this approach will allow objects to still collide with
        //the rigidbody.
        // https://fish-networking.gitbook.io/docs/guides/features/prediction/creating-code/understanding-replicatestate/using-states-in-code
        if (state.IsFuture())
        {
            PredictionRigidbody.Velocity(Vector2.zero);
            PredictionRigidbody.AngularVelocity(0);
            return;
        }

        //Be sure to always apply and set velocties using PredictionRigidbody
        //and never on the rigidbody itself; this includes if also accessing from
        //another script.

        if (data.Teleport != Vector2.zero)
        {
            rb.position = data.Teleport;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            playerMovement.teleport = Vector2.zero;
        }

        if (data.Move != Vector2.zero)
        {
            if (localGrappleObject == null)
            {
                bool isGrounded = IsGrounded();
                if (isGrounded)
                {
                    Vector2 currentVelocity = rb.linearVelocity;
                    currentVelocity.x = data.Move.x * playerMovement.moveSpeed;
                    PredictionRigidbody.Velocity(currentVelocity);
                }
                else
                {
                    PredictionRigidbody.AddForce(
                        new Vector2(data.Move.x * playerMovement.moveSpeed, 0),
                        ForceMode2D.Force
                    );
                }
            }
            else
            {
                PredictionRigidbody.AddForce(
                    new Vector2(data.Move.x * playerMovement.hangMoveSpeed, 0),
                    ForceMode2D.Force
                );
            }
        }
        else if (localGrappleObject == null && IsGrounded())
        {
            Vector2 currentVelocity = rb.linearVelocity;
            currentVelocity.x = 0;
            PredictionRigidbody.Velocity(currentVelocity);
        }

        // if (data.Popup)
        // {
        //     PredictionRigidbody.AddForce(
        //         data.Aim * -1 * playerMovement.popupForce,
        //         ForceMode2D.Impulse
        //     );
        // }

        if (data.Jump && IsGrounded())
        {
            PredictionRigidbody.AddForce(
                Vector2.up * playerMovement.popupForce,
                ForceMode2D.Impulse
            );
        }

        if (localGrappleObject == null && data.GrappleObjectId != 0)
        {
            // Store localGrappleObject so we don't have to look it up every tick.
            if (!localGrappleObject)
            {
                localGrappleObject = GetObjectById(data.GrappleObjectId);
            }
            if (!springJoint2D.enabled && localGrappleObject != null)
            {
                springJoint2D.connectedBody = localGrappleObject.GetComponent<Rigidbody2D>();
                springJoint2D.connectedAnchor = data.GrappleOffset;
                springJoint2D.enabled = true;
            }
        }
        else if (localGrappleObject != null && data.GrappleObjectId == 0)
        {
            localGrappleObject = null;
            springJoint2D.enabled = false;
            springJoint2D.connectedBody = null;
            springJoint2D.connectedAnchor = Vector2.zero;
        }

        if (!IsOwner && playerMovement != null)
        {
            if (data.Aim != Vector2.zero)
            {
                playerMovement.aimDirection = data.Aim;
            }

            if (localGrappleObject != null)
            {
                playerMovement.lastGrapplePoint = (Vector2)
                    localGrappleObject.transform.TransformPoint(data.GrappleOffset);
            }
            else if (data.Popup)
            {
                playerMovement.lastGrapplePoint = tongue.transform.TransformPoint(
                    data.Aim * -1 * tongue.defaultTongueLength
                );
            }

            if (data.StartGrapple)
            {
                playerMovement.grappleOffset = data.GrappleOffset;
                playerMovement.grappleObject = localGrappleObject;
                tongue.ResetTongueWaves();
            }
            if (data.StopGrapple)
            {
                playerMovement.grappleOffset = Vector2.zero;
                playerMovement.grappleObject = null;
                tongue.ResetTongueWaves();
            }
        }

        PredictionRigidbody.Simulate();
    }

    private void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }

    //Create the reconcile data here and call your reconcile method.
    public override void CreateReconcile()
    {
        //We must send back the state of the rigidbody. Using your
        //PredictionRigidbody field in the reconcile data is an easy
        //way to accomplish this. More advanced states may require other
        //values to be sent; this will be covered later on.
        ReconcileData rd = new ReconcileData(PredictionRigidbody);
        //Like with the replicate you could specify a channel here, though
        //it's unlikely you ever would with a reconcile.
        ReconcileState(rd);
    }

    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        //Call reconcile on your PredictionRigidbody field passing in
        //values from data.
        PredictionRigidbody.Reconcile(data.PredictionRigidbody);
    }

    private void SetPlayerColor()
    {
        ColorUtility.TryParseHtmlString(
            PlayerColors.playerColors[gameManager.GetPlayerColor(OwnerId)],
            out Color playerColor
        );
        GameObject graphicObject = transform.Find("GraphicalObject").gameObject;
        graphicObject.GetComponent<SpriteRenderer>().color = playerColor;

        ColorUtility.TryParseHtmlString(
            PlayerColors.tongueColors[gameManager.GetPlayerColor(OwnerId)],
            out Color tongueColor
        );

        tongue.gameObject.GetComponent<LineRenderer>().colorGradient = new Gradient
        {
            colorKeys = new GradientColorKey[] { new GradientColorKey(tongueColor, 1f) },
            alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1f, 1f) },
        };
    }

    //[ServerRpc]
    //private void AddPlayer()
    //{
    //    playerNumber.Value = ServerManager.Clients.Count - 1;
    //    name = "Player_" + playerNumber.Value;

    //}

    //private void SetPlayerColor()
    //{
    //string playerColor = PlayerColors.playerColors[playerNumber.Value];
    //ColorUtility.TryParseHtmlString(playerColor, out Color newPlayerColor);
    //GameObject graphicObject = transform.Find("GraphicalObject").gameObject;
    //graphicObject.GetComponent<SpriteRenderer>().color = newPlayerColor;

    //string tongueColor = PlayerColors.tongueColors[playerNumber.Value];
    //ColorUtility.TryParseHtmlString(tongueColor, out Color newTongueColor);

    //tongue.gameObject.GetComponent<LineRenderer>().colorGradient = new Gradient
    //{
    //    colorKeys = new GradientColorKey[]
    //    {
    //        new GradientColorKey(newTongueColor, 1f)
    //    },
    //    alphaKeys = new GradientAlphaKey[]
    //    {
    //        new GradientAlphaKey(1f, 1f)
    //    }
    //};

    //}
}
