using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;

namespace TheLostHill.Admin
{
    public class PauseSystem : MonoBehaviour
    {
        public bool IsPaused { get; private set; } = false;

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        private void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0; // Efecto Local
            
            GameManager.Instance.HostManager?.Broadcast(new PauseGameMessage());
            Debug.Log("[Admin] Juego Pausado.");
        }

        private void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1; // Efecto Local
            
            GameManager.Instance.HostManager?.Broadcast(new ResumeGameMessage());
            Debug.Log("[Admin] Juego Reanudado.");
        }
    }
}
