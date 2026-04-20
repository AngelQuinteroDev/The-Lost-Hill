using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLostHill.UI.Lobby
{
    /// <summary>
    /// Representa una entrada de jugador en la lista del Lobby (MVP).
    /// </summary>
    public class LobbyPlayerEntry : MonoBehaviour
    {
        public TextMeshProUGUI PlayerNameText;
        public TextMeshProUGUI StatusText; // Ej: "Host", "Ready", "Ping: 20ms"
        public Button KickButton; // Asignar en el inspector
        
        private int _playerId;
        private Action<int> _onKickAction;

        private void Start()
        {
            if (KickButton != null)
            {
                KickButton.onClick.AddListener(() =>
                {
                    _onKickAction?.Invoke(_playerId);
                });
            }
        }

        public void Setup(int playerId, string playerName, string status, bool canKick, Action<int> onKickAction = null)
        {
            _playerId = playerId;
            _onKickAction = onKickAction;

            if (PlayerNameText != null) PlayerNameText.text = playerName;
            if (StatusText != null) StatusText.text = status;
            
            if (KickButton != null)
            {
                // Mostramos el botón solo si tenemos permiso de banear (es el host quien ve a un cliente)
                KickButton.gameObject.SetActive(canKick);
            }
        }

        public void UpdateStatus(string status)
        {
            if (StatusText != null) StatusText.text = status;
        }
    }
}
