using UnityEngine;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Client
{
    /// <summary>
    /// Ping Monitor envía mensajes de PingRequest al servidor cada 'PingInterval'
    /// y mide el tiempo que toma en recibir la respuesta para calcular el RTT (Round Trip Time).
    /// </summary>
    public class PingMonitor : MonoBehaviour
    {
        private ClientNetworkHandler _client;
        private float _lastPingTime;

        private float[] _pingHistory;
        private int _pingIndex;

        /// <summary>El ping actual en milisegundos suavizado.</summary>
        public int CurrentPingMs { get; private set; }

        private void Awake()
        {
            _client = GetComponent<ClientNetworkHandler>();
            _pingHistory = new float[Core.Constants.PingSmoothingWindow];
        }

        private void OnEnable()
        {
            _client.OnMessageReceived += HandleMessage;
        }

        private void OnDisable()
        {
            if (_client != null)
                _client.OnMessageReceived -= HandleMessage;
        }

        private void Update()
        {
            if (!_client.IsConnected) return;

            if (Time.time - _lastPingTime >= Core.Constants.PingInterval)
            {
                _lastPingTime = Time.time;
                var pingMsg = new PingRequestMessage { ClientTime = Time.time };
                
                // Usamos TCP para un RTT más preciso en el canal crítico,
                // aunque UDP es válido si queremos medir latencia de gameplay puro.
                // En Unity multijugador comúnmente se usan ambos, aquí TCP es más controlable.
                _client.SendTCP(pingMsg); 
            }
        }

        private void HandleMessage(NetworkMessage msg)
        {
            if (msg is PingResponseMessage pingResp)
            {
                float rtt = Time.time - pingResp.ClientTime;
                
                _pingHistory[_pingIndex] = rtt;
                _pingIndex = (_pingIndex + 1) % _pingHistory.Length;

                float sum = 0;
                int count = 0;
                foreach (var p in _pingHistory)
                {
                    if (p > 0)
                    {
                        sum += p;
                        count++;
                    }
                }

                if (count > 0)
                {
                    CurrentPingMs = Mathf.RoundToInt((sum / count) * 1000f);
                }
            }
        }
    }
}
