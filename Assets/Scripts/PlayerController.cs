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
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Grapple")]
    public GrappleGun grappleGun;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask terrainLayer;

    [Header("Bounds Check")]
    public LayerMask boundsLayer;
    private Transform spawnPoint;

    public Rigidbody2D rb;

    public bool isGrounded;
    private Vector2 moveInput;

    [SerializeField] public Collider2D[] playerColliders;

    public PlayerInput playerInput;
    public PlayerInputManager playerInputManager;
    [SerializeField] private float popupForce = 500f;

    public readonly SyncVar<int> playerNumber = new SyncVar<int>();


    private void OnPlayerNumberChanged(int previous, int next, bool asServer)
    {
        AssignSpriteToPlayer();
    }

    GameManager gameManager;

    public PredictionRigidbody2D PredictionRigidbody;

    public struct ReplicateData : IReplicateData
    {
        public Vector2 Move;
        public ReplicateData(Vector2 move) : this()
        {
            Move = move;
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
        ReplicateData md = new ReplicateData(moveInput);
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

        if (data.Move != Vector2.zero)
        {
            PredictionRigidbody.AddForce(new Vector2(data.Move.x * moveSpeed, 0), ForceMode2D.Force);
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
       
        playerInputManager = GameObject.Find("PlayerInputManager").GetComponent<PlayerInputManager>();
        spawnPoint = GameObject.Find("Spawn").transform;

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


    void FixedUpdate()
    {
        if (CheckOutOfBounds())
        {
            rb.position = spawnPoint.position;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

        }

        if (!IsOwner) return;

        // Check if grounded
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, terrainLayer);

    }




    public void OnLook(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme == "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
        }

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (playerInput.currentControlScheme != "Gamepad")
        {
            moveInput = context.ReadValue<Vector2>();
        }


    }



    //public void PopupPlayer(Vector2 point)
    //{
    //    Vector2 distanceVector = (Vector2)grappleGun.transform.position - point;
    //    rb.AddForce(distanceVector.normalized * popupForce, ForceMode2D.Force);
    //    grappleGun.pendingPopup = false;

    //}


    ///////////////////////// MovePlayer
    //public void MovePlayer(Vector2 input)
    //{
    //    rb.AddForce(new Vector2(input.x * moveSpeed, 0), ForceMode2D.Force);
    //}


    //[Rpc(SendTo.Server)]
    //public void MovePlayerServerRpc(Vector2 input)
    //{
    //    pendingInput = input;
    //}

    //[Rpc(SendTo.NotServer)]
    //public void MovePlayerClientRpc(StateSnapshot snapshot)
    //{
    //    stateSnapshot = snapshot;
    //}

    ///////////////////////// UpdatePlayerTransform
    //public void UpdatePlayerTransform(StateSnapshot snapshot)
    //{
    //    rb.position = snapshot.position;
    //    rb.rotation = snapshot.rotation;
    //    rb.linearVelocity = snapshot.velocity;
    //    rb.angularVelocity = snapshot.angularVelocity;
    //}


    //[Rpc(SendTo.Server)]
    //public void UpdatePlayerTransformServerRpc(StateSnapshot snapshot)
    //{
    //    UpdatePlayerTransform(snapshot);
    //    UpdatePlayerTransformClientRpc(snapshot);
    //}

    //[Rpc(SendTo.NotServer)]
    //public void UpdatePlayerTransformClientRpc(StateSnapshot snapshot)
    //{
    //    stateSnapshot = snapshot;
    //}



    ///////////////////////// Utils

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

    bool CheckOutOfBounds()
    {
        return Physics2D.OverlapCircle(rb.position, 1f, boundsLayer);
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