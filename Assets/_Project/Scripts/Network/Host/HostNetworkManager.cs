using System;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using TheLostHill.Core;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Host
{
    /// <summary>
    /// Servidor player-host con un único socket UDP (control + gameplay en el mismo canal).
    /// </summary>
    public class HostNetworkManager : MonoBehaviour
    {
        [Header("Network Config")]
        [SerializeField] private int _listenPort = Constants.DefaultNetworkPort;

        public ConnectionRegistry Registry { get; private set; }
        public BanList BanList { get; private set; }
        public MessageQueue IncomingQueue { get; private set; }

        private UdpClient _udpClient;
        private Thread _udpReceiveThread;
        private volatile bool _isRunning;
        private readonly object _sendLock = new object();

        public bool IsHosting => _isRunning;
        public int ListenPort => _listenPort;

        public event Action<int, string> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action<NetworkMessage> OnMessageReceived;

        private int _worldTick;
        private float _lastBroadcastTime;

        // Estado/log periódico de conexiones (debug operativo)
        private float _nextStatusLogUnscaled;

        private void Awake()
        {
            Registry = new ConnectionRegistry();
            BanList = new BanList();
            IncomingQueue = new MessageQueue();
        }

        private void Update()
        {
            if (!_isRunning) return;

            ProcessInboundMessages();
            CheckClientTimeouts();
            LogConnectionsStatusPeriodically();
        }

        private void OnDestroy()
        {
            StopHost();
        }

        private void OnApplicationQuit()
        {
            StopHost();
        }

        /// <summary>Inicia el servidor UDP en el puerto indicado.</summary>
        public void StartHost(int port = -1)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[Host] Ya está ejecutándose.");
                return;
            }

            if (port > 0) _listenPort = port;

            try
            {
                _udpClient = new UdpClient(_listenPort);
                
                // Evitar excepción 10054 (ConnectionReset) en Windows cuando un cliente se desconecta a la fuerza
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _udpClient.Client.IOControl((System.Net.Sockets.IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }

                _isRunning = true;

                _udpReceiveThread = new Thread(UdpReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Host-UDP"
                };
                _udpReceiveThread.Start();

                Debug.Log($"[Host] Servidor UDP iniciado en puerto {_listenPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Error al iniciar: {e.Message}");
                StopHost();
            }
        }

        public void StopHost()
        {
            if (!_isRunning && _udpClient == null) return;

            try
            {
                var disconnectMsg = new DisconnectMessage { SenderId = 0 };
                Broadcast(disconnectMsg);
            }
            catch { }

            _isRunning = false;

            try { _udpClient?.Close(); } catch { }
            _udpClient = null;

            _udpReceiveThread?.Join(1500);
            _udpReceiveThread = null;

            Registry.Clear();
            IncomingQueue.Clear();

            Debug.Log("[Host] Servidor detenido.");
        }

        private void UdpReceiveLoop()
        {
            Debug.Log("[Host] UDP receive loop iniciado.");

            while (_isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref remoteEP);

                    if (data.Length < 1) continue;

                    IPEndPoint remoteNorm = UdpEndpointUtil.NormalizeEndPoint(remoteEP);

                    NetworkMessage msg = PacketSerializer.Deserialize(data);
                    if (msg == null) continue;

                    ClientSession session = Registry.GetByEndPoint(remoteNorm);

                    if (session == null && msg.SenderId > 0)
                    {
                        if (Registry.TryGet(msg.SenderId, out var migrated) &&
                            migrated.UdpEndPoint != null &&
                            UdpEndpointUtil.AddressesMatch(migrated.UdpEndPoint.Address, remoteNorm.Address))
                        {
                            migrated.UdpEndPoint = remoteNorm;
                            session = migrated;
                        }
                    }

                    if (session == null)
                    {
                        if (msg is ConnectRequestMessage request)
                            HandleConnectRequest(remoteNorm, request);
                        continue;
                    }

                    session.UdpEndPoint = remoteNorm;
                    session.LastHeartbeat = DateTime.UtcNow;
                    msg.SenderId = session.PlayerId;

                    if (msg is PlayerStateMessage posMsg)
                    {
                        posMsg.PlayerId = session.PlayerId;
                        posMsg.SenderId = session.PlayerId;
                        session.LastPosition = new Vector3(posMsg.PosX, posMsg.PosY, posMsg.PosZ);
                        session.LastRotationY = posMsg.RotY;
                        session.LastIsMoving = posMsg.IsMoving;
                        session.LastIsRunning = posMsg.IsRunning;
                        session.LastIsPickingUp = posMsg.IsPickingUp;
                        session.IsAlive = posMsg.IsAlive;
                        session.HasReceivedState = true; // NUEVO
                    }

                    IncomingQueue.EnqueueInbound(msg);
                }
                catch (SocketException se) when (!_isRunning || se.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset)
                {
                    if (!_isRunning) break;
                    // Ignore 10054 on Windows if somehow IOControl failed or isn't used
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (_isRunning)
                        Debug.LogError($"[Host] Error en UDP receive: {e.Message}");
                }
            }
        }

        private void HandleConnectRequest(IPEndPoint remoteEP, ConnectRequestMessage request)
        {
            IPEndPoint ep = UdpEndpointUtil.NormalizeEndPoint(remoteEP) ?? remoteEP;
            string clientIP = ep.Address.ToString();

            if (BanList.IsBanned(clientIP))
            {
                Debug.Log($"[Host] Conexión rechazada (baneado): {clientIP}");
                SendToEndPoint(ep, new ConnectRejectMessage { SenderId = 0, Reason = RejectReason.Banned });
                return;
            }

            if (Registry.TotalPlayers >= Constants.MaxPlayers)
            {
                Debug.Log("[Host] Conexión rechazada (lleno)");
                SendToEndPoint(ep, new ConnectRejectMessage { SenderId = 0, Reason = RejectReason.ServerFull });
                return;
            }

            int playerId = Registry.GeneratePlayerId();
            int colorIdx = Registry.AssignColor();

            var session = new ClientSession(playerId, ep)
            {
                PlayerName = request.PlayerName ?? $"Player_{playerId}",
                ColorIndex = colorIdx >= 0 ? colorIdx : 0
            };

            Registry.Add(session);

            var acceptMsg = new ConnectAcceptMessage
            {
                SenderId = 0,
                AssignedPlayerId = playerId,
                AssignedColorIndex = session.ColorIndex
            };
            SendToEndPoint(ep, acceptMsg);

            var joinedMsg = new PlayerJoinedMessage
            {
                SenderId = 0,
                PlayerId = playerId,
                PlayerName = session.PlayerName,
                ColorIndex = session.ColorIndex
            };
            Broadcast(joinedMsg, excludePlayerId: playerId);

            // El nuevo cliente no recibe el broadcast anterior; reenviarle PlayerJoined de cada sesión ya existente.
            foreach (var existing in Registry.GetAll())
            {
                if (existing.PlayerId == playerId) continue;
                var prior = new PlayerJoinedMessage
                {
                    SenderId = 0,
                    PlayerId = existing.PlayerId,
                    PlayerName = existing.PlayerName,
                    ColorIndex = existing.ColorIndex
                };
                SendToEndPoint(ep, prior);
            }

            IncomingQueue.EnqueueInbound(new PlayerJoinedMessage
            {
                PlayerId = playerId,
                PlayerName = session.PlayerName,
                ColorIndex = session.ColorIndex
            });

            Debug.Log($"[Host] Cliente conectado: {session}");
        }

        private void ProcessInboundMessages()
        {
            int processed = 0;
            const int maxPerFrame = 100;

            while (processed < maxPerFrame && IncomingQueue.TryDequeueInbound(out NetworkMessage msg))
            {
                processed++;

                switch (msg)
                {
                    case PingRequestMessage ping:
                        HandlePingRequest(ping);
                        break;

                    case DisconnectMessage dc:
                        if (dc.SenderId > 0)
                            HandleClientDisconnect(dc.SenderId);
                        break;

                    case PlayerJoinedMessage joined:
                        OnClientConnected?.Invoke(joined.PlayerId, joined.PlayerName);
                        OnMessageReceived?.Invoke(joined);
                        break;

                    case KeepAliveMessage _:
                        break;

                    case PlayerStateMessage ps:
                        // Host actualiza su vista y relaya al resto.
                        OnMessageReceived?.Invoke(ps);
                        Broadcast(ps, excludePlayerId: ps.SenderId);
                        break;

                    default:
                        OnMessageReceived?.Invoke(msg);
                        break;
                }
            }
        }

        private void HandlePingRequest(PingRequestMessage ping)
        {
            var response = new PingResponseMessage
            {
                SenderId = 0,
                ClientTime = ping.ClientTime,
                TargetPlayerId = ping.SenderId
            };
            SendToClient(ping.SenderId, response);
        }

        private void CheckClientTimeouts()
        {
            var now = DateTime.UtcNow;
            var disconnected = new List<int>();

            foreach (var session in Registry.GetAll())
            {
                double elapsed = (now - session.LastHeartbeat).TotalSeconds;
                if (elapsed > Constants.DisconnectTimeout)
                {
                    Debug.Log($"[Host] Timeout de P{session.PlayerId} ({elapsed:F1}s)");
                    disconnected.Add(session.PlayerId);
                }
            }

            foreach (int playerId in disconnected)
                HandleClientDisconnect(playerId);
        }

        private void SendToEndPoint(IPEndPoint ep, NetworkMessage msg)
        {
            if (ep == null || _udpClient == null || !_isRunning) return;

            try
            {
                byte[] data = PacketSerializer.SerializeUDP(msg);
                lock (_sendLock)
                {
                    _udpClient.Send(data, data.Length, ep);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Host] Error UDP a endpoint {ep}: {e.Message}");
            }
        }

        public void SendToClient(int playerId, NetworkMessage msg)
        {
            if (!Registry.TryGet(playerId, out ClientSession session) || session.UdpEndPoint == null)
                return;

            SendToEndPoint(session.UdpEndPoint, msg);
        }

        public void Broadcast(NetworkMessage msg, int excludePlayerId = -1)
        {
            if (_udpClient == null || !_isRunning) return;

            byte[] data = PacketSerializer.SerializeUDP(msg);

            foreach (var session in Registry.GetConnected())
            {
                if (session.PlayerId == excludePlayerId) continue;
                if (session.UdpEndPoint == null) continue;

                try
                {
                    lock (_sendLock)
                    {
                        _udpClient.Send(data, data.Length, session.UdpEndPoint);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Host] Error UDP broadcast a P{session.PlayerId}: {e.Message}");
                }
            }
        }

        public void BroadcastWorldState(WorldStateMessage worldState)
        {
            worldState.Tick = _worldTick++;
            worldState.SenderId = 0;
            Broadcast(worldState);
        }

        private void HandleClientDisconnect(int playerId)
        {
            if (Registry.Remove(playerId, out ClientSession session))
            {
                session.Close();

                var leftMsg = new PlayerLeftMessage
                {
                    SenderId = 0,
                    PlayerId = playerId
                };
                Broadcast(leftMsg);

                Debug.Log($"[Host] Cliente desconectado: P{playerId} ({session.PlayerName})");

                OnClientDisconnected?.Invoke(playerId);
            }
        }

        public void KickPlayer(int playerId, string reason = "Kicked by host")
        {
            if (Registry.TryGet(playerId, out ClientSession session))
            {
                var kickMsg = new KickPlayerMessage
                {
                    SenderId = 0,
                    TargetPlayerId = playerId,
                    Reason = reason
                };
                SendToClient(playerId, kickMsg);
                HandleClientDisconnect(playerId);
                Debug.Log($"[Host] Jugador expulsado: P{playerId} — {reason}");
            }
        }

        public void BanPlayer(int playerId, string reason = "Banned by host")
        {
            if (Registry.TryGet(playerId, out ClientSession session))
            {
                BanList.Ban(session.IPAddress);
                KickPlayer(playerId, reason);
                Debug.Log($"[Host] Jugador baneado: P{playerId} IP={session.IPAddress}");
            }
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void LogConnectionsStatusPeriodically()
        {
            if (Time.unscaledTime < _nextStatusLogUnscaled) return;
            _nextStatusLogUnscaled = Time.unscaledTime + 5f;

            var sessions = Registry.GetAll();
            if (sessions.Count == 0)
            {
                Debug.Log("[Host] Estado: sin clientes conectados.");
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var s in sessions)
            {
                double idle = (now - s.LastHeartbeat).TotalSeconds;
                Debug.Log($"[Host] Estado P{s.PlayerId} ({s.PlayerName}) idle={idle:F1}s / timeout={Constants.DisconnectTimeout:F0}s");
            }
        }
    }
}
