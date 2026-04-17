using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.UI.HUD
{
    /// <summary>
    /// Maneja los elementos del HUD en juego (progreso, salud si la hay, ping).
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public GameObject HudContainer;

        private void Start()
        {
            // Ocultar base
            if (HudContainer != null) HudContainer.SetActive(false);
            
            if (GameManager.Instance != null && GameManager.Instance.StateMachine != null)
            {
                GameManager.Instance.StateMachine.OnStateChanged += HandleStateChanged;
                // Si la escena inició ya en playing
                if (GameManager.Instance.StateMachine.CurrentState == GameState.Playing)
                {
                    HandleStateChanged(GameState.MainMenu, GameState.Playing);
                }
            }
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
            if (HudContainer != null)
            {
                HudContainer.SetActive(newState == GameState.Playing);
            }
        }
    }
}
