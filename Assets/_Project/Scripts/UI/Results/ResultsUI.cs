using UnityEngine;
using UnityEngine.UI;

namespace TheLostHill.UI.Results
{
    public class ResultsUI : MonoBehaviour
    {
        public Transform ScoreboardContainer;
        public Button RestartGameBtn; // Host only
        public Button BackToLobbyBtn;
        
        private void Start()
        {
            RestartGameBtn.onClick.AddListener(OnRestart);
            BackToLobbyBtn.onClick.AddListener(OnBackToLobby);
            
            // Si no soy host, apago el botón "Reiniciar"
            // if (GameManager.Instance.Role != NetworkRole.Host) RestartGameBtn.gameObject.SetActive(false);
        }

        private void OnRestart()
        {
            // GameManager.Instance.StateMachine.ChangeState(GameState.Playing);
        }

        private void OnBackToLobby()
        {
            // Si el host lo presiona, lleva a todos de vuelta al lobby
            // GameManager.Instance.StateMachine.ChangeState(GameState.Lobby);
        }
    }
}
