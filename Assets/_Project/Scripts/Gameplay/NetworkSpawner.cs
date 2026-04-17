using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Gameplay.Player;
using System.Collections.Generic;

namespace TheLostHill.Gameplay
{
    /// <summary>
    /// Se encarga de instanciar los prefabs de los jugadores cuando se entra en la escena de juego.
    /// </summary>
    public class NetworkSpawner : MonoBehaviour
    {
        public static NetworkSpawner Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject PlayerPrefab;
        
        // Registro de transforms para que GameSessionManager pueda leerlos
        public Dictionary<int, PlayerNetworkSync> ActivePlayers = new Dictionary<int, PlayerNetworkSync>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }



        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.StateMachine.CurrentState == GameState.Playing)
            {
                SpawnLocalAndRemotePlayers();
            }

            // Suscribirse a mensajes de red
            if (GameManager.Instance != null && GameManager.Instance.ClientHandler != null)
            {
                GameManager.Instance.ClientHandler.OnMessageReceived += HandleNetworkMessage;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.ClientHandler != null)
            {
                GameManager.Instance.ClientHandler.OnMessageReceived -= HandleNetworkMessage;
            }
        }

        private void HandleNetworkMessage(Network.Shared.NetworkMessage msg)
        {
            if (msg is Network.Shared.WorldStateMessage worldState)
            {
                ProcessSnapshots(worldState.Players);
            }
            else if (msg is Network.Shared.WorldSnapshotMessage worldSnapshot)
            {
                Debug.Log("[Spawner] Recibido Snapshot completo del mundo.");
                ProcessSnapshots(worldSnapshot.Players);
            }
        }

        private void ProcessSnapshots(Network.Shared.PlayerSnapshot[] snapshots)
        {
            if (snapshots == null) return;

            foreach (var playerSnap in snapshots)
            {
                if (ActivePlayers.TryGetValue(playerSnap.PlayerId, out PlayerNetworkSync sync))
                {
                    // Actualizar posición/rotación e informar del color
                    sync.ApplyServerState(new Vector3(playerSnap.PosX, playerSnap.PosY, playerSnap.PosZ), playerSnap.RotY);
                    sync.ApplyColor(playerSnap.ColorIndex);
                }
                else
                {
                    // HOT-JOIN: Si no lo tenemos en el diccionario, es un jugador nuevo o que ya estaba
                    Debug.Log($"[Spawner] Detectado jugador nuevo en snapshot: ID {playerSnap.PlayerId}. Spawneando...");
                    
                    string remoteName = (playerSnap.PlayerId == 0) ? "Host" : $"Jugador {playerSnap.PlayerId}";
                    SpawnPlayer(playerSnap.PlayerId, remoteName, false, playerSnap.ColorIndex);
                }
            }
        }

        public void SpawnLocalAndRemotePlayers()
        {
            // 1. Spawneamos al jugador LOCAL
            // Obtenemos nuestro color desde el handler o por defecto 0
            int localColor = 0;
            if (GameManager.Instance.Role == NetworkRole.Client && GameManager.Instance.ClientHandler != null)
            {
                localColor = GameManager.Instance.ClientHandler.ColorIndex;
            }
            
            SpawnPlayer(GameManager.Instance.LocalPlayerId, GameManager.Instance.LocalPlayerName, true, localColor);

            // 2. Si somos el Host, el Registry tiene a todos
            if (GameManager.Instance.Role == NetworkRole.Host)
            {
                foreach (var session in GameManager.Instance.HostManager.Registry.GetAll())
                {
                    if (session.PlayerId != GameManager.Instance.LocalPlayerId)
                    {
                        SpawnPlayer(session.PlayerId, session.PlayerName, false, session.ColorIndex);
                    }
                }
            }
        }

        public void SpawnPlayer(int playerId, string playerName, bool isLocal, int colorIndex = 0)
        {
            if (ActivePlayers.ContainsKey(playerId)) return;

            // Spawnear cerca de la posición del Spawner con un pequeño offset circular para no encimarlos
            Vector3 spawnPos = transform.position + Quaternion.Euler(0, playerId * 45, 0) * Vector3.forward * 2f;
            spawnPos.y += 1f; // Un poco elevado para caer

            Debug.Log($"[Spawner] Spawning {(isLocal ? "Local" : "Remote")} Player: {playerName} (ID: {playerId}) en {spawnPos} con Color: {colorIndex}");

            GameObject go = Instantiate(PlayerPrefab, spawnPos, Quaternion.identity);
            go.name = isLocal ? "LocalPlayer" : $"RemotePlayer_{playerId}";

            var controller = go.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.IsLocalPlayer = isLocal;
            }

            var sync = go.GetComponent<PlayerNetworkSync>();
            if (sync != null)
            {
                sync.AssignedPlayerId = playerId;
                sync.ApplyColor(colorIndex); // Aplicar color inmediatamente
                ActivePlayers[playerId] = sync;
            }

            // Si es remoto, desactivamos componentes físicos para que solo la interpolación lo mueva
            if (!isLocal)
            {
                var cc = go.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }
        }
    }
}
