using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using TheLostHill.Admin;

namespace TheLostHill.UI.HUD
{
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Containers")]
        public GameObject PausePanel;

        [Header("UI Texts")]
        public TextMeshProUGUI PauseTitleText;

        [Header("Buttons")]
        public Button ResumeButton;
        public Button PauseGameButton; // Solo visible/usable por el host
        public Button QuitButton;

        private PauseSystem _pauseSystem;
        private bool _isMenuOpen = false;

        private void Start()
        {
            PausePanel.SetActive(false);

            ResumeButton.onClick.AddListener(CloseMenu);
            QuitButton.onClick.AddListener(OnQuitClicked);
            PauseGameButton.onClick.AddListener(OnPauseClicked);

            // Intentar buscar el PauseSystem si existe
            _pauseSystem = FindFirstObjectByType<PauseSystem>();
        }

        private void Update()
        {
            // Abir / cerrar menú al presionar la tecla Esc
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isMenuOpen)
                {
                    CloseMenu();
                }
                else
                {
                    OpenMenu();
                }
            }
        }

        private void OpenMenu()
        {
            _isMenuOpen = true;
            PausePanel.SetActive(true);

            // Ajustar UI dependiendo del rol
            bool isHost = GameManager.Instance.Role == NetworkRole.Host;
            PauseGameButton.gameObject.SetActive(isHost);

            if (isHost && _pauseSystem != null)
            {
                PauseGameButton.GetComponentInChildren<TextMeshProUGUI>().text = _pauseSystem.IsPaused ? "Reanudar Partida" : "Pausar Partida";
            }
        }

        private void CloseMenu()
        {
            _isMenuOpen = false;
            PausePanel.SetActive(false);
        }

        private void OnPauseClicked()
        {
            if (GameManager.Instance.Role != NetworkRole.Host) return;

            if (_pauseSystem != null)
            {
                _pauseSystem.TogglePause();
                
                // Actualizar texto luego de pausar
                PauseGameButton.GetComponentInChildren<TextMeshProUGUI>().text = _pauseSystem.IsPaused ? "Reanudar Partida" : "Pausar Partida";
            }
        }

        private void OnQuitClicked()
        {
            // Abandonar la sesión y volver al menú principal
            GameManager.Instance.LeaveSession();
        }
    }
}
