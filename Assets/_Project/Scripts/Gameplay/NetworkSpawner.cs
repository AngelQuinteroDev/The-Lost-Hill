using System.Collections.Generic;
using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using TheLostHill.Network.Host;
using TheLostHill.Network.Client;
using TheLostHill.Network.Sync;
using TheLostHill.Gameplay.Player;

namespace TheLostHill.Gameplay
{
    /// <summary>
    /// Vive en la escena Gameplay. Se destruye al salir.
    /// 
    /// RESPONSABILIDADES:
    ///   · HOST: Al iniciar, spawnea a todos (host incluido). Escucha PlayerJoined/Left y spawnea/despawnea.
    ///           Cada FixedUpdate lee posiciones reales y hace Broadcast de WorldState.
    ///   · CLIENTE: Al iniciar, spawnea solo su player local. Espera WorldSnapshot/WorldState
    ///              para spawnear a los remotos y actualizar posiciones.
    /// 
    /// REGLA CLAVE: Este script se suscribe a OnMessageReceived en Awake y se desuscribe en OnDestroy.
    ///              El GameManager hace FlushPendingWorldStates() DESPUÉS de que esta escena cargó,
    ///              garantizando que los mensajes buffereados lleguen cuando ya hay listener.
    /// </summary>
    public class NetworkSpawner : MonoBehaviour
    {
        public static NetworkSpawner Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject _localPlayerPrefab;
        [SerializeField] private GameObject _remotePlayerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] _spawnPoints;

        // ── Estado interno ──────────────────────────────────────
        // playerId → instancia de GameObject
        private readonly Dictionary<int, GameObject> _playerObjects = new Dictionary<int, GameObject>();

        private GameManager _gm;
        private HostNetworkManager _host;
        private ClientNetworkHandler _client;

        // ── Flag para el cliente: indica que ya estamos listos para recibir WorldState ──
        public static bool IsClientWorldListenerReady { get; private set; }

        // ── Tick de broadcast del host ──────────────────────────
        private float _broadcastTimer;
        private const float BroadcastInterval = 0.05f; // 20 Hz

        // ════════════════════════════════════════════════════════
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _gm     = GameManager.Instance;
            _host   = _gm.HostManager;
            _client = _gm.ClientHandler;
        }

        private void Start()
        {
            ValidateAndResolvePrefabs();

            if (_gm == null)
            {
                Debug.LogError("[NetworkSpawner] GameManager.Instance es null. No se puede inicializar spawner.");
                enabled = false;
                return;
            }

            if (_gm.IsHost)
            {
                if (_host == null)
                {
                    Debug.LogError("[NetworkSpawner] HostManager es null en modo Host.");
                    enabled = false;
                    return;
                }

                // Suscribirse a eventos del host
                _host.OnClientConnected    += OnClientConnected;
                _host.OnClientDisconnected += OnClientDisconnected;
                _host.OnMessageReceived    += OnHostMessageReceived;

                // Spawnear al host como jugador 0
                SpawnPlayer(_gm.LocalPlayerId, _gm.LocalColorIndex, isLocal: true);

                // Spawnear a todos los clientes ya conectados
                foreach (var session in _host.Registry.GetAll())
                    SpawnPlayer(session.PlayerId, session.ColorIndex, isLocal: false);

                // Enviar WorldSnapshot a cada cliente para sincronizar el estado inicial
                SendInitialSnapshotToAll();
            }
            else // Cliente
            {
                if (_client == null)
                {
                    Debug.LogError("[NetworkSpawner] ClientHandler es null en modo Client.");
                    enabled = false;
                    return;
                }

                // Suscribirse ANTES de hacer flush
                _client.OnMessageReceived += OnClientMessageReceived;
                IsClientWorldListenerReady = true;

                // Spawnear al jugador local inmediatamente
                SpawnPlayer(_gm.LocalPlayerId, _gm.LocalColorIndex, isLocal: true);

                // El GameManager ya llamó FlushPendingWorldStates() después de cargar esta escena,
                // pero si por timing llegó primero el Start, los mensajes del flush llegarán ahora
                // porque IsClientWorldListenerReady ya es true y ClientNetworkHandler los routeará aquí.
            }
        }

        private void OnDestroy()
        {
            Instance = null;
            IsClientWorldListenerReady = false;

            if (_host != null)
            {
                _host.OnClientConnected    -= OnClientConnected;
                _host.OnClientDisconnected -= OnClientDisconnected;
                _host.OnMessageReceived    -= OnHostMessageReceived;
            }

            if (_client != null)
                _client.OnMessageReceived -= OnClientMessageReceived;
        }

        // ════════════════════════════════════════════════════════
        //  HOST: BROADCAST DE WORLD STATE (FixedUpdate)
        // ════════════════════════════════════════════════════════
        private void FixedUpdate()
        {
            // Volver al comportamiento estable: el host emite WorldState desde aquí.
            if (_gm == null || !_gm.IsHost || _host == null) return;

            _broadcastTimer += Time.fixedDeltaTime;
            if (_broadcastTimer < BroadcastInterval) return;
            _broadcastTimer = 0f;

            _host.BroadcastWorldState(BuildWorldState());
        }

        private WorldStateMessage BuildWorldState()
        {
            var snapshots = new List<PlayerSnapshot>();
            foreach (var kvp in _playerObjects)
            {
                if (kvp.Value == null) continue;
                var t = kvp.Value.transform;
                snapshots.Add(new PlayerSnapshot
                {
                    PlayerId   = kvp.Key,
                    PosX       = t.position.x,
                    PosY       = t.position.y,
                    PosZ       = t.position.z,
                    RotY       = t.eulerAngles.y,
                    ColorIndex = GetColorForPlayer(kvp.Key),
                    IsAlive    = true
                });
            }
            return new WorldStateMessage { Players = snapshots.ToArray() };
        }

        // ════════════════════════════════════════════════════════
        //  HOST: MENSAJES ENTRANTES
        // ════════════════════════════════════════════════════════
        private void OnHostMessageReceived(NetworkMessage msg)
        {
            if (msg is PlayerInputMessage input)
            {
                ApplyInputToRemotePlayer(input.SenderId, input.InputX, input.InputZ, input.Sprint);
                return;
            }

            if (msg is PlayerStateMessage ps)
            {
                int playerId = ps.SenderId > 0 ? ps.SenderId : ps.PlayerId;
                ApplyRemotePlayerState(playerId, ps.PosX, ps.PosY, ps.PosZ, ps.RotY);
            }
        }

        private void ApplyRemotePlayerState(int playerId, float posX, float posY, float posZ, float rotY)
        {
            ApplyRemotePlayerState(playerId, posX, posY, posZ, rotY, false);
        }

        private void ApplyRemotePlayerState(int playerId, float posX, float posY, float posZ, float rotY, bool resetInterpolation)
        {
            // 0 es válido (host). Solo invalidar negativos y nuestro propio player local.
            if (playerId < 0 || playerId == _gm.LocalPlayerId) return;

            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null)
            {
                int colorIndex = 0;
                if (_host != null && _host.Registry.TryGet(playerId, out var session))
                    colorIndex = session.ColorIndex;

                SpawnPlayer(playerId, colorIndex, isLocal: false);
                if (!_playerObjects.TryGetValue(playerId, out go) || go == null) return;
                resetInterpolation = true;
            }

            Vector3 targetPos = new Vector3(posX, posY, posZ);
            Quaternion targetRot = Quaternion.Euler(0f, rotY, 0f);

            if (_gm != null && _gm.IsHost)
            {
                go.transform.SetPositionAndRotation(targetPos, targetRot);
                return;
            }

            var interp = go.GetComponent<InterpolationSystem>();
            if (interp != null)
            {
                if (resetInterpolation)
                {
                    go.transform.SetPositionAndRotation(targetPos, targetRot);
                    interp.Clear();
                }

                interp.AddSnapshot(targetPos, rotY, Time.time);
            }
            else
            {
                go.transform.SetPositionAndRotation(targetPos, targetRot);
            }
        }

        private void OnClientConnected(int playerId, string playerName)
        {
            if (!_playerObjects.ContainsKey(playerId))
            {
                // Obtener colorIndex de la sesión
                int colorIndex = 0;
                if (_host.Registry.TryGet(playerId, out var session))
                    colorIndex = session.ColorIndex;

                SpawnPlayer(playerId, colorIndex, isLocal: false);
            }

            // Enviar snapshot del mundo actual al nuevo cliente
            SendInitialSnapshotToClient(playerId);
        }

        private void OnClientDisconnected(int playerId)
        {
            DespawnPlayer(playerId);
        }

        // ════════════════════════════════════════════════════════
        //  CLIENTE: MENSAJES ENTRANTES
        // ════════════════════════════════════════════════════════
        private void OnClientMessageReceived(NetworkMessage msg)
        {
            switch (msg)
            {
                // Snapshot inicial: lista completa de todos los jugadores en el mundo
                case WorldSnapshotMessage snap:
                    HandleWorldSnapshot(snap);
                    break;

                // Update de alta frecuencia: posiciones de todos
                case WorldStateMessage world:
                    HandleWorldState(world);
                    break;

                case PlayerStateMessage ps:
                {
                    int playerId = ps.PlayerId > 0 ? ps.PlayerId : ps.SenderId;
                    ApplyRemotePlayerState(playerId, ps.PosX, ps.PosY, ps.PosZ, ps.RotY);
                    break;
                }

                // Alguien se conectó mientras estábamos jugando
                case PlayerJoinedMessage joined:
                    if (joined.PlayerId != _gm.LocalPlayerId && !_playerObjects.ContainsKey(joined.PlayerId))
                        SpawnPlayer(joined.PlayerId, joined.ColorIndex, isLocal: false);
                    break;

                // Alguien se desconectó
                case PlayerLeftMessage left:
                    DespawnPlayer(left.PlayerId);
                    break;

                // Kick recibido: desconectarse
                case KickPlayerMessage kick when kick.TargetPlayerId == _gm.LocalPlayerId:
                    Debug.Log($"[NetworkSpawner] Fuiste expulsado: {kick.Reason}");
                    _client.Disconnect();
                    _gm.ChangeState(GameState.MainMenu);
                    UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.MainMenuScene);
                    break;
            }
        }

        private void HandleWorldSnapshot(WorldSnapshotMessage snap)
        {
            foreach (var p in snap.Players)
            {
                if (p.PlayerId == _gm.LocalPlayerId) continue;
                ApplyRemotePlayerState(p.PlayerId, p.PosX, p.PosY, p.PosZ, p.RotY, true);
                ApplyPlayerColor(p.PlayerId, p.ColorIndex);
            }
        }

        private void HandleWorldState(WorldStateMessage world)
        {
            foreach (var p in world.Players)
            {
                if (p.PlayerId == _gm.LocalPlayerId) continue;
                ApplyRemotePlayerState(p.PlayerId, p.PosX, p.PosY, p.PosZ, p.RotY);
                ApplyPlayerColor(p.PlayerId, p.ColorIndex);
            }
        }


        private void DespawnPlayer(int playerId)
        {
            if (_playerObjects.TryGetValue(playerId, out var go))
            {
                if (go != null) Destroy(go);
                _playerObjects.Remove(playerId);
                Debug.Log($"[NetworkSpawner] Despawned P{playerId}");
            }
        }

        private Vector3 GetSpawnPosition(int playerId)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
                return _spawnPoints[playerId % _spawnPoints.Length].position;
            // Fallback: posiciones en línea
            return new Vector3(playerId * 2f, 0f, 0f);
        }

        // ════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════
        private void SendInitialSnapshotToAll()
        {
            var snap = BuildWorldSnapshot();
            foreach (var session in _host.Registry.GetAll())
                _host.SendToClient(session.PlayerId, snap);
        }

        private void SendInitialSnapshotToClient(int playerId)
        {
            var snap = BuildWorldSnapshot();
            _host.SendToClient(playerId, snap);
        }

        private WorldSnapshotMessage BuildWorldSnapshot()
        {
            var snapshots = new List<PlayerSnapshot>();
            foreach (var kvp in _playerObjects)
            {
                if (kvp.Value == null) continue;
                var t = kvp.Value.transform;
                snapshots.Add(new PlayerSnapshot
                {
                    PlayerId   = kvp.Key,
                    PosX       = t.position.x,
                    PosY       = t.position.y,
                    PosZ       = t.position.z,
                    RotY       = t.eulerAngles.y,
                    ColorIndex = GetColorForPlayer(kvp.Key),
                    IsAlive    = true
                });
            }
            return new WorldSnapshotMessage
            {
                Players          = snapshots.ToArray(),
                CurrentGameState = (byte)GameState.Playing
            };
        }

        private int GetColorForPlayer(int playerId)
        {
            if (playerId == _gm.LocalPlayerId) return _gm.LocalColorIndex;
            if (_host != null && _host.Registry.TryGet(playerId, out var session))
                return session.ColorIndex;
            return 0;
        }

        private void ApplyInputToRemotePlayer(int playerId, float inputX, float inputZ, bool sprint)
        {
            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null) return;
            var ctrl = go.GetComponent<PlayerController>();
            ctrl?.ApplyRemoteInput(inputX, inputZ, sprint);
        }

        // ── API pública ──────────────────────────────────────────
        public GameObject GetPlayerObject(int playerId)
        {
            _playerObjects.TryGetValue(playerId, out var go);
            return go;
        }

        public IReadOnlyDictionary<int, GameObject> ActivePlayers => _playerObjects;

        private void ValidateAndResolvePrefabs()
        {
            if (_localPlayerPrefab == null && _remotePlayerPrefab != null)
            {
                Debug.LogWarning("[NetworkSpawner] _localPlayerPrefab no asignado. Usando _remotePlayerPrefab como fallback.");
                _localPlayerPrefab = _remotePlayerPrefab;
            }

            if (_remotePlayerPrefab == null && _localPlayerPrefab != null)
            {
                Debug.LogWarning("[NetworkSpawner] _remotePlayerPrefab no asignado. Usando _localPlayerPrefab como fallback.");
                _remotePlayerPrefab = _localPlayerPrefab;
            }

            if (_localPlayerPrefab == null && _remotePlayerPrefab == null)
            {
                Debug.LogError("[NetworkSpawner] No hay prefabs asignados (local/remote). Asigna ambos en el inspector.");
            }
        }

        private void SpawnPlayer(int playerId, int colorIndex, bool isLocal)
        {
            if (_playerObjects.ContainsKey(playerId)) return;

            var prefab = isLocal ? _localPlayerPrefab : _remotePlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError($"[NetworkSpawner] Prefab {(isLocal ? "_localPlayerPrefab" : "_remotePlayerPrefab")} es null. No se puede crear P{playerId}.");
                return;
            }

            var spawnPos = GetSpawnPosition(playerId);
            var go = Instantiate(prefab, spawnPos, Quaternion.identity);
            go.name = isLocal ? $"LocalPlayer_{playerId}" : $"RemotePlayer_{playerId}";

            var controller = go.GetComponent<PlayerController>();
            if (controller != null)
                controller.Initialize(isLocal);

            var netSync = go.GetComponent<PlayerNetworkSync>();
            if (netSync == null && isLocal && _gm != null && _gm.Role == NetworkRole.Client)
                netSync = go.AddComponent<PlayerNetworkSync>();

            if (netSync != null)
                netSync.Initialize(playerId, isLocal);
            else if (isLocal && _gm != null && _gm.Role == NetworkRole.Client)
                Debug.LogError("[NetworkSpawner] El prefab local no tiene PlayerNetworkSync; no se enviará PlayerState.");

            _playerObjects[playerId] = go;
            ApplyPlayerColor(playerId, colorIndex);

            Debug.Log($"[NetworkSpawner] Spawned P{playerId} (local={isLocal}, color={colorIndex}) @ {spawnPos}");
        }

        private void ApplyPlayerColor(int playerId, int colorIndex)
        {
            if (colorIndex < 0) colorIndex = 0;
            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null) return;

            var netSync = go.GetComponent<PlayerNetworkSync>();
            if (netSync != null)
                netSync.ApplyColor(colorIndex);

            var visuals = go.GetComponent<PlayerVisuals>();
            if (visuals != null)
                visuals.SetColorIndex(colorIndex);
        }
    }
}