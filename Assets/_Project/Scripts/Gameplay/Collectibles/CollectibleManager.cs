using System.Collections.Generic;
using UnityEngine;

namespace TheLostHill.Gameplay.Collectibles
{
    /// <summary>
    /// Registro global de todos los ítems de la partida.
    /// En el Host: recibe cuando alguien recolecta, actualiza estado, propaga.
    /// En el Cliente: escucha eventos del servidor para desaparecer ítems y contar.
    /// </summary>
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
            // Solo Host llama esto. Hace broadcast y ejecuta localmente
            if (_activeItems.ContainsKey(itemId))
            {
                DoCollectItem(itemId);
                // GameManager.Instance.HostManager.BroadcastTCP(ItemCollectedMsg...)
                
                if (CheckWinCondition())
                {
                    // Triggerear victoria (GameStateChange)
                }
            }
        }

        public void OnReceiveItemCollected(int itemId, int collectorPlayerId)
        {
            // Clientes llaman a esto al recibir el evento
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
