using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Player
{
    /// <summary>
    /// Se encarga de la sincronización en red específica de un jugador.
    /// Si es local: toma el input de PlayerController/CSP y lo envía con la frecuencia adecuada.
    /// Si es remoto: toma los datos del WorldState y alimenta al InterpolationSystem.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerNetworkSync : MonoBehaviour
    {
        private PlayerController _controller;
        private float _lastSendTime;
        
        public int AssignedPlayerId { get; set; }

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (_controller.IsLocalPlayer && GameManager.Instance.Role != NetworkRole.None)
            {
                SyncLocalState();
            }
        }

        private void SyncLocalState()
        {
            if (Time.time - _lastSendTime >= Constants.NetworkSendRate)
            {
                _lastSendTime = Time.time;
                
                // TODO: En el update real, tomamos el último sequence number de ClientSidePrediction
                // y los inputs acumulados, y enviamos PlayerInputMessage vía GameManager.Instance.ClientHandler...
            }
        }

        /// <summary>
        /// Llamado cuando recibimos posición corregida del servidor.
        /// </summary>
        public void ApplyServerState(Vector3 position, float rotationY, int lastProcessedInput)
        {
            // TODO: si es local, pasarlo a CSP Reconcile()
            // si es remoto, pasarlo a InterpolationSystem
        }
    }
}
