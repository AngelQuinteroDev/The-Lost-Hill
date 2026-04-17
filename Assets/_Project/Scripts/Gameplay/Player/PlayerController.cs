using UnityEngine;
using UnityEngine.InputSystem;
using TheLostHill.Network.Sync;

namespace TheLostHill.Gameplay.Player
{
    /// <summary>
    /// Controlador físico y de input del jugador local.
    /// Para clientes o host locales. Actúa en conjunto con ClientSidePrediction.
    /// </summary>
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
        
        // Asignado por el manager al instanciar este prefab
        public bool IsLocalPlayer { get; set; } = false;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _prediction = GetComponent<ClientSidePrediction>();
        }

        private void Start()
        {
            // Configuración de cámara
            if (PlayerCamera != null)
            {
                PlayerCamera.enabled = IsLocalPlayer;
                // También el listener de audio para evitar warnings de Unity
                if (PlayerListener != null) PlayerListener.enabled = IsLocalPlayer;
            }
        }

        private void Update()
        {
            if (!IsLocalPlayer) return;

            // 1. Recoger Inputs (Nueva API de Input System)
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

            // 2. Client Side Prediction lo procesa y aplica al CC localmente
            // Además guardará este input para enviarlo por red (PlayerNetworkSync se encarga)
            if (inputDir.magnitude > 0.01f)
            {
                // Rotar el modelo (esto también se interpola en remoto)
                Quaternion targetRot = Quaternion.LookRotation(inputDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
            }

            _prediction.ProcessLocalInput(inputDir, sprint, Time.deltaTime);
        }
    }
}
