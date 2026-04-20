using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Collectibles
{
  
    public class ItemSpawner : MonoBehaviour
    {
        public GameObject ItemPrefab;
        public Transform[] SpawnPoints;

        public void SpawnItems(int amount)
        {
            if (GameManager.Instance.Role != NetworkRole.Host) return;

            int toSpawn = Mathf.Min(amount, SpawnPoints.Length);
            

            ShufflePoints();

            for (int i = 0; i < toSpawn; i++)
            {
                Transform spot = SpawnPoints[i];
                GameObject go = Instantiate(ItemPrefab, spot.position, spot.rotation);
                
                CollectibleItem item = go.GetComponent<CollectibleItem>();
                item.ItemId = i + 1;

                CollectibleManager.Instance.RegisterItem(item);

               
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
