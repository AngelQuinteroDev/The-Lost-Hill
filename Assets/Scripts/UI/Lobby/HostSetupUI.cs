using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLostHill.Core;

namespace TheLostHill.UI.Lobby
{
    public class HostSetupUI : MonoBehaviour
    {
        [Header("Inputs")]
        public TMP_InputField NameInput;
        public TMP_InputField PortInput;
        
        [Header("Buttons")]
        public Button CreateHostButton;

        private void Start()
        {
            PortInput.text = Constants.DefaultTcpPort.ToString();
            CreateHostButton.onClick.AddListener(OnCreateHost);
        }

        private void OnCreateHost()
        {
            string pName = string.IsNullOrEmpty(NameInput.text) ? "HostPlayer" : NameInput.text;
            int port = int.TryParse(PortInput.text, out var p) ? p : Constants.DefaultTcpPort;

            GameManager.Instance.StartHost(pName, port);
            gameObject.SetActive(false); // Ocultar después de iniciar
        }
    }
}
