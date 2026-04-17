using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Collectibles
{
    /// <summary>
    /// Ítem físico en el mapa (ej. un cubo, una nota).
    /// Detecta triggers locales. Si es el jugador local quien lo toca,
    /// envía al servidor ITEM_COLLECTED.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollectibleItem : MonoBehaviour
    {
        public int ItemId { get; set; }

        private bool _isCollected = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            // TODO: Comprobar si 'other' es el jugador *LOCAL*
            // Solo el cliente local que tocó el item solicita la recolección al Host
            // (ClientNetworkHandler.SendTCP(new ItemCollectedMessage { ... }))
            
            // _isCollected evita múltiples colisiones en un frame, pero el server
            // tiene la decisión final autoritativa de si fue recolectado o no
            
            // Para test:
            if (other.CompareTag("Player"))
            {
                Debug.Log($"[Collectible] Tocando item {ItemId}");
            }
        }

        public void Disappear()
        {
            _isCollected = true;
            // TODO: FX de partículas de recolección
            gameObject.SetActive(false);
        }
    }
}
