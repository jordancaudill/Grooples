using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{

    public readonly SyncVar<int> playerNumber = new SyncVar<int>();

    private PlayerMovement playerMovement;
    public Rigidbody2D rb;
    public GameObject tongue;
    public SpringJoint2D springJoint2D;
    private GameObject localGrappleObject;
    GameManager gameManager;
    private bool localPopup = false;

    private void OnPlayerNumberChanged(int previous, int next, bool asServer)
    {
        AssignSpriteToPlayer();
    }


    public PredictionRigidbody2D PredictionRigidbody;

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
        // the network id of the object being grappled to, if any
        public int GrappleObjectId;
        // Offset from the grapple object's position to the grapple point
        public Vector2 GrappleOffset;


        public ReplicateData(Vector2 move, Vector2 teleport, Vector2 aim, bool popup, int grappleObjectId, Vector2 grappleOffset) : this()
        {
            Move = move;
            Teleport = teleport;
            Aim = aim;
            Popup = popup;
            GrappleObjectId = grappleObjectId;
            GrappleOffset = grappleOffset;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;


    }



    private GameObject GetObjectById(int networkObjectId)
    {
        NetworkObject found;
        Dictionary<int, NetworkObject> spawned = IsServerInitialized ? NetworkManager.ServerManager.Objects.Spawned : NetworkManager.ClientManager.Objects.Spawned;
        if (spawned.TryGetValue(networkObjectId, out found))
        {
            return found.gameObject;
        }
        return null;

    }

    public struct ReconcileData : IReconcileData
    {
        //PredictionRigidbody is used to synchronize rigidbody states
        //and forces. This could be done manually but the PredictionRigidbody
        //type makes this process considerably easier. Velocities, kinematic state,
        //transform properties, pending velocities and more are automatically
        //handled with PredictionRigidbody.
        public PredictionRigidbody2D PredictionRigidbody;

        public ReconcileData(PredictionRigidbody2D pr) : this()
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
        playerNumber.OnChange += OnPlayerNumberChanged;

    }
    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody2D>.StoreAndDefault(ref PredictionRigidbody);
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
            NetworkObject networkObject = playerMovement.grappleObject.GetComponent<NetworkObject>();
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
            grappleObjectNetworkId,
            playerMovement.grappleOffset
        );


        // Reset local single-frame inputs
        localPopup = false;
        return md;
    }

    [Replicate]
    private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
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
            PredictionRigidbody.AddForce(new Vector2(data.Move.x * playerMovement.moveSpeed, 0), ForceMode2D.Force);
        }

        if (data.Popup)
        {
            //Vector2 distanceVector = (Vector2)tongue.transform.position - data.Aim;
            PredictionRigidbody.AddForce(data.Aim * -1 * playerMovement.popupForce, ForceMode2D.Impulse);
            print("popup" + playerNumber.Value + " | " + IsOwner + " | " + data.Aim);


        }

        if (localGrappleObject == null && data.GrappleObjectId != 0)
        {
            // Store localGrappleObject so we don't have to look it up every tick.
            print("grappling");
            localGrappleObject = GetObjectById(data.GrappleObjectId);
            if (localGrappleObject != null)
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

        if (!IsOwner)
        {
            if (playerMovement != null)
            {
                playerMovement.aimDirection = data.Aim;
                playerMovement.grappleOffset = data.GrappleOffset;
                playerMovement.grappleObject = localGrappleObject;
                if (localGrappleObject != null)
                {
                    playerMovement.lastGrapplePoint = (Vector2)localGrappleObject.transform.TransformPoint(data.GrappleOffset);

                }

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


    public override void OnStartClient()
    {

        if (IsOwner)
        {
            GetComponent<PlayerInput>().enabled = true;
            AddPlayer();
        }
        playerMovement = GetComponent<PlayerMovement>();

    }

    private void Start()
    {
        if (IsServerStarted)
        {
            gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();

        }
    }

    [ServerRpc]
    private void AddPlayer()
    {
        playerNumber.Value = gameManager.playerCount.Value;
        gameManager.playerCount.Value++;
        name = "Player_" + playerNumber.Value;

    }

    private void AssignSpriteToPlayer()
    {
        Dictionary<int, string> playerSprites = new Dictionary<int, string>
        {
            { 0, "Sprites/player_pink" },
            { 1, "Sprites/player_green" },
            { 2, "Sprites/player_yellow" },
            { 3, "Sprites/player_purple" },
        };
        string playerSprite = playerSprites[playerNumber.Value];
        GameObject graphicObject = transform.Find("GraphicalObject").gameObject;
        Sprite resource = Resources.Load<Sprite>(playerSprite);
        graphicObject.GetComponent<SpriteRenderer>().sprite = resource;


        // Assign a color to the player based on the OwnerClientId or any other logic
        Dictionary<int, string> playerColors = new Dictionary<int, string>
        {
            { 0, "#EA61CC" },
            { 1, "#689581" },
            { 2, "#E9AC6F" },
            { 3, "#9576A6" },
        };

        string playerColor = playerColors[playerNumber.Value];
        ColorUtility.TryParseHtmlString(playerColor, out Color color);

        tongue.GetComponent<LineRenderer>().colorGradient = new Gradient
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
}