using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Admin
{
    public class KickSystem : MonoBehaviour
    {
        public void KickPlayer(int playerId, string reason = "Removido por el admin.")
        {
            if (GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.KickPlayer(playerId, reason);
            }
        }
    }
}
