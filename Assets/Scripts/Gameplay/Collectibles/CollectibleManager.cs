using System.Collections.Generic;
using UnityEngine;

namespace TheLostHill.Gameplay.Collectibles
{
 
    public class CollectibleManager : MonoBehaviour
    {
        public static CollectibleManager Instance { get; private set; }

        private readonly Dictionary<int, CollectibleItem> _activeItems = new Dictionary<int, CollectibleItem>();
        
        public int TotalItems { get; private set; }
        public int CollectedItems { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RegisterItem(CollectibleItem item)
        {
            _activeItems[item.ItemId] = item;
            TotalItems++;
        }

        public void HandleItemCollectedCommand(int itemId, int collectorPlayerId)
        {
            
            if (_activeItems.ContainsKey(itemId))
            {
                DoCollectItem(itemId);
      
                
                if (CheckWinCondition())
                {
                    
                }
            }
        }

        public void OnReceiveItemCollected(int itemId, int collectorPlayerId)
        {
            
            DoCollectItem(itemId);
        }

        private void DoCollectItem(int itemId)
        {
            if (_activeItems.TryGetValue(itemId, out CollectibleItem item))
            {
                item.Disappear();
                _activeItems.Remove(itemId);
                CollectedItems++;
            }
        }

        private bool CheckWinCondition()
        {
            return CollectedItems >= TotalItems && TotalItems > 0;
        }

        public void ResetSession()
        {
            _activeItems.Clear();
            TotalItems = 0;
            CollectedItems = 0;
        }
    }
}
