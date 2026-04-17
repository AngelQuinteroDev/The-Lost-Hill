using UnityEngine;
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

        public void Setup(string playerName, string status)
        {
            if (PlayerNameText != null) PlayerNameText.text = playerName;
            if (StatusText != null) StatusText.text = status;
        }

        public void UpdateStatus(string status)
        {
            if (StatusText != null) StatusText.text = status;
        }
    }
}
