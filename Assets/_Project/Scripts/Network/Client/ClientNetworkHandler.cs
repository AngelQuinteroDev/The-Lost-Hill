using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;
using TheLostHill.Core;
using TheLostHill.Gameplay;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Client
{
    /// <summary>
    /// Cliente UDP: un solo socket y puerto compartido con el host (player-host).
    /// </summary>
    public class ClientNetworkHandler : MonoBehaviour
    {
        [Header("Client Config")]
        public string ServerIP = "127.0.0.1";

        [FormerlySerializedAs("TcpPort")]
        public int ServerPort = Constants.DefaultNetworkPort;

        public string PlayerName = "Player";

        public MessageQueue IncomingQueue { get; private set; }
        private ReconnectHandler _reconnectHandler;

        public int LocalPlayerId { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsConnecting { get; private set; }

        private UdpClient _udpClient;
        private IPEndPoint _serverEndPoint;
        private Thread _receiveThread;
        private volatile bool _isRunning;
        private readonly object _sendLock = new object();
        private string _cachedVersion;
        private float _nextKeepAliveUnscaled = float.NegativeInfinity;

        /// <summary>
        /// WorldState puede llegar mientras GameplayScene carga; NetworkSpawner aún no existe y no hay suscriptores.
        /// </summary>
        private readonly List<NetworkMessage> _pendingWorldGameplay = new List<NetworkMessage>();
        // WorldState / WorldSnapshot / PlayerState pueden llegar mientras GameplayScene carga.
        private const int MaxPendingWorldGameplay = 128;

        /// <summary>Otros clientes en lobby (el host no está aquí; id 0 es el host).</summary>
        private readonly Dictionary<int, string> _lobbyPeers = new Dictionary<int, string>();

        public IReadOnlyDictionary<int, string> LobbyPeers => _lobbyPeers;

        public event Action OnConnected;
        public event Action<RejectReason> OnConnectionRejected;
        public event Action OnDisconnected;
        public event Action<NetworkMessage> OnMessageReceived;

        private void Awake()
        {
            IncomingQueue = new MessageQueue();
            _reconnectHandler = gameObject.AddComponent<ReconnectHandler>();

            // Respaldo por si GameManager aún no lo configuró.
            Application.runInBackground = true;
        }

        private void Update()
        {
            ProcessInboundMessages();

            // Auto-flush: evita depender de llamadas externas para drenar mensajes pendientes.
            if (IsConnected &&
                _pendingWorldGameplay.Count > 0 &&
                NetworkSpawner.Instance != null &&
                NetworkSpawner.IsClientWorldListenerReady)
            {
                FlushPendingWorldStates();
            }

            // Latido UDP independiente de la escena (PingMonitor puede no existir o pausarse con timeScale=0).
            // El host actualiza LastHeartbeat al recibir cualquier datagrama válido del cliente.
            if (IsConnected && _udpClient != null)
            {
                float now = Time.unscaledTime;
                if (now >= _nextKeepAliveUnscaled)
                {
                    // Cada 2 s si ya hay PingMonitor a 1 s; si no hay ping, esto basta para el timeout del host.
                    _nextKeepAliveUnscaled = now + Constants.PingInterval * 2f;
                    Send(new KeepAliveMessage());
                }
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public void Connect(string ip, int port, string playerName)
        {
            if (IsConnected || IsConnecting) return;

            if (!IPAddress.TryParse(ip, out var addr))
            {
                Debug.LogError($"[Client] IP inválida: {ip}");
                IncomingQueue.EnqueueInbound(new ConnectRejectMessage { Reason = RejectReason.None });
                return;
            }

            ServerIP = ip;
            ServerPort = port;
            PlayerName = playerName;
            IsConnecting = true;
            _isRunning = true;
            _cachedVersion = Application.version;
            _nextKeepAliveUnscaled = float.NegativeInfinity;
            IncomingQueue.Clear();
            _pendingWorldGameplay.Clear();
            _lobbyPeers.Clear();

            try
            {
                _udpClient = new UdpClient(0);
                _serverEndPoint = new IPEndPoint(addr, port);

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Client-UDP-Recv"
                };
                _receiveThread.Start();

                var requestMsg = new ConnectRequestMessage
                {
                    PlayerName = PlayerName,
                    GameVersion = _cachedVersion
                };
                byte[] data = PacketSerializer.SerializeUDP(requestMsg);
                lock (_sendLock)
                {
                    _udpClient.Send(data, data.Length, _serverEndPoint);
                }

                Debug.Log($"[Client] CONNECT_REQUEST → {ip}:{port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Error al conectar: {e.Message}");
                ConnectionFailed();
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    UdpClient client = _udpClient;
                    if (client == null) break;

                    byte[] data = client.Receive(ref remote);
                    if (data == null || data.Length < 1) continue;

                    if (!UdpEndpointUtil.AddressesMatch(remote.Address, _serverEndPoint.Address))
                        continue;

                    NetworkMessage msg = PacketSerializer.Deserialize(data);
                    if (msg == null) continue;

                    IncomingQueue.EnqueueInbound(msg);
                }
                catch (SocketException) when (!_isRunning)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (_isRunning)
                        Debug.LogWarning($"[Client] UDP receive: {e.Message}");
                }
            }

            if (_isRunning)
                IncomingQueue.EnqueueInbound(new DisconnectMessage { SenderId = 0 });
        }

        private void ConnectionFailed()
        {
            IsConnecting = false;
            _isRunning = false;
            _pendingWorldGameplay.Clear();
            _lobbyPeers.Clear();
            CleanupUdp();
            IncomingQueue.EnqueueInbound(new ConnectRejectMessage { Reason = RejectReason.None });
        }

        public void Disconnect()
        {
            if (!_isRunning && _udpClient == null) return;

            _reconnectHandler?.StopReconnect();

            _pendingWorldGameplay.Clear();
            _lobbyPeers.Clear();

            _isRunning = false;

            try
            {
                if (_udpClient != null && IsConnected)
                {
                    var dcMsg = new DisconnectMessage { SenderId = LocalPlayerId };
                    byte[] data = PacketSerializer.SerializeUDP(dcMsg);
                    lock (_sendLock)
                    {
                        _udpClient?.Send(data, data.Length, _serverEndPoint);
                    }
                }
            }
            catch { }

            IsConnected = false;
            IsConnecting = false;

            try { _udpClient?.Close(); } catch { }
            _udpClient = null;

            _receiveThread?.Join(800);
            _receiveThread = null;

            Debug.Log("[Client] Desconectado.");
            OnDisconnected?.Invoke();
        }

        private void CleanupUdp()
        {
            try { _udpClient?.Close(); } catch { }
            _udpClient = null;
            _receiveThread?.Join(800);
            _receiveThread = null;
        }

        /// <summary>Llama NetworkSpawner tras suscribirse para aplicar WorldState recibidos durante la carga async.</summary>
        public void FlushPendingWorldStates()
        {
            if (_pendingWorldGameplay.Count == 0) return;
            var batch = _pendingWorldGameplay.ToArray();
            _pendingWorldGameplay.Clear();
            foreach (var m in batch)
                OnMessageReceived?.Invoke(m);
        }

        private bool ShouldBufferWorldStateForLaterGameplay()
        {
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.Role != NetworkRole.Client) return false;
            if (GameManager.Instance.StateMachine == null) return false;
            if (GameManager.Instance.StateMachine.CurrentState != GameState.Playing) return false;
            return NetworkSpawner.Instance == null || !NetworkSpawner.IsClientWorldListenerReady;
        }

        private void RecordLobbyPeerMessages(NetworkMessage msg)
        {
            if (msg is PlayerJoinedMessage j)
            {
                if (j.PlayerId > 0 && j.PlayerId != LocalPlayerId)
                    _lobbyPeers[j.PlayerId] = string.IsNullOrEmpty(j.PlayerName) ? $"Player_{j.PlayerId}" : j.PlayerName;
            }
            else if (msg is PlayerLeftMessage left)
            {
                _lobbyPeers.Remove(left.PlayerId);
            }
        }

        public void Send(NetworkMessage msg)
        {
            if (!IsConnected || _udpClient == null) return;

            msg.SenderId = LocalPlayerId;
            if (msg.Timestamp <= 0f) msg.Timestamp = Time.unscaledTime;

            byte[] data = PacketSerializer.SerializeUDP(msg);

            try
            {
                lock (_sendLock)
                {
                    _udpClient.Send(data, data.Length, _serverEndPoint);
                }
            }
            catch (Exception)
            {
                IncomingQueue.EnqueueInbound(new DisconnectMessage { SenderId = 0 });
            }
        }

        private void ProcessInboundMessages()
        {
            int limit = 100;
            while (limit-- > 0 && IncomingQueue.TryDequeueInbound(out NetworkMessage msg))
            {
                if (msg is ConnectAcceptMessage accept)
                {
                    LocalPlayerId = accept.AssignedPlayerId;
                    ColorIndex = accept.AssignedColorIndex;
                    IsConnected = true;
                    IsConnecting = false;
                    Debug.Log($"[Client] Conectado OK → PlayerId={LocalPlayerId}, ColorIndex={ColorIndex}, KeepAlive={Constants.PingInterval * 2f:0.0}s");
                    OnConnected?.Invoke();
                    continue;
                }

                if (msg is ConnectRejectMessage reject)
                {
                    IsConnecting = false;
                    _isRunning = false;
                    CleanupUdp();
                    OnConnectionRejected?.Invoke(reject.Reason);
                    continue;
                }

                if (msg is DisconnectMessage)
                {
                    _isRunning = false;
                    CleanupUdp();
                    IsConnected = false;
                    IsConnecting = false;
                    OnDisconnected?.Invoke();
                    _reconnectHandler.AttemptReconnect(this);
                    continue;
                }

                if (!IsConnected)
                {
                    if (msg is PlayerJoinedMessage || msg is PlayerLeftMessage)
                        OnMessageReceived?.Invoke(msg);
                    continue;
                }

                if (msg is WorldStateMessage || msg is WorldSnapshotMessage || msg is PlayerStateMessage)
                {
                    if (NetworkSpawner.Instance == null || !NetworkSpawner.IsClientWorldListenerReady || ShouldBufferWorldStateForLaterGameplay())
                    {
                        if (_pendingWorldGameplay.Count >= MaxPendingWorldGameplay)
                            _pendingWorldGameplay.RemoveAt(0);

                        _pendingWorldGameplay.Add(msg);
                        continue;
                    }
                }

                RecordLobbyPeerMessages(msg);
                OnMessageReceived?.Invoke(msg);
            }
        }
    }
}
