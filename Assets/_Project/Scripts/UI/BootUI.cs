using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.UI
{
    /// <summary>
    /// Gestiona la visibilidad de los paneles principales (MVP).
    /// </summary>
    public class BootUI : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject MainMenuPanel;
        public GameObject HostSetupPanel;
        public GameObject JoinPanel;
        public GameObject LobbyPanel;

        private void Start()
        {
            ShowMenu(MainMenuPanel);

            // Suscribirse a cambios de estado globales
            GameManager.Instance.StateMachine.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.StateMachine != null)
            {
                GameManager.Instance.StateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                    ShowMenu(MainMenuPanel);
                    break;
                case GameState.Lobby:
                    ShowMenu(LobbyPanel);
                    break;
                case GameState.Playing:
                    // Esconder UI de menús al empezar a jugar
                    HideAll();
                    break;
            }
        }

        public void ShowHostSetup() => ShowMenu(HostSetupPanel);
        public void ShowJoin() => ShowMenu(JoinPanel);
        public void ShowMainMenu() => ShowMenu(MainMenuPanel);

        private void ShowMenu(GameObject target)
        {
            HideAll();
            if (target != null) target.SetActive(true);
        }

        private void HideAll()
        {
            if (MainMenuPanel != null) MainMenuPanel.SetActive(false);
            if (HostSetupPanel != null) HostSetupPanel.SetActive(false);
            if (JoinPanel != null) JoinPanel.SetActive(false);
            if (LobbyPanel != null) LobbyPanel.SetActive(false);
        }
    }
}
