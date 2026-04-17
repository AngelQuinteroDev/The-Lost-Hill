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
            DontDestroyOnLoad(gameObject);

            StateMachine = gameObject.AddComponent<GameStateMachine>();
        }

        public void StartHost(string playerName, int port)
        {
            if (Role != NetworkRole.None) return;

            Role = NetworkRole.Host;
            LocalPlayerName = playerName;

            // Inicia servidor
            if (HostManager == null) HostManager = gameObject.AddComponent<HostNetworkManager>();
            HostManager.StartHost(port, port + 1);

            // Cambiar estado a Lobby
            StateMachine.ChangeState(GameState.Lobby);
        }

        public void StartClient(string ip, int port, string playerName)
        {
            if (Role != NetworkRole.None) return;

            Role = NetworkRole.Client;
            LocalPlayerName = playerName;

            if (ClientHandler == null) ClientHandler = gameObject.AddComponent<ClientNetworkHandler>();
            ClientHandler.Connect(ip, port, playerName);
            
            // Client Handler gestiona sus propios eventos, pero el StateMachine se actualizará
            // cuando el Host envíe el mensaje de GameStateChange
        }

        public void LeaveSession()
        {
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
            
            // TODO: Podría usar SceneLoader para cargar MainMenu
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
