using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLostHill.UI.Admin
{
    public class GameRulesUI : MonoBehaviour
    {
        [Header("Inputs")]
        public TMP_InputField MaxPlayers;
        public TMP_InputField MonsterSpeed;
        public TMP_InputField TotalItems;

        [Header("Buttons")]
        public Button ApplyBtn;

        private void Start()
        {
            ApplyBtn.onClick.AddListener(ApplyRules);
        }

        private void ApplyRules()
        {
            // TODO: Crear objeto GameRules, enviar via UpdateRules()
            // Y GameManager.Instance.HostManager.BroadcastTCP
        }
    }
}
