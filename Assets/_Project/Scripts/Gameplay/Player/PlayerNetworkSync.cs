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

        private void Update()
        {
            if (_controller.IsLocalPlayer)
            {
                // Solo enviamos si estamos en red
                if (GameManager.Instance.Role != NetworkRole.None)
                {
                    SyncLocalState();
                }
            }
        }

        private void SyncLocalState()
        {
            if (Time.time - _lastSendTime >= Constants.NetworkSendRate)
            {
                _lastSendTime = Time.time;
                
                // Enviar nuestra posición actual al Host por UDP
                var posMsg = new PlayerStateMessage
                {
                    SenderId = AssignedPlayerId,
                    PosX = transform.position.x,
                    PosY = transform.position.y,
                    PosZ = transform.position.z,
                    RotY = transform.rotation.eulerAngles.y
                };

                if (GameManager.Instance.Role == NetworkRole.Host)
                {
                    // Si somos el host, simplemente actualizamos el registro localmente (o nos lo enviamos a nosotros mismos)
                    // En este MVP, el HostManager ya tiene acceso a nuestra posición física vía Registry.
                }
                else
                {
                    GameManager.Instance.ClientHandler.SendTCP(posMsg);
                }
            }
        }

        /// <summary>
        /// Llamado cuando recibimos posición desde el servidor (WorldState).
        /// </summary>
        public void ApplyServerState(Vector3 position, float rotationY)
        {
            if (_controller.IsLocalPlayer)
            {
                // Opcional: Reconciliación si la discrepancia es mucha
                // transform.position = position; 
            }
            else
            {
                // Entidad remota: Alimerntar el sistema de interpolación
                if (_interpolation != null)
                {
                    _interpolation.AddSnapshot(position, rotationY, Time.time);
                }
            }
        }
    }
}
