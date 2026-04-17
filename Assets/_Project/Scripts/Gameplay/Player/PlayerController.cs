using UnityEngine;
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

        private CharacterController _cc;
        private ClientSidePrediction _prediction;
        
        // Asignado por el manager al instanciar este prefab
        public bool IsLocalPlayer { get; set; } = false;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _prediction = GetComponent<ClientSidePrediction>();
        }

        private void Update()
        {
            if (!IsLocalPlayer) return;

            // 1. Recoger Inputs
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            Vector3 inputDir = new Vector3(h, 0, v).normalized;

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
