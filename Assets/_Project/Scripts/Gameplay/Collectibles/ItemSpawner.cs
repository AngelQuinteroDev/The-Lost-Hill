using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Collectibles
{
    /// <summary>
    /// Aparece ítems en el mapa al inicio de la ronda.
    /// Solo interactúa en el lado del Host.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        public GameObject ItemPrefab;
        public Transform[] SpawnPoints;

        public void SpawnItems(int amount)
        {
            if (GameManager.Instance.Role != NetworkRole.Host) return;

            int toSpawn = Mathf.Min(amount, SpawnPoints.Length);
            
            // Un algoritmo simple: shuffle de los puntos y tomar 'toSpawn'
            ShufflePoints();

            for (int i = 0; i < toSpawn; i++)
            {
                Transform spot = SpawnPoints[i];
                GameObject go = Instantiate(ItemPrefab, spot.position, spot.rotation);
                
                CollectibleItem item = go.GetComponent<CollectibleItem>();
                item.ItemId = i + 1;

                CollectibleManager.Instance.RegisterItem(item);

                // Enviar a los clientes mediante el HostManager si se desea hot-join
                // Normalmente al cargar la escena, se enviaría el ItemsSyncMessage
            }
        }

        private void ShufflePoints()
        {
            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                Transform temp = SpawnPoints[i];
                int randomIndex = Random.Range(i, SpawnPoints.Length);
                SpawnPoints[i] = SpawnPoints[randomIndex];
                SpawnPoints[randomIndex] = temp;
            }
        }
    }
}
