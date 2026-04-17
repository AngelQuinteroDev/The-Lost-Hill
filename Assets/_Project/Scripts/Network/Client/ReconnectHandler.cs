using System.Collections;
using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Client
{
    /// <summary>
    /// Maneja los reintentos automáticos de conexión cuando un cliente 
    /// pierde la conexión de manera abrupta con el host.
    /// Utiliza un backoff exponencial.
    /// </summary>
    public class ReconnectHandler : MonoBehaviour
    {
        private int _attempts = 0;
        private bool _isReconnecting = false;
        
        public bool IsReconnecting => _isReconnecting;

        public void AttemptReconnect(ClientNetworkHandler handler)
        {
            if (_isReconnecting) return;
            if (_attempts >= Constants.MaxReconnectAttempts) return; // Superó el máximo
            
            StartCoroutine(ReconnectRoutine(handler));
        }

        private IEnumerator ReconnectRoutine(ClientNetworkHandler handler)
        {
            _isReconnecting = true;

            // Backoff exponencial con Random Jitter
            float delay = Mathf.Min(
                Constants.ReconnectBaseDelay * Mathf.Pow(2, _attempts),
                Constants.ReconnectMaxDelay
            );
            delay += Random.Range(0f, delay * 0.2f); // Jitter de hasta 20%

            Debug.Log($"[Client] Intentando reconectar en {delay:F1}s... (Intento {_attempts + 1}/{Constants.MaxReconnectAttempts})");

            yield return new WaitForSeconds(delay);

            _attempts++;
            
            // Reintentar conexión usando la última IP y puerto
            handler.Connect(handler.ServerIP, handler.TcpPort, handler.PlayerName);

            // Esperar un rato para ver si tuvo éxito o no (manejado de forma encolada en handler)
            yield return new WaitForSeconds(Constants.ConnectionTimeout);

            if (handler.IsConnected)
            {
                Debug.Log("[Client] Reconexión exitosa.");
                _attempts = 0;
            }
            
            _isReconnecting = false;
        }

        public void ResetAttempts()
        {
            _attempts = 0;
            _isReconnecting = false;
        }
    }
}
