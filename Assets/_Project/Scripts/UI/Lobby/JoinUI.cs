using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLostHill.Core;

namespace TheLostHill.UI.Lobby
{
    public class JoinUI : MonoBehaviour
    {
        [Header("Inputs")]
        public TMP_InputField NameInput;
        public TMP_InputField IpInput;
        public TMP_InputField PortInput;

        [Header("Buttons")]
        public Button JoinButton;

        private void Start()
        {
            IpInput.text = "127.0.0.1";
            PortInput.text = Constants.DefaultTcpPort.ToString();
            
            JoinButton.onClick.AddListener(OnJoin);
        }

        private void OnJoin()
        {
            string pName = string.IsNullOrEmpty(NameInput.text) ? "ClientPlayer" : NameInput.text;
            string ip = IpInput.text;
            int port = int.TryParse(PortInput.text, out var p) ? p : Constants.DefaultTcpPort;

            GameManager.Instance.StartClient(ip, port, pName);
            gameObject.SetActive(false);
        }
    }
}
