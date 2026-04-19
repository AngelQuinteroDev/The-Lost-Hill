using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(PlayerInput))]
public class PlayerControllerM : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;

    [Header("C�mara primera persona")]
    public Transform playerCamera;
    public Camera mainCamera;
    public float mouseSensitivity = 2f;
    public float cameraHeightOffset = 1.7f;
    private float cameraPitch = 0f;

    [Header("Recogida")]
    public float pickupRayLength = 3f;

    [Header("Respawn")]
    public Transform spawnPoint;

    [Header("Multijugador")]
    public Renderer[] playerMeshRenderers;

    private CharacterController cc;
    private Animator anim;
    private Vector3 velocity;
    private bool isAlive = true;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private static readonly int AnimWalk = Animator.StringToHash("isWalking");
    private static readonly int AnimRun = Animator.StringToHash("isRunning");
    private static readonly int AnimIdle = Animator.StringToHash("isIdle");
    private static readonly int AnimPickup = Animator.StringToHash("isPickingUp");

    void Start()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera != null)
        {
            playerCamera.SetParent(transform);
            playerCamera.localPosition = new Vector3(0f, cameraHeightOffset, 0.2f);
            playerCamera.localRotation = Quaternion.identity;
        }

        if (mainCamera == null && playerCamera != null)
            mainCamera = playerCamera.GetComponentInChildren<Camera>();

        SetupLayers();

        PlayerInput pi = GetComponent<PlayerInput>();
        pi.actions["Interact"].started += ctx => TryPickup();
    }

    void SetupLayers()
    {
        int localLayer = LayerMask.NameToLayer("LocalPlayerMesh");
        int remoteLayer = LayerMask.NameToLayer("RemotePlayerMesh");

        if (localLayer == -1 || remoteLayer == -1) return;

        bool local = IsLocalPlayer();

        foreach (Renderer r in playerMeshRenderers)
            r.gameObject.layer = local ? localLayer : remoteLayer;

        if (playerCamera != null && local)
        {
            Camera cam = playerCamera.GetComponentInChildren<Camera>();
            if (cam != null)
                cam.cullingMask &= ~(1 << localLayer);
        }
    }

    bool IsLocalPlayer() => true;

    void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => lookInput = value.Get<Vector2>();
    void OnSprint(InputValue value) { }

    public void TryPickup()
    {
        if (!isAlive || mainCamera == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * pickupRayLength, Color.green, 2f);

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRayLength)) return;

        Debug.Log("[Player] Golpeo: " + hit.collider.gameObject.name);

        CollectibleItem item = hit.collider.GetComponent<CollectibleItem>();
        if (item == null) return;

        item.Collect();
        StartCoroutine(PickupAnimation());
    }

    IEnumerator PickupAnimation()
    {
        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, false);
        anim.SetBool(AnimPickup, true);

        yield return new WaitForSeconds(1f);

        anim.SetBool(AnimPickup, false);
        anim.SetBool(AnimIdle, true);
    }

    void Update()
    {
        if (!isAlive) return;
        HandleCamera();
        HandleMovement();
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
    }

    public void OnCaughtByEnemy()
    {
        if (!isAlive) return;
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        isAlive = false;

        yield return new WaitForSeconds(1.5f);

        cc.enabled = false;
        transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        cc.enabled = true;

        cameraPitch = 0f;
        if (playerCamera != null)
            playerCamera.localEulerAngles = Vector3.zero;

        isAlive = true;
        moveInput = Vector2.zero;

        anim.SetBool(AnimRun, false);
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimIdle, true);
        anim.SetBool(AnimPickup, false);
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
}