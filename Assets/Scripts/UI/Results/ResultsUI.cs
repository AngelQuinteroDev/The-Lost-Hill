using UnityEngine;
using UnityEngine.UI;

namespace TheLostHill.UI.Results
{
    public class ResultsUI : MonoBehaviour
    {
        public Transform ScoreboardContainer;
        public Button RestartGameBtn; 
        public Button BackToLobbyBtn;
        
        private void Start()
        {
            RestartGameBtn.onClick.AddListener(OnRestart);
            BackToLobbyBtn.onClick.AddListener(OnBackToLobby);
            
         
        }

        private void OnRestart()
        {
            ;
        }

        private void OnBackToLobby()
        {
            
        }
    }
}
