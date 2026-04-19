using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(PlayerInput))]
public class PlayerControllerM : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;

    [Header("Cámara primera persona")]
    public Transform playerCamera;
    public Camera mainCamera;
    public float mouseSensitivity = 2f;
    public float cameraHeightOffset = 1.7f;
    private float cameraPitch = 0f;

    [Header("Recogida")]
    public float pickupRayLength = 3f;

    [Header("Respawn")]
    public Transform spawnPoint;
    public float respawnDelay = 10f;

    [Header("UI Muerte")]
    public GameObject respawnPanel;
    public TMP_Text countdownText;

    [Header("Multijugador")]
    public Renderer[] playerMeshRenderers;

    private CharacterController cc;
    private Animator anim;
    private Vector3 velocity;
    private bool isAlive = true;
    private bool canMove = true;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private static readonly int AnimWalk = Animator.StringToHash("isWalking");
    private static readonly int AnimRun = Animator.StringToHash("isRunning");
    private static readonly int AnimIdle = Animator.StringToHash("isIdle");
    private static readonly int AnimPickup = Animator.StringToHash("isPickingUp");
    private static readonly int AnimDeath = Animator.StringToHash("isDead");

    private PlayerInput _playerInput;
    private InputAction _interactAction;
    private bool _isLocalPlayer = true;

    public bool IsLocalPlayer => _isLocalPlayer;
    public bool NetIsMoving { get; private set; }
    public bool NetIsRunning { get; private set; }
    public bool NetIsPickingUp { get; private set; }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        _playerInput = GetComponent<PlayerInput>();
    }

    void Start()
    {
        if (playerCamera != null)
        {
            playerCamera.SetParent(transform);
            playerCamera.localPosition = new Vector3(0f, cameraHeightOffset, 0.2f);
            playerCamera.localRotation = Quaternion.identity;
        }

        if (mainCamera == null && playerCamera != null)
            mainCamera = playerCamera.GetComponentInChildren<Camera>();

        ApplyOwnershipState();
        SetupLayers();

        if (_playerInput != null && _playerInput.actions != null)
        {
            _interactAction = _playerInput.actions["Interact"];
            if (_interactAction != null)
                _interactAction.started += OnInteract;
        }

        if (respawnPanel != null) respawnPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_interactAction != null)
            _interactAction.started -= OnInteract;
    }

    void SetupLayers()
    {
        int localLayer = LayerMask.NameToLayer("LocalPlayerMesh");
        int remoteLayer = LayerMask.NameToLayer("RemotePlayerMesh");

        if (localLayer == -1 || remoteLayer == -1) return;

        bool local = _isLocalPlayer;

        foreach (Renderer r in playerMeshRenderers)
            r.gameObject.layer = local ? localLayer : remoteLayer;

        if (playerCamera != null && local)
        {
            Camera cam = playerCamera.GetComponentInChildren<Camera>();
            if (cam != null)
                cam.cullingMask &= ~(1 << localLayer);
        }
    }

    private void ApplyOwnershipState()
    {
        if (_playerInput != null) _playerInput.enabled = _isLocalPlayer;
        if (mainCamera != null) mainCamera.enabled = _isLocalPlayer;

        var listener = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
        if (listener != null) listener.enabled = _isLocalPlayer;

        if (anim != null && !_isLocalPlayer)
            anim.applyRootMotion = false;

        if (cc != null && !_isLocalPlayer)
            cc.enabled = false;

        if (_isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!isAlive || !_isLocalPlayer) return;
        HandleCamera();
        if (canMove) HandleMovement();
        else StopMovement();
    }

    void HandleCamera()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime * 100f;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime * 100f;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -80f, 80f);
        if (playerCamera != null)
            playerCamera.localEulerAngles = Vector3.right * cameraPitch;
    }

    void HandleMovement()
    {
        if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;

        bool moving = moveInput.magnitude > 0.1f;
        bool running = Keyboard.current != null &&
                       Keyboard.current.leftShiftKey.isPressed &&
                       moving;
        float speed = running ? runSpeed : walkSpeed;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        cc.Move(move * speed * Time.deltaTime);

        velocity.y += Physics.gravity.y * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);

        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, false);

        if (running) anim.SetBool(AnimRun, true);
        else if (moving) anim.SetBool(AnimWalk, true);
        else anim.SetBool(AnimIdle, true);

        NetIsMoving = moving;
        NetIsRunning = running;
    }

    void StopMovement()
    {
        velocity = Vector3.zero;
        moveInput = Vector2.zero;

        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, false);
    }

    public void OnCaughtByEnemy()
    {
        if (!isAlive) return;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        isAlive = false;
        canMove = false;

        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, false);
        anim.SetBool(AnimPickup, false);
        anim.SetBool(AnimDeath, true);

        if (respawnPanel != null) respawnPanel.SetActive(true);

        float remaining = respawnDelay;
        while (remaining > 0f)
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
                countdownText.gameObject.SetActive(true);
                countdownText.text = "Reapareciendo en " + Mathf.CeilToInt(remaining) + "...";
                countdownText.ForceMeshUpdate();
            }

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (respawnPanel != null) respawnPanel.SetActive(false);
        if (countdownText != null) countdownText.text = "";

        anim.SetBool(AnimDeath, false);

        cc.enabled = false;
        transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        cc.enabled = true;

        cameraPitch = 0f;
        if (playerCamera != null)
            playerCamera.localEulerAngles = Vector3.zero;

        isAlive = true;
        canMove = true;
        moveInput = Vector2.zero;

        anim.SetBool(AnimIdle, true);

        NetIsMoving = false;
        NetIsRunning = false;
        NetIsPickingUp = false;
    }

    void OnDrawGizmosSelected()
    {
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(ray.origin, ray.direction * pickupRayLength);
        }
    }

    public void Initialize(bool isLocalPlayer)
    {
        _isLocalPlayer = isLocalPlayer;
        ApplyOwnershipState();
        SetupLayers();
    }

    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
    public void OnLook(InputValue value) => lookInput = value.Get<Vector2>();
    public void OnSprint(InputValue value) { }

    private void OnInteract(InputAction.CallbackContext ctx) => TryPickup();

    public void TryPickup()
    {
        if (!isAlive || !canMove || !_isLocalPlayer || mainCamera == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * pickupRayLength, Color.green, 1.5f);

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRayLength)) return;

        CollectibleItem item = hit.collider.GetComponent<CollectibleItem>();
        if (item == null) return;

        item.Collect();
        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        NetIsPickingUp = true;

        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, false);
        anim.SetBool(AnimPickup, true);

        yield return new WaitForSeconds(1f);

        anim.SetBool(AnimPickup, false);
        anim.SetBool(AnimIdle, true);

        NetIsPickingUp = false;
    }

    public void ApplyRemoteInput(float inputX, float inputZ, bool sprint)
    {
        if (_isLocalPlayer) return;

        Vector3 inputDir = new Vector3(inputX, 0f, inputZ);
        NetIsMoving = inputDir.sqrMagnitude > 0.0001f;
        NetIsRunning = sprint && NetIsMoving;
        NetIsPickingUp = false;

        if (NetIsMoving)
        {
            Quaternion targetRot = Quaternion.LookRotation(inputDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }

        float speed = NetIsRunning ? runSpeed : walkSpeed;
        Vector3 move = inputDir.normalized * speed;

        if (cc != null && cc.enabled)
            cc.Move(move * Time.deltaTime);
        else
            transform.position += move * Time.deltaTime;

        if (anim != null)
        {
            anim.SetBool(AnimRun, NetIsRunning);
            anim.SetBool(AnimWalk, NetIsMoving && !NetIsRunning);
            anim.SetBool(AnimIdle, !NetIsMoving);
            anim.SetBool(AnimPickup, false);
        }
    }
}