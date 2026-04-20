using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Client
{

    public class PingMonitor : MonoBehaviour
    {
        private ClientNetworkHandler _client;
        private float _lastPingTime;

        private float[] _pingHistory;
        private int _pingIndex;


        public int CurrentPingMs { get; private set; }

        private void Awake()
        {
            ResolveClient();
            _pingHistory = new float[Core.Constants.PingSmoothingWindow];
        }

       
        private void ResolveClient()
        {
            if (_client != null) return;
            _client = GetComponent<ClientNetworkHandler>();
            if (_client == null)
                _client = GetComponentInParent<ClientNetworkHandler>();
            if (_client == null && GameManager.Instance != null)
                _client = GameManager.Instance.ClientHandler;
        }

        private void OnEnable()
        {
            ResolveClient();
            if (_client == null) return;
            _client.OnMessageReceived += HandleMessage;
        }

        private void OnDisable()
        {
            if (_client != null)
                _client.OnMessageReceived -= HandleMessage;
        }

        private void Update()
        {
            if (_client == null || !_client.IsConnected) return;

            if (Time.unscaledTime - _lastPingTime >= Core.Constants.PingInterval)
            {
                _lastPingTime = Time.unscaledTime;
                var pingMsg = new PingRequestMessage { ClientTime = Time.unscaledTime };
                
        
                _client.Send(pingMsg);
            }
        }

        private void HandleMessage(NetworkMessage msg)
        {
            if (msg is PingResponseMessage pingResp)
            {
                float rtt = Time.unscaledTime - pingResp.ClientTime;
                
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
