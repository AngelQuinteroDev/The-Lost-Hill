using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Sync;
using TheLostHill.Network.Shared;

namespace TheLostHill.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class PlayerNetworkSync : MonoBehaviour
    {
        private PlayerControllerM _controllerM;
        private Animator _animator;
        private Transform _stateTransform;

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
            _controllerM = GetComponent<PlayerControllerM>();
            if (_controllerM == null) _controllerM = GetComponentInChildren<PlayerControllerM>(true);

            _animator = GetComponentInChildren<Animator>(true);
            _interpolation = GetComponent<InterpolationSystem>();
            _prediction = GetComponent<ClientSidePrediction>();

            ResolveStateTransform();
        }

        public void Initialize(int playerId, bool isLocalPlayer)
        {
            AssignedPlayerId = playerId;
            _lastSendTime = float.NegativeInfinity;
            _isInitialized = true;

            if (_controllerM != null) _controllerM.Initialize(isLocalPlayer);

            ResolveStateTransform();
        }

        private void ResolveStateTransform()
        {
            if (_controllerM != null) _stateTransform = _controllerM.transform;
            else _stateTransform = transform;
        }

        private bool IsLocalOwned()
        {
            if (_controllerM != null && _controllerM.IsLocalPlayer) return true;

            if (GameManager.Instance != null && AssignedPlayerId > 0)
                return AssignedPlayerId == GameManager.Instance.LocalPlayerId;

            return false;
        }

        private void LateUpdate()
        {
            if (!_isInitialized && IsLocalOwned() && GameManager.Instance != null && GameManager.Instance.LocalPlayerId > 0)
                Initialize(GameManager.Instance.LocalPlayerId, true);

            if (!_isInitialized || !IsLocalOwned()) return;
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

            if (_stateTransform == null) ResolveStateTransform();

            Vector3 pos = _stateTransform != null ? _stateTransform.position : transform.position;
            float rotY = _stateTransform != null ? _stateTransform.eulerAngles.y : transform.eulerAngles.y;

            ReadLocalAnimationState(out bool isMoving, out bool isRunning, out bool isPickingUp, out bool isAlive);

            var posMsg = new PlayerStateMessage
            {
                SenderId = playerId,
                PlayerId = playerId,
                Timestamp = Time.unscaledTime,
                PosX = pos.x,
                PosY = pos.y,
                PosZ = pos.z,
                RotY = rotY,
                IsMoving = isMoving,
                IsRunning = isRunning,
                IsPickingUp = isPickingUp,
                IsAlive = isAlive
            };

            GameManager.Instance.ClientHandler.Send(posMsg);
        }

        private void ReadLocalAnimationState(out bool isMoving, out bool isRunning, out bool isPickingUp, out bool isAlive)
        {
            isMoving = false;
            isRunning = false;
            isPickingUp = false;
            isAlive = true;

            if (_controllerM != null)
            {
                isMoving = _controllerM.NetIsMoving;
                isRunning = _controllerM.NetIsRunning;
                isPickingUp = _controllerM.NetIsPickingUp;
                return;
            }

            if (_animator != null)
            {
                isRunning = TryGetAnimatorBool("isRunning");
                bool isWalking = TryGetAnimatorBool("isWalking");
                isPickingUp = TryGetAnimatorBool("isPickingUp");
                isMoving = isRunning || isWalking;
            }
        }

        private bool TryGetAnimatorBool(string param)
        {
            if (_animator == null) return false;
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                    return _animator.GetBool(param);
            return false;
        }

        /// <summary>
        /// Llamado cuando recibimos posición desde el servidor (WorldState).
        /// </summary>
        public void ApplyServerState(Vector3 position, float rotationY)
        {
            if (IsLocalOwned()) return;

            if (_interpolation != null)
                _interpolation.AddSnapshot(position, rotationY, Time.time);
            else
            {
                transform.position = position;
                transform.rotation = Quaternion.Euler(0, rotationY, 0);
            }
        }

        /// <summary>Compatibilidad con NetworkSpawner al spawnear jugadores.</summary>
        public void ApplyColor(int colorIndex)
        {
            var visuals = GetComponent<PlayerVisuals>();
            if (visuals != null)
            {
                visuals.SetColorIndex(colorIndex);
                return;
            }

            if (PlayerColors == null || PlayerColors.Length == 0) return;
            if (colorIndex < 0 || colorIndex >= PlayerColors.Length) colorIndex = 0;

            Color targetColor = PlayerColors[colorIndex];
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r != null && r.material != null)
                    r.material.color = targetColor;
            }
        }
    }
}
