using UnityEngine;
using UnityEngine.InputSystem;
using TheLostHill.Network.Sync;

namespace TheLostHill.Gameplay.Player
{
  
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ClientSidePrediction))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float WalkSpeed = 3.5f;
        public float SprintSpeed = 6.0f;
        public float RotationSpeed = 10f;

        [Header("Components")]
        public Camera PlayerCamera;
        public AudioListener PlayerListener;

        private CharacterController _cc;
        private ClientSidePrediction _prediction;

        private bool _isLocalPlayer = false;
        public bool IsLocalPlayer
        {
            get => _isLocalPlayer;
            set
            {
                if (_isLocalPlayer == value) return;
                _isLocalPlayer = value;
                ApplyOwnershipState();
            }
        }

        public bool NetIsMoving { get; private set; }
        public bool NetIsRunning { get; private set; }
        public bool NetIsPickingUp { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _prediction = GetComponent<ClientSidePrediction>();
            ApplyOwnershipState();
        }

        private void Start()
        {
            ApplyOwnershipState();
        }

        public void Initialize(bool isLocalPlayer)
        {
            IsLocalPlayer = isLocalPlayer;
        }

        private void ApplyOwnershipState()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_prediction == null) _prediction = GetComponent<ClientSidePrediction>();

            if (PlayerCamera != null) PlayerCamera.enabled = _isLocalPlayer;
            if (PlayerListener != null) PlayerListener.enabled = _isLocalPlayer;
            if (_cc != null) _cc.enabled = _isLocalPlayer;
            if (_prediction != null) _prediction.enabled = _isLocalPlayer;
        }

        private void Update()
        {
            if (!IsLocalPlayer) return;

           
            Vector2 moveInput = Vector2.zero;
            bool sprint = false;

            if (Keyboard.current != null)
            {
                float h = 0;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1;

                float v = 0;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1;

                moveInput = new Vector2(h, v);
                sprint = Keyboard.current.leftShiftKey.isPressed;
            }

            Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;

            NetIsMoving = inputDir.sqrMagnitude > 0.0001f;
            NetIsRunning = sprint && NetIsMoving;
            NetIsPickingUp = false;

            
            if (inputDir.magnitude > 0.01f)
            {
           
                Quaternion targetRot = Quaternion.LookRotation(inputDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
            }

            _prediction.ProcessLocalInput(inputDir, sprint, Time.deltaTime);
        }

        public void ApplyRemoteInput(float inputX, float inputZ, bool sprint)
        {
            if (IsLocalPlayer || _cc == null) return;

            Vector3 inputDir = new Vector3(inputX, 0f, inputZ);
            NetIsMoving = inputDir.sqrMagnitude > 0.0001f;
            NetIsRunning = sprint && NetIsMoving;
            NetIsPickingUp = false;

            if (inputDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(inputDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
            }

            float speed = sprint ? SprintSpeed : WalkSpeed;
            _cc.Move(inputDir.normalized * speed * Time.deltaTime);
        }
    }
}
