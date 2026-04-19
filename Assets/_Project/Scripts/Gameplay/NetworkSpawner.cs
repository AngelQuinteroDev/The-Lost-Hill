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
            if (_gm == null || !_gm.IsHost || _host == null) return;

            // Asegura que el host vea movimiento remoto aunque el evento llegue antes/desfasado.
            SyncRemoteObjectsFromSessions();

            _broadcastTimer += Time.fixedDeltaTime;
            if (_broadcastTimer < BroadcastInterval) return;
            _broadcastTimer = 0f;

            _host.BroadcastWorldState(BuildWorldState());

            // Refuerzo: enviar estado explícito del host (PlayerId 0)
            BroadcastHostLocalState();
        }

        private void BroadcastHostLocalState()
        {
            if (_gm == null || !_gm.IsHost || _host == null) return;
            if (!_playerObjects.TryGetValue(_gm.LocalPlayerId, out var hostGo) || hostGo == null) return;

            // FIX: usar fuente correcta del host local (PlayerControllerM primero)
            if (!TryReadHostLocalTransform(hostGo, out var pos, out var rotY))
            {
                pos = hostGo.transform.position;
                rotY = hostGo.transform.eulerAngles.y;
            }

            ReadAnimationSnapshot(hostGo, out bool isMoving, out bool isRunning, out bool isPickingUp);

            var msg = new PlayerStateMessage
            {
                SenderId = _gm.LocalPlayerId, // host = 0
                PlayerId = _gm.LocalPlayerId,
                Timestamp = Time.unscaledTime,
                PosX = pos.x,
                PosY = pos.y,
                PosZ = pos.z,
                RotY = rotY,
                IsMoving = isMoving,
                IsRunning = isRunning,
                IsPickingUp = isPickingUp,
                IsAlive = true
            };

            _host.Broadcast(msg);
        }

        private void SyncRemoteObjectsFromSessions()
        {
            if (_host == null || _gm == null || !_gm.IsHost) return;

            foreach (var session in _host.Registry.GetAll())
            {
                if (session == null) continue;
                if (session.PlayerId == _gm.LocalPlayerId) continue;
                if (!session.HasReceivedState) continue;

                if (!_playerObjects.TryGetValue(session.PlayerId, out var go) || go == null)
                {
                    SpawnPlayer(session.PlayerId, session.ColorIndex, isLocal: false);
                    if (!_playerObjects.TryGetValue(session.PlayerId, out go) || go == null) continue;
                }

                go.transform.SetPositionAndRotation(
                    session.LastPosition,
                    Quaternion.Euler(0f, session.LastRotationY, 0f));

                ApplyRemoteAnimationState(
                    session.PlayerId,
                    session.LastIsMoving,
                    session.LastIsRunning,
                    session.LastIsPickingUp,
                    session.IsAlive);
            }
        }

        private WorldStateMessage BuildWorldState()
        {
            var snapshots = new List<PlayerSnapshot>();
            foreach (var kvp in _playerObjects)
            {
                if (kvp.Value == null) continue;

                Vector3 pos;
                float rotY;
                bool isMoving, isRunning, isPickingUp, isAlive;

                bool isHostLocal = _gm != null && _gm.IsHost && kvp.Key == _gm.LocalPlayerId;

                if (isHostLocal)
                {
                    // FIX: host local con fuente dedicada
                    if (!TryReadHostLocalTransform(kvp.Value, out pos, out rotY))
                    {
                        var t = kvp.Value.transform;
                        pos = t.position;
                        rotY = t.eulerAngles.y;
                    }

                    ReadAnimationSnapshot(kvp.Value, out isMoving, out isRunning, out isPickingUp);
                    isAlive = true;
                }
                else if (!TryGetAuthoritativeRemoteState(
                    kvp.Key,
                    out pos,
                    out rotY,
                    out isMoving,
                    out isRunning,
                    out isPickingUp,
                    out isAlive))
                {
                    if (!TryReadControllerTransform(kvp.Value, out pos, out rotY))
                    {
                        var t = kvp.Value.transform;
                        pos = t.position;
                        rotY = t.eulerAngles.y;
                    }

                    ReadAnimationSnapshot(kvp.Value, out isMoving, out isRunning, out isPickingUp);
                    isAlive = true;
                }

                snapshots.Add(new PlayerSnapshot
                {
                    PlayerId = kvp.Key,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotY = rotY,
                    ColorIndex = GetColorForPlayer(kvp.Key),
                    IsAlive = isAlive,
                    IsMoving = isMoving,
                    IsRunning = isRunning,
                    IsPickingUp = isPickingUp
                });
            }

            return new WorldStateMessage { Players = snapshots.ToArray() };
        }

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
                ApplyRemoteAnimationState(playerId, ps.IsMoving, ps.IsRunning, ps.IsPickingUp, ps.IsAlive);
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
                    ApplyRemoteAnimationState(playerId, ps.IsMoving, ps.IsRunning, ps.IsPickingUp, ps.IsAlive);
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
                ApplyRemoteAnimationState(p.PlayerId, p.IsMoving, p.IsRunning, p.IsPickingUp, p.IsAlive);
            }
        }

        private void HandleWorldState(WorldStateMessage world)
        {
            foreach (var p in world.Players)
            {
                if (p.PlayerId == _gm.LocalPlayerId) continue;
                ApplyRemotePlayerState(p.PlayerId, p.PosX, p.PosY, p.PosZ, p.RotY);
                ApplyPlayerColor(p.PlayerId, p.ColorIndex);
                ApplyRemoteAnimationState(p.PlayerId, p.IsMoving, p.IsRunning, p.IsPickingUp, p.IsAlive);
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

                Vector3 pos;
                float rotY;
                bool isMoving, isRunning, isPickingUp, isAlive;

                bool isHostLocal = _gm != null && _gm.IsHost && kvp.Key == _gm.LocalPlayerId;

                if (isHostLocal)
                {
                    // FIX: host local con fuente dedicada
                    if (!TryReadHostLocalTransform(kvp.Value, out pos, out rotY))
                    {
                        var t = kvp.Value.transform;
                        pos = t.position;
                        rotY = t.eulerAngles.y;
                    }

                    ReadAnimationSnapshot(kvp.Value, out isMoving, out isRunning, out isPickingUp);
                    isAlive = true;
                }
                else if (!TryGetAuthoritativeRemoteState(
                    kvp.Key,
                    out pos,
                    out rotY,
                    out isMoving,
                    out isRunning,
                    out isPickingUp,
                    out isAlive))
                {
                    if (!TryReadControllerTransform(kvp.Value, out pos, out rotY))
                    {
                        var t = kvp.Value.transform;
                        pos = t.position;
                        rotY = t.eulerAngles.y;
                    }

                    ReadAnimationSnapshot(kvp.Value, out isMoving, out isRunning, out isPickingUp);
                    isAlive = true;
                }

                snapshots.Add(new PlayerSnapshot
                {
                    PlayerId = kvp.Key,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotY = rotY,
                    ColorIndex = GetColorForPlayer(kvp.Key),
                    IsAlive = isAlive,
                    IsMoving = isMoving,
                    IsRunning = isRunning,
                    IsPickingUp = isPickingUp
                });
            }

            return new WorldSnapshotMessage
            {
                Players = snapshots.ToArray(),
                CurrentGameState = (byte)GameState.Playing
            };
        }

        // Fuente dedicada para el host local
        private bool TryReadHostLocalTransform(GameObject go, out Vector3 pos, out float rotY)
        {
            pos = Vector3.zero;
            rotY = 0f;
            if (go == null) return false;

            var cm = go.GetComponent<PlayerControllerM>() ?? go.GetComponentInChildren<PlayerControllerM>(true);
            if (cm != null)
            {
                pos = cm.transform.position;
                rotY = cm.transform.eulerAngles.y;
                return true;
            }

            var cc = go.GetComponent<CharacterController>() ?? go.GetComponentInChildren<CharacterController>(true);
            if (cc != null)
            {
                pos = cc.transform.position;
                rotY = cc.transform.eulerAngles.y;
                return true;
            }

            var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                pos = rb.transform.position;
                rotY = rb.transform.eulerAngles.y;
                return true;
            }

            pos = go.transform.position;
            rotY = go.transform.eulerAngles.y;
            return true;
        }

        private static bool TryReadControllerTransform(GameObject go, out Vector3 pos, out float rotY)
        {
            pos = Vector3.zero;
            rotY = 0f;
            if (go == null) return false;

            // FIX: priorizar PlayerControllerM para POS y ROT
            var cm = go.GetComponent<PlayerControllerM>() ?? go.GetComponentInChildren<PlayerControllerM>(true);
            if (cm != null)
            {
                pos = cm.transform.position;
                rotY = cm.transform.eulerAngles.y;
                return true;
            }

            var cc = go.GetComponent<CharacterController>() ?? go.GetComponentInChildren<CharacterController>(true);
            if (cc != null)
            {
                pos = cc.transform.position;
                rotY = cc.transform.eulerAngles.y;
                return true;
            }

            var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                pos = rb.transform.position;
                rotY = rb.transform.eulerAngles.y;
                return true;
            }

            return false;
        }

        private void ReadAnimationSnapshot(GameObject go, out bool isMoving, out bool isRunning, out bool isPickingUp)
        {
            isMoving = false;
            isRunning = false;
            isPickingUp = false;
            if (go == null) return;

            // SOLO PlayerControllerM
            var controllerM = go.GetComponent<PlayerControllerM>() ?? go.GetComponentInChildren<PlayerControllerM>(true);
            if (controllerM != null)
            {
                isMoving = controllerM.NetIsMoving;
                isRunning = controllerM.NetIsRunning;
                isPickingUp = controllerM.NetIsPickingUp;
                return;
            }

            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                isRunning = TryGetAnimatorBool(animator, "isRunning");
                bool isWalking = TryGetAnimatorBool(animator, "isWalking");
                isPickingUp = TryGetAnimatorBool(animator, "isPickingUp");
                isMoving = isRunning || isWalking;
            }
        }

        private static bool TryGetAnimatorBool(Animator animator, string param)
        {
            if (animator == null || string.IsNullOrEmpty(param)) return false;

            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                    return animator.GetBool(param);
            }

            return false;
        }

        private void ApplyRemoteAnimationState(int playerId, bool isMoving, bool isRunning, bool isPickingUp, bool isAlive)
        {
            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null) return;

            var visuals = go.GetComponent<PlayerVisuals>() ?? go.GetComponentInChildren<PlayerVisuals>(true);
            if (visuals != null)
            {
                visuals.SetAnimationState(isMoving, isRunning, isPickingUp, isAlive);
                return;
            }

            // Fallback si no hay PlayerVisuals
            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            bool walking = isMoving && !isRunning && !isPickingUp;
            bool idle = !isMoving && !isPickingUp && isAlive;

            SetAnimatorBoolIfExists(animator, "isRunning", isRunning && isAlive);
            SetAnimatorBoolIfExists(animator, "isWalking", walking && isAlive);
            SetAnimatorBoolIfExists(animator, "isPickingUp", isPickingUp && isAlive);
            SetAnimatorBoolIfExists(animator, "isIdle", idle);
        }

        private static void SetAnimatorBoolIfExists(Animator animator, string param, bool value)
        {
            if (animator == null || string.IsNullOrEmpty(param)) return;
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                {
                    animator.SetBool(param, value);
                    return;
                }
            }
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

            GameObject go = null;

            // If spawning the local player, first check if one already exists in the scene
            if (isLocal)
            {
                var existingPlayers = FindObjectsOfType<PlayerControllerM>();
                foreach (var p in existingPlayers)
                {
                    if (p.IsLocalPlayer && !p.name.Contains("RemotePlayer"))
                    {
                        go = p.gameObject;
                        Debug.Log($"[NetworkSpawner] Found pre-existing local player {go.name} for P{playerId}");
                        break;
                    }
                }
            }

            if (go == null)
            {
                var prefab = isLocal ? _localPlayerPrefab : _remotePlayerPrefab;
                if (prefab == null)
                {
                    Debug.LogError($"[NetworkSpawner] Prefab {(isLocal ? "_localPlayerPrefab" : "_remotePlayerPrefab")} es null. No se puede crear P{playerId}.");
                    return;
                }

                var spawnPos = GetSpawnPosition(playerId);
                go = Instantiate(prefab, spawnPos, Quaternion.identity);
                go.name = isLocal ? $"LocalPlayer_{playerId}" : $"RemotePlayer_{playerId}";
                Debug.Log($"[NetworkSpawner] Spawned P{playerId} (local={isLocal}, color={colorIndex}) @ {spawnPos}");
            }

            // SOLO PlayerControllerM
            var controllerM = go.GetComponent<PlayerControllerM>() ?? go.GetComponentInChildren<PlayerControllerM>(true);
            if (controllerM != null) controllerM.Initialize(isLocal);

            var netSync = go.GetComponent<PlayerNetworkSync>() ?? go.GetComponentInChildren<PlayerNetworkSync>(true);
            if (netSync == null && isLocal && _gm != null && _gm.Role == NetworkRole.Client)
                netSync = go.AddComponent<PlayerNetworkSync>();

            if (netSync != null)
                netSync.Initialize(playerId, isLocal);
            else if (isLocal && _gm != null && _gm.Role == NetworkRole.Client)
                Debug.LogError("[NetworkSpawner] El prefab local no tiene PlayerNetworkSync; no se enviará PlayerState.");

            _playerObjects[playerId] = go;
            ApplyPlayerColor(playerId, colorIndex);
        }

        private void ApplyPlayerColor(int playerId, int colorIndex)
        {
            if (colorIndex < 0) colorIndex = 0;
            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null) return;

            var netSync = go.GetComponent<PlayerNetworkSync>() ?? go.GetComponentInChildren<PlayerNetworkSync>(true);
            if (netSync != null) netSync.ApplyColor(colorIndex);

            var visuals = go.GetComponent<PlayerVisuals>() ?? go.GetComponentInChildren<PlayerVisuals>(true);
            if (visuals != null) visuals.SetColorIndex(colorIndex);
        }

        private int GetColorForPlayer(int playerId)
        {
            if (_gm != null && playerId == _gm.LocalPlayerId)
                return _gm.LocalColorIndex;

            if (_host != null && _host.Registry.TryGet(playerId, out var session))
                return session.ColorIndex;

            return 0;
        }

        private void ApplyInputToRemotePlayer(int playerId, float inputX, float inputZ, bool sprint)
        {
            if (!_playerObjects.TryGetValue(playerId, out var go) || go == null) return;

            // SOLO PlayerControllerM
            var controllerM = go.GetComponent<PlayerControllerM>() ?? go.GetComponentInChildren<PlayerControllerM>(true);
            if (controllerM != null)
                controllerM.ApplyRemoteInput(inputX, inputZ, sprint);
        }

        private bool TryGetAuthoritativeRemoteState(
            int playerId,
            out Vector3 pos,
            out float rotY,
            out bool isMoving,
            out bool isRunning,
            out bool isPickingUp,
            out bool isAlive)
        {
            pos = Vector3.zero;
            rotY = 0f;
            isMoving = false;
            isRunning = false;
            isPickingUp = false;
            isAlive = true;

            if (_gm == null || !_gm.IsHost || _host == null) return false;
            if (playerId == _gm.LocalPlayerId) return false;

            if (_host.Registry.TryGet(playerId, out var session) && session != null && session.HasReceivedState)
            {
                pos = session.LastPosition;
                rotY = session.LastRotationY;
                isMoving = session.LastIsMoving;
                isRunning = session.LastIsRunning;
                isPickingUp = session.LastIsPickingUp;
                isAlive = session.IsAlive;
                return true;
            }

            return false;
        }
    }
}