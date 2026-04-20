using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Admin
{
    public class BanSystem : MonoBehaviour
    {
        public void BanPlayer(int playerId, string reason = "Violación de reglas.")
        {
            if (GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.BanPlayer(playerId, reason);
            }
        }

        public void UnbanIP(string ip)
        {
            if (GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.BanList.Unban(ip);
            }
        }
    }
}
