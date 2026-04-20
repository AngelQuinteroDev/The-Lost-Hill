using System.Collections;
using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Client
{

    public class ReconnectHandler : MonoBehaviour
    {
        private int _attempts = 0;
        private bool _isReconnecting = false;
        
        public bool IsReconnecting => _isReconnecting;

        public void AttemptReconnect(ClientNetworkHandler handler)
        {
            if (_isReconnecting) return;
            if (_attempts >= Constants.MaxReconnectAttempts) return; 
            
            StartCoroutine(ReconnectRoutine(handler));
        }

        private IEnumerator ReconnectRoutine(ClientNetworkHandler handler)
        {
            _isReconnecting = true;

            float delay = Mathf.Min(
                Constants.ReconnectBaseDelay * Mathf.Pow(2, _attempts),
                Constants.ReconnectMaxDelay
            );
            delay += Random.Range(0f, delay * 0.2f); 

            Debug.Log($"[Client] Intentando reconectar en {delay:F1}s... (Intento {_attempts + 1}/{Constants.MaxReconnectAttempts})");

            yield return new WaitForSeconds(delay);

            _attempts++;
            
            
            handler.Connect(handler.ServerIP, handler.ServerPort, handler.PlayerName);

          
            yield return new WaitForSeconds(Constants.ConnectionTimeout);

            if (handler.IsConnected)
            {
                Debug.Log("[Client] Reconexión exitosa.");
                _attempts = 0;
            }
            
            _isReconnecting = false;
        }

        public void StopReconnect()
        {
            StopAllCoroutines();
            ResetAttempts();
        }

        private void ResetAttempts()
        {
            _attempts = 0;
            _isReconnecting = false;
        }
    }
}
