using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using TheLostHill.Network.Host;

namespace TheLostHill.UI.Lobby
{
    /// <summary>
    /// UI del Lobby que muestra la lista de jugadores conectados.
    /// Funciona tanto para el Host como para los Clientes.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Containers")]
        public Transform PlayersContainer;
        public GameObject PlayerListItemPrefab;
        
        [Header("Buttons")]
        public Button StartGameButton; // Solo para el host
        public Button LeaveButton;

        private Dictionary<int, LobbyPlayerEntry> _playerEntries = new Dictionary<int, LobbyPlayerEntry>();

        private void Start()
        {
            StartGameButton.onClick.AddListener(OnStartGameClicked);
            LeaveButton.onClick.AddListener(OnLeaveClicked);

            // Refrescar inicialmente
            RefreshList();
        }

        private void OnEnable()
        {
            // Suscribirse a eventos de red para actualizar la lista
            if (GameManager.Instance.Role == NetworkRole.Host)
            {
                GameManager.Instance.HostManager.OnClientConnected += (id, name) => RefreshList();
                GameManager.Instance.HostManager.OnClientDisconnected += (id) => RefreshList();
            }
            else if (GameManager.Instance.Role == NetworkRole.Client)
            {
                GameManager.Instance.ClientHandler.OnMessageReceived += HandleClientNetworkMessage;
                GameManager.Instance.ClientHandler.OnDisconnected += OnLeaveClicked;
            }
            
            RefreshList();
        }

        private void OnDisable()
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.Role == NetworkRole.Host && GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.OnClientConnected -= OnHostClientConnected;
                GameManager.Instance.HostManager.OnClientDisconnected -= OnHostClientDisconnected;
            }
            else if (GameManager.Instance.Role == NetworkRole.Client && GameManager.Instance.ClientHandler != null)
            {
                GameManager.Instance.ClientHandler.OnMessageReceived -= HandleClientNetworkMessage;
                GameManager.Instance.ClientHandler.OnDisconnected -= OnLeaveClicked;
            }
        }

        private void OnHostClientConnected(int id, string name) { if (this != null) RefreshList(); }
        private void OnHostClientDisconnected(int id) { if (this != null) RefreshList(); }


        private void HandleClientNetworkMessage(NetworkMessage msg)
        {
            // Si alguien se une o se va, refrescamos la lista
            if (msg is PlayerJoinedMessage || msg is PlayerLeftMessage || msg is WorldSnapshotMessage)
            {
                RefreshList();
            }
        }

        public void RefreshList()
        {
            if (this == null || PlayersContainer == null) return;

            // Limpiar lista actual
            foreach (Transform child in PlayersContainer)
            {
                if (child != null) Destroy(child.gameObject);
            }
            _playerEntries.Clear();

            // Solo el host tiene el botón Start activado
            StartGameButton.gameObject.SetActive(GameManager.Instance.Role == NetworkRole.Host);

            // 1. Agregar al Host (ID 0)
            AddPlayerEntry(0, GameManager.Instance.Role == NetworkRole.Host ? GameManager.Instance.LocalPlayerName : "Host", "Host");

            // 2. Agregar a los clientes conectados
            if (GameManager.Instance.Role == NetworkRole.Host)
            {
                foreach (var session in GameManager.Instance.HostManager.Registry.GetAll())
                {
                    AddPlayerEntry(session.PlayerId, session.PlayerName, $"Ping: {(int)session.Ping}ms");
                }
            }
            else if (GameManager.Instance.Role == NetworkRole.Client)
            {
                // En el MVP, el cliente obtiene la lista de jugadores a través de mensajes de red.
                // Por ahora, solo mostramos al host y a nosotros mismos si tenemos el ID.
                if (GameManager.Instance.ClientHandler.IsConnected)
                {
                    AddPlayerEntry(GameManager.Instance.LocalPlayerId, GameManager.Instance.LocalPlayerName, "Local");
                }
            }
        }

        private void AddPlayerEntry(int id, string playerName, string status)
        {
            GameObject go = Instantiate(PlayerListItemPrefab, PlayersContainer);
            LobbyPlayerEntry entry = go.GetComponent<LobbyPlayerEntry>();
            if (entry != null)
            {
                entry.Setup(playerName, status);
                _playerEntries[id] = entry;
            }
        }

        private void OnStartGameClicked()
        {
            if (GameManager.Instance.Role == NetworkRole.Host)
            {
                Debug.Log("[Lobby] Host iniciando partida...");
                GameManager.Instance.StateMachine.ChangeState(GameState.Playing);
            }
        }

        private void OnLeaveClicked()
        {
            Debug.Log("[Lobby] Abandonando sesión...");
            GameManager.Instance.LeaveSession();
        }
    }
}
