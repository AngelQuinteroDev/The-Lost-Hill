using UnityEngine;

namespace TheLostHill.UI.Results
{
    public class GameOverUI : MonoBehaviour
    {
        public GameObject VictoryPanel;
        public GameObject DefeatPanel;

        public void ShowVictory()
        {
            VictoryPanel.SetActive(true);
            DefeatPanel.SetActive(false);
        }

        public void ShowDefeat()
        {
            VictoryPanel.SetActive(false);
            DefeatPanel.SetActive(true);
        }
    }
}
