using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Admin
{

    [RequireComponent(typeof(KickSystem))]
    [RequireComponent(typeof(BanSystem))]
    [RequireComponent(typeof(PauseSystem))]
    public class AdminController : MonoBehaviour
    {
        public KickSystem Kick { get; private set; }
        public BanSystem Ban { get; private set; }
        public PauseSystem Pause { get; private set; }

        private void Awake()
        {
            Kick = GetComponent<KickSystem>();
            Ban = GetComponent<BanSystem>();
            Pause = GetComponent<PauseSystem>();
        }

        private void Start()
        {
            if (GameManager.Instance.Role != NetworkRole.Host)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
