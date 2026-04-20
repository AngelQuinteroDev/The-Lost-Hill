using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Collectibles
{

    [RequireComponent(typeof(Collider))]
    public class CollectibleItem : MonoBehaviour
    {
        public int ItemId { get; set; }

        private bool _isCollected = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

  
            if (other.CompareTag("Player"))
            {
                Debug.Log($"[Collectible] Tocando item {ItemId}");
            }
        }

        public void Disappear()
        {
            _isCollected = true;

            gameObject.SetActive(false);
        }
    }
}
