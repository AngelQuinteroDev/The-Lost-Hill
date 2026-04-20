using UnityEngine;

namespace TheLostHill.UI.HUD
{
    public class PlayerListUI : MonoBehaviour
    {
        public Transform Container;
        public GameObject PlayerRowPrefab;

        void Update()
        {
            // Detectar 'Tab' para mostrar el scoreboard de los jugadores vivos/muertos
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Container.gameObject.SetActive(true);
                RefreshList();
            }
            else if (Input.GetKeyUp(KeyCode.Tab))
            {
                Container.gameObject.SetActive(false);
            }
        }

        private void RefreshList()
        {
            // TODO: Iterar sobre registros locales del ClientHandler
            // Instanciar filas con -> Nombre, Ping, Status (Alive/Dead)
        }
    }
}
