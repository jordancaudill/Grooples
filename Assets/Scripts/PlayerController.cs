using FishNet.Managing.Client;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{

    public readonly SyncVar<int> playerNumber = new SyncVar<int>();

    private PlayerMovement playerMovement;
    public Rigidbody2D rb;
    public GameObject tongue;
    GameManager gameManager;


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
        public Vector2 Popup;

        public ReplicateData(Vector2 move, Vector2 teleport, Vector2 aim, Vector2 popup) : this()
        {
            Move = move;
            Teleport = teleport;
            Aim = aim;
            Popup = popup;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
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

    private void TimeManager_OnTick()
    {
        RunInputs(CreateReplicateData());
    }

    private ReplicateData CreateReplicateData()
    {
        if (!IsOwner)
            return default;

        //Build the replicate data with all inputs which affect the prediction.
        ReplicateData md = new ReplicateData(playerMovement.moveInput, playerMovement.teleport, playerMovement.aimDirection, playerMovement.popup);
        return md;
    }

    [Replicate]
    private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        /* ReplicateState is set based on if the data is new, being replayed, ect.
        * Visit the ReplicationState enum for more information on what each value
        * indicates. At the end of this guide a more advanced use of state will
        * be demonstrated. */

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

        if (data.Popup != Vector2.zero)
        {
            Vector2 distanceVector = (Vector2)tongue.transform.position - data.Popup;
            PredictionRigidbody.AddForce(distanceVector.normalized * playerMovement.popupForce, ForceMode2D.Force);

            playerMovement.popup = Vector2.zero;
        }
        //Add gravity to make the object fall faster. This is of course
        //entirely optional.
        //PredictionRigidbody.AddForce(Physics.gravity * 3f);

        //Simulate the added forces.
        //Typically you call this at the end of your replicate. Calling
        //Simulate is ultimately telling the PredictionRigidbody to iterate
        //the forces we added above.
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
    }
}