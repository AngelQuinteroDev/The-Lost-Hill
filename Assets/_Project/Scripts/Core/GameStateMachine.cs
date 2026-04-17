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

        private void Start()
        {
            _gm = GameManager.Instance;
            
            if (_gm.ClientHandler != null)
            {
                _gm.ClientHandler.OnMessageReceived += HandleClientMessage;
            }
        }

        private void OnDestroy()
        {
            if (_gm != null && _gm.ClientHandler != null)
            {
                _gm.ClientHandler.OnMessageReceived -= HandleClientMessage;
            }
        }

        /// <summary>
        /// Intenta cambiar el estado. Si es Host, cambia y hace broadcast.
        /// Si es cliente pero el cambio no es sincronizado, simplemente cambia.
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameState] {oldState} -> {CurrentState}");

            OnStateChanged?.Invoke(oldState, CurrentState);

            // Si somos Host, lo notificamos a todos
            if (_gm != null && _gm.Role == NetworkRole.Host && _gm.HostManager != null)
            {
                var msg = new GameStateChangeMessage
                {
                    SenderId = 0, // 0 = Host authority
                    NewState = (byte)CurrentState
                };
                _gm.HostManager.BroadcastTCP(msg);
            }
        }

        private void HandleClientMessage(NetworkMessage msg)
        {
            if (msg is GameStateChangeMessage stateChange)
            {
                GameState newState = (GameState)stateChange.NewState;
                if (CurrentState != newState)
                {
                    GameState oldState = CurrentState;
                    CurrentState = newState;
                    Debug.Log($"[GameState] Recibido desde Host: {oldState} -> {CurrentState}");
                    OnStateChanged?.Invoke(oldState, CurrentState);
                }
            }
        }
    }
}
