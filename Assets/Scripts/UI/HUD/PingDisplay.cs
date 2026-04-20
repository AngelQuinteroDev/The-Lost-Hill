using UnityEngine;
using TMPro;
using TheLostHill.Network.Client;
using TheLostHill.Core;

namespace TheLostHill.UI.HUD
{
    /// <summary>
    /// Muestra el RTT de ping de forma visual.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PingDisplay : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private PingMonitor _monitor;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            // Solo clientes conectados (y el Host como su propio cliente indirecto, pero Host siempre es 0ms internamente)
            if (GameManager.Instance.ClientHandler != null)
            {
                _monitor = GameManager.Instance.ClientHandler.GetComponent<PingMonitor>();
            }
            else
            {
                // Host
                _text.text = "Host (0ms)";
            }
        }

        private void Update()
        {
            if (_monitor != null)
            {
                int ping = _monitor.CurrentPingMs;
                _text.text = $"Ping: {ping}ms";
                
                if (ping < 80) _text.color = Color.green;
                else if (ping < 150) _text.color = Color.yellow;
                else _text.color = Color.red;
            }
        }
    }
}
