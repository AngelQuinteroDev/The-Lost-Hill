using UnityEngine;
using UnityEngine.UI;
using TheLostHill.Core;
using TheLostHill.Admin;

namespace TheLostHill.UI.Admin
{
    public class AdminPanelUI : MonoBehaviour
    {
        public GameObject Panel;
        public Button PauseToggleBtn;

        private AdminController _adminCtrl;

        private void Start()
        {
            Panel.SetActive(false);
            
            if (GameManager.Instance.Role == NetworkRole.Host)
            {
                _adminCtrl = FindFirstObjectByType<AdminController>();
                PauseToggleBtn.onClick.AddListener(OnPauseToggle);
            }
        }

        private void Update()
        {
            if (GameManager.Instance.Role == NetworkRole.Host && Input.GetKeyDown(KeyCode.F12))
            {
                Panel.SetActive(!Panel.activeSelf);
            }
        }

        private void OnPauseToggle()
        {
            if (_adminCtrl != null && _adminCtrl.Pause != null)
            {
                _adminCtrl.Pause.TogglePause();
            }
        }
    }
}
