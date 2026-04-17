using UnityEngine;
using TMPro;
using TheLostHill.Gameplay.Collectibles;

namespace TheLostHill.UI.HUD
{
    /// <summary>
    /// UI que refleja los ítems coleccionables obtenidos vs. objetivo.
    /// </summary>
    public class CollectiblesProgressUI : MonoBehaviour
    {
        public TextMeshProUGUI ProgressText;

        private void Update()
        {
            if (CollectibleManager.Instance != null && ProgressText != null)
            {
                ProgressText.text = $"{CollectibleManager.Instance.CollectedItems} / {CollectibleManager.Instance.TotalItems} Objects";
            }
        }
    }
}
