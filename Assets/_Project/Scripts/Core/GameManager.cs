using UnityEngine;
using TheLostHill.Network.Host;
using TheLostHill.Network.Client;

namespace TheLostHill.Core
{
    public enum NetworkRole
    {
        None,
        Host,
        Client
    }

    /// <summary>
    /// Singleton central que coordina el estado del juego.
    /// Referencia al NetworkManager correspondiente según el rol elegido y maneja 
    /// transiciones globales a través de GameStateMachine.
    /// Permanece vivo entre escenas (DontDestroyOnLoad).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public NetworkRole Role { get; private set; } = NetworkRole.None;
        
        [Header("Network Managers")]
        public HostNetworkManager HostManager;
        public ClientNetworkHandler ClientHandler;

        [Header("State")]
        public GameStateMachine StateMachine { get; private set; }

        // ── Datos Locales ─────────────────────────────────────────
        public int LocalPlayerId => Role == NetworkRole.Host ? 0 : (ClientHandler != null ? ClientHandler.LocalPlayerId : -1);
        public string LocalPlayerName { get; set; } = "Player";
        public bool IsPausable => Role == NetworkRole.Host;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Mantener Update/Networking activos aunque la ventana no tenga foco (Multiplayer Center).
            Application.runInBackground = true;

            // Requisito de Unity: DontDestroyOnLoad solo funciona en objetos "raíz"
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Asegurarnos de que si asignaste los managers en otros objetos por error o decisión,
            // tampoco se destruyan al cambiar de escena.
            if (HostManager != null && HostManager.gameObject != gameObject)
            {
                HostManager.transform.SetParent(null);
                DontDestroyOnLoad(HostManager.gameObject);
            }
            if (ClientHandler != null && ClientHandler.gameObject != gameObject)
            {
                ClientHandler.transform.SetParent(null);
                DontDestroyOnLoad(ClientHandler.gameObject);
            }

            StateMachine = gameObject.AddComponent<GameStateMachine>();
        }

        public void StartHost(string playerName, int port)
        {
            if (Role != NetworkRole.None) return;

            Role = NetworkRole.Host;
            LocalPlayerName = playerName;

            // Inicia servidor
            if (HostManager == null) HostManager = gameObject.AddComponent<HostNetworkManager>();
            HostManager.StartHost(port);

            // Cambiar estado a Lobby
            StateMachine.ChangeState(GameState.Lobby);
        }

        public void StartClient(string ip, int port, string playerName)
        {
            if (Role != NetworkRole.None) return;

            Role = NetworkRole.Client;
            LocalPlayerName = playerName;

            if (ClientHandler == null) ClientHandler = gameObject.AddComponent<ClientNetworkHandler>();
            // Mismo GameObject que ClientHandler (en escena puede estar en un hijo); si no, GetComponent<ClientNetworkHandler>() en PingMonitor es null y OnEnable falla.
            GameObject clientGo = ClientHandler.gameObject;
            if (clientGo.GetComponent<PingMonitor>() == null)
                clientGo.AddComponent<PingMonitor>();

            StateMachine.SubscribeClientMessages(ClientHandler);

            ClientHandler.OnConnected += () => {
                StateMachine.ChangeState(GameState.Lobby);
            };

            ClientHandler.Connect(ip, port, playerName);
        }

        private bool _isLeaving = false;

        public void LeaveSession()
        {
            if (_isLeaving) return;
            _isLeaving = true;

            if (Role == NetworkRole.Host)
            {
                HostManager?.StopHost();
            }
            else if (Role == NetworkRole.Client)
            {
                ClientHandler?.Disconnect();
            }

            Role = NetworkRole.None;
            StateMachine.ChangeState(GameState.MainMenu);
            
            _isLeaving = false;
        }

        private void OnDestroy()
        {
            Debug.Log($"[GameManager] OnDestroy en GameManager (ID: {GetInstanceID()}, Object: {gameObject.name}). Instance = {(Instance == this ? "This" : "Other")}");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ── Compatibilidad con Gameplay/NetworkSpawner ───────────
        public bool IsHost => Role == NetworkRole.Host;
        public int LocalColorIndex => Role == NetworkRole.Host ? 0 : (ClientHandler != null ? ClientHandler.ColorIndex : 0);

        public void ChangeState(GameState newState)
        {
            StateMachine?.ChangeState(newState);
        }
    }
}
