using System;
using UnityEngine;
using TheLostHill.Network.Shared;
using TheLostHill.Network.Host;

namespace TheLostHill.Core
{
    public enum GameState : byte
    {
        MainMenu = 0,
        Lobby = 1,
        Playing = 2,
        GameOver = 3,
        Results = 4
    }

    /// <summary>
    /// Maneja los estados del juego (Lobby -> Playing -> GameOver -> Results).
    /// Si es Host, autoritativamente cambia estados y lo propaga a clientes.
    /// Si es Cliente, escucha eventos del servidor para cambiar.
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        public event Action<GameState, GameState> OnStateChanged;

        private GameManager _gm;

        private void Awake()
        {
            _gm = GameManager.Instance;
        }

        private void Update()
        {
            // Intentar suscribirse si aún no lo estamos y el ClientHandler ya existe
            if (_gm != null && _gm.ClientHandler != null && !_isSubscribedToClient)
            {
                _gm.ClientHandler.OnMessageReceived += HandleClientMessage;
                _isSubscribedToClient = true;
            }
        }

        private bool _isSubscribedToClient = false;

        private void OnDestroy()
        {
            if (_gm != null && _gm.ClientHandler != null && _isSubscribedToClient)
            {
                _gm.ClientHandler.OnMessageReceived -= HandleClientMessage;
            }
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameState] {oldState} -> {CurrentState}");

            // Lógica de carga de escenas automática
            HandleSceneTransition(newState);

            OnStateChanged?.Invoke(oldState, CurrentState);

            // Si somos Host, lo notificamos a todos
            if (_gm != null && _gm.Role == NetworkRole.Host && _gm.HostManager != null)
            {
                var msg = new GameStateChangeMessage
                {
                    SenderId = 0,
                    NewState = (byte)CurrentState
                };
                _gm.HostManager.BroadcastTCP(msg);
            }
        }

        private void HandleSceneTransition(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    SceneLoader.Instance.LoadScene("MainScene");
                    break;
                case GameState.Lobby:
                    // El lobby suele ser un panel en la escena de menú o una escena propia
                    // Si es una escena propia: SceneLoader.Instance.LoadScene("LobbyScene");
                    break;
                case GameState.Playing:
                    SceneLoader.Instance.LoadScene("GameplayScene");
                    break;
            }
        }

        private void HandleClientMessage(NetworkMessage msg)
        {
            if (msg is GameStateChangeMessage stateChange)
            {
                GameState newState = (GameState)stateChange.NewState;
                if (CurrentState != newState)
                {
                    ChangeState(newState); // Usar ChangeState para que también cargue la escena localmente
                }
            }
        }
    }
}
