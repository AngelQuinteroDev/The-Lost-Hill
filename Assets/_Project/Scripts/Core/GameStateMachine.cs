using System;
using UnityEngine;
using TheLostHill.Network.Shared;
using TheLostHill.Network.Host;
using TheLostHill.Network.Client;

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

        private ClientNetworkHandler _clientSubscribed;

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.ClientHandler != null && !_isSubscribedToClient)
                SubscribeClientMessages(gm.ClientHandler);
        }

        private bool _isSubscribedToClient = false;

        /// <summary>Registra el handler antes de conectar para no perder GameStateChange temprano.</summary>
        public void SubscribeClientMessages(ClientNetworkHandler handler)
        {
            if (handler == null || _isSubscribedToClient) return;
            handler.OnMessageReceived += HandleClientMessage;
            _clientSubscribed = handler;
            _isSubscribedToClient = true;
        }

        private void OnDestroy()
        {
            if (_clientSubscribed != null && _isSubscribedToClient)
                _clientSubscribed.OnMessageReceived -= HandleClientMessage;
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameState] {oldState} -> {CurrentState}");

            var gm = GameManager.Instance;
            if (gm != null && gm.Role == NetworkRole.Host && gm.HostManager != null)
            {
                var msg = new GameStateChangeMessage
                {
                    SenderId = 0,
                    NewState = (byte)CurrentState
                };
                gm.HostManager.Broadcast(msg);
            }

            HandleSceneTransition(newState);

            OnStateChanged?.Invoke(oldState, CurrentState);
        }

        private void HandleSceneTransition(GameState state)
        {
            string sceneToLoad = "";
            switch (state)
            {
                case GameState.MainMenu:
                    sceneToLoad = "MainScene";
                    break;
                case GameState.Lobby:
                    // El lobby suele ser un panel en la escena de menú o una escena propia
                    // Si es una escena propia: sceneToLoad = "LobbyScene";
                    break;
                case GameState.Playing:
                    sceneToLoad = "GameplayScene";
                    break;
            }

            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.LoadScene(sceneToLoad);
                }
                else
                {
                    Debug.LogWarning($"[GameStateMachine] SceneLoader no encontrado (quizás empezaste directo desde la escena). Usando SceneManager directo para: {sceneToLoad}");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
                }
            }
        }

        private void HandleClientMessage(NetworkMessage msg)
        {
            if (msg is GameStateChangeMessage stateChange)
            {
                GameState newState = (GameState)stateChange.NewState;
                if (CurrentState != newState)
                {
                    if (newState == GameState.MainMenu)
                    {
                        Debug.Log("[GameStateMachine] Host cambió a MainMenu. Saliendo de la sesión limpiamente...");
                        GameManager.Instance.LeaveSession();
                    }
                    else
                    {
                        ChangeState(newState); // Usar ChangeState para que también cargue la escena localmente
                    }
                }
            }
        }
    }
}
