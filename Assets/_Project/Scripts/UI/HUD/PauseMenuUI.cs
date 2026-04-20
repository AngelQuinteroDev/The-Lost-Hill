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
        public Button ResumeButton; // Cierra el menú (para todos). Y si es host, también despausa el juego
        public Button PauseGameButton; // Solo visible por el host para pausar el juego
        public Button QuitButton;

        private bool _isMenuOpen = false;
        private static bool _isGameGloballyPaused = false;
        
        public static bool IsPaused => _isGameGloballyPaused; // Propiedad para que otros scripts sepan si el juego está en pausa.
        
        public static PauseMenuUI Instance { get; private set; } // Referencia Singleton

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            PausePanel.SetActive(false);

            ResumeButton.onClick.AddListener(OnResumeClicked);
            QuitButton.onClick.AddListener(OnQuitClicked);
            PauseGameButton.onClick.AddListener(OnPauseClicked);
        }

        private void Update()
        {
            // Abir / cerrar menú al presionar la tecla Esc usando el nuevo Input System
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
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
            if (PausePanel != null) PausePanel.SetActive(true);

            // Ajustar UI dependiendo del rol
            bool isHost = GameManager.Instance.Role == NetworkRole.Host;
            
            // Host ve todos los botones. Clientes SOLO el de salir.
            if (PauseGameButton != null) PauseGameButton.gameObject.SetActive(isHost);
            if (ResumeButton != null) ResumeButton.gameObject.SetActive(isHost); // Según feedback: Clientes no ven Resume.
            if (QuitButton != null) QuitButton.gameObject.SetActive(true);

            // Desbloquear cursor para interactuar con la UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseMenu()
        {
            _isMenuOpen = false;
            if (PausePanel != null) PausePanel.SetActive(false);

            // Volver a bloquear cursor al cerrar la UI (si no estamos globalmente pausados)
            if (!_isGameGloballyPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public bool IsMenuOpen => _isMenuOpen;

        private void OnPauseClicked()
        {
            if (GameManager.Instance.Role != NetworkRole.Host) return;

            // Pausa global
            _isGameGloballyPaused = true;
            Time.timeScale = 0f;
            
            if (GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.Broadcast(new PauseGameMessage());
            }

            Debug.Log("[PauseMenu] Juego Pausado por el Host.");
        }
        
        private void OnResumeClicked()
        {
            // Si eres el host, también despausas el juego
            if (GameManager.Instance.Role == NetworkRole.Host && _isGameGloballyPaused)
            {
                _isGameGloballyPaused = false;
                Time.timeScale = 1f;

                if (GameManager.Instance.HostManager != null)
                {
                    GameManager.Instance.HostManager.Broadcast(new ResumeGameMessage());
                }

                Debug.Log("[PauseMenu] Juego Reanudado por el Host.");
            }

            // Para todos (Host y Clientes): Cierra el menú visual de pausa local
            CloseMenu();
        }

        private void OnQuitClicked()
        {
            // Abandonar la sesión y volver al menú principal de forma segura
            
            // Asegurarnos de restablecer el tiempo antes de salir
            Time.timeScale = 1f; 
            _isGameGloballyPaused = false;

            GameManager.Instance.LeaveSession();
        }

        // Método estático para que la red (NetworkSpawner) actualice el estado en los clientes
        public static void ApplyRemotePauseState(bool isPaused)
        {
            _isGameGloballyPaused = isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            Debug.Log($"[PauseMenu] Pausa remota actualizada: {isPaused}");
        }
    }
}
