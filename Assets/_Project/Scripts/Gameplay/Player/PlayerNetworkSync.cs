using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Sync;
using TheLostHill.Network.Shared;

namespace TheLostHill.Gameplay.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerNetworkSync : MonoBehaviour
    {
        private PlayerController _controller;
        private float _lastSendTime;
        public int AssignedPlayerId { get; set; }

        private InterpolationSystem _interpolation;
        private ClientSidePrediction _prediction;
        private bool _isInitialized;

        [Header("Visuals")]
        public Color[] PlayerColors = new Color[]
        {
            Color.red,
            Color.blue,
            new Color(1f, 0.5f, 0f), // Orange
            Color.green,
            new Color(0.5f, 0f, 0.5f), // Purple
            Color.cyan,
            Color.magenta,
            Color.yellow
        };

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _interpolation = GetComponent<InterpolationSystem>();
            _prediction = GetComponent<ClientSidePrediction>();
        }

        public void Initialize(int playerId, bool isLocalPlayer)
        {
            if (_controller == null) _controller = GetComponent<PlayerController>();

            AssignedPlayerId = playerId;
            _lastSendTime = float.NegativeInfinity;
            _isInitialized = true;

            if (_controller != null)
                _controller.Initialize(isLocalPlayer);
        }

        public void ApplyColor(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= PlayerColors.Length) colorIndex = 0;
            
            Color targetColor = PlayerColors[colorIndex];
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            
            foreach (var renderer in renderers)
            {
                // Crear una copia única del material para este jugador
                renderer.material.color = targetColor;
            }
        }

        private void LateUpdate()
        {
            if (_controller == null) return;

            // Fallback por orden de ciclo: si faltó Initialize, autoinicializar local.
            if (!_isInitialized &&
                _controller.IsLocalPlayer &&
                GameManager.Instance != null &&
                GameManager.Instance.LocalPlayerId > 0)
            {
                Initialize(GameManager.Instance.LocalPlayerId, true);
            }

            if (!_isInitialized || !_controller.IsLocalPlayer) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.Role != NetworkRole.Client) return;
            if (GameManager.Instance.StateMachine == null) return;
            if (GameManager.Instance.StateMachine.CurrentState != GameState.Playing) return;
            if (GameManager.Instance.ClientHandler == null || !GameManager.Instance.ClientHandler.IsConnected) return;

            int gmLocalId = GameManager.Instance.LocalPlayerId;
            if (gmLocalId > 0 && AssignedPlayerId != gmLocalId)
                AssignedPlayerId = gmLocalId;

            SyncLocalState();
        }

        private void SyncLocalState()
        {
            if (Time.unscaledTime - _lastSendTime < Constants.NetworkSendRate) return;
            _lastSendTime = Time.unscaledTime;

            int playerId = AssignedPlayerId > 0 ? AssignedPlayerId : GameManager.Instance.LocalPlayerId;
            if (playerId <= 0) return;

            var posMsg = new PlayerStateMessage
            {
                SenderId = playerId,
                PlayerId = playerId,
                Timestamp = Time.unscaledTime,
                PosX = transform.position.x,
                PosY = transform.position.y,
                PosZ = transform.position.z,
                RotY = transform.rotation.eulerAngles.y
            };

            GameManager.Instance.ClientHandler.Send(posMsg);
        }

        /// <summary>
        /// Llamado cuando recibimos posición desde el servidor (WorldState).
        /// </summary>
        public void ApplyServerState(Vector3 position, float rotationY)
        {
            if (_controller != null && _controller.IsLocalPlayer) return;

            if (_interpolation != null)
                _interpolation.AddSnapshot(position, rotationY, Time.time);
            else
            {
                transform.position = position;
                transform.rotation = Quaternion.Euler(0, rotationY, 0);
            }
        }
    }
}
