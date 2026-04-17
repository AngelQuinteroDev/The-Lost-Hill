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
    /// Manager principal de red del host. Abre un servidor TCP para control
    /// y un socket UDP para datos de alta frecuencia (posiciones).
    /// 
    /// Diseño de hilos:
    /// - Hilo TCP Accept: acepta nuevas conexiones
    /// - Hilo TCP Receive (uno por cliente): lee mensajes de cada cliente
    /// - Hilo UDP Receive: recibe datagramas UDP
    /// - Main Thread (Update): procesa MessageQueue inbound, genera outbound
    /// 
    /// NUNCA llames APIs de Unity (transform, GameObject, etc.) desde hilos de red.
    /// Usa MessageQueue como puente.
    /// </summary>
    public class HostNetworkManager : MonoBehaviour
    {
        // ── Configuración ───────────────────────────────────────
        [Header("Network Config")]
        [SerializeField] private int _tcpPort = Constants.DefaultTcpPort;
        [SerializeField] private int _udpPort = Constants.DefaultUdpPort;

        // ── Componentes internos ────────────────────────────────
        public ConnectionRegistry Registry { get; private set; }
        public BanList BanList { get; private set; }
        public MessageQueue IncomingQueue { get; private set; }

        // ── Sockets ─────────────────────────────────────────────
        private TcpListener _tcpListener;
        private UdpClient _udpClient;

        // ── Hilos ───────────────────────────────────────────────
        private Thread _acceptThread;
        private Thread _udpReceiveThread;
        private readonly Dictionary<int, Thread> _clientReceiveThreads = new Dictionary<int, Thread>();
        private volatile bool _isRunning;

        // ── Estado ──────────────────────────────────────────────
        public bool IsHosting => _isRunning;
        public int TcpPort => _tcpPort;
        public int UdpPort => _udpPort;

        // ── Eventos (disparados en main thread) ─────────────────
        public event Action<int, string> OnClientConnected;     // playerId, name
        public event Action<int> OnClientDisconnected;          // playerId
        public event Action<NetworkMessage> OnMessageReceived;  // cualquier mensaje procesado

        // ── Tick para world state ───────────────────────────────
        private int _worldTick;
        private float _lastBroadcastTime;

        // ═════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════

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
        }

        private void OnDestroy()
        {
            StopHost();
        }

        private void OnApplicationQuit()
        {
            StopHost();
        }

        // ═════════════════════════════════════════════════════════
        //  START / STOP HOST
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Inicia el servidor TCP y UDP. Comienza a aceptar conexiones.
        /// </summary>
        public void StartHost(int tcpPort = -1, int udpPort = -1)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[Host] Ya está ejecutándose.");
                return;
            }

            if (tcpPort > 0) _tcpPort = tcpPort;
            if (udpPort > 0) _udpPort = udpPort;

            try
            {
                // Iniciar TCP Listener
                _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
                _tcpListener.Start();

                // Iniciar UDP Socket
                _udpClient = new UdpClient(_udpPort);

                _isRunning = true;

                // Hilo para aceptar conexiones TCP
                _acceptThread = new Thread(AcceptLoop)
                {
                    IsBackground = true,
                    Name = "Host-TCP-Accept"
                };
                _acceptThread.Start();

                // Hilo para recibir UDP
                _udpReceiveThread = new Thread(UdpReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Host-UDP-Receive"
                };
                _udpReceiveThread.Start();

                Debug.Log($"[Host] Servidor iniciado → TCP:{_tcpPort} UDP:{_udpPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Error al iniciar: {e.Message}");
                StopHost();
            }
        }

        /// <summary>
        /// Detiene el servidor. Cierra todas las conexiones.
        /// </summary>
        public void StopHost()
        {
            if (!_isRunning) return;

            _isRunning = false;

            // Notificar a todos los clientes
            var disconnectMsg = new DisconnectMessage { SenderId = 0 };
            BroadcastTCP(disconnectMsg);

            // Cerrar todas las sesiones
            Registry.Clear();

            // Cerrar sockets
            try { _tcpListener?.Stop(); } catch { }
            try { _udpClient?.Close(); } catch { }

            // Esperar hilos (con timeout)
            _acceptThread?.Join(1000);
            _udpReceiveThread?.Join(1000);

            foreach (var thread in _clientReceiveThreads.Values)
                thread?.Join(500);

            _clientReceiveThreads.Clear();
            IncomingQueue.Clear();

            Debug.Log("[Host] Servidor detenido.");
        }

        // ═════════════════════════════════════════════════════════
        //  TCP ACCEPT LOOP (hilo separado)
        // ═════════════════════════════════════════════════════════

        private void AcceptLoop()
        {
            Debug.Log("[Host] Accept loop iniciado.");

            while (_isRunning)
            {
                try
                {
                    if (!_tcpListener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    TcpClient tcpClient = _tcpListener.AcceptTcpClient();
                    tcpClient.NoDelay = true; // Deshabilitar Nagle para baja latencia

                    string clientIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();

                    // Verificar ban
                    if (BanList.IsBanned(clientIP))
                    {
                        Debug.Log($"[Host] Conexión rechazada (baneado): {clientIP}");
                        RejectConnection(tcpClient, RejectReason.Banned);
                        continue;
                    }

                    // Verificar capacidad
                    if (Registry.TotalPlayers >= Constants.MaxPlayers)
                    {
                        Debug.Log($"[Host] Conexión rechazada (lleno): {clientIP}");
                        RejectConnection(tcpClient, RejectReason.ServerFull);
                        continue;
                    }

                    // Aceptar conexión: esperar CONNECT_REQUEST
                    HandleNewConnection(tcpClient);
                }
                catch (SocketException) when (!_isRunning)
                {
                    break; // Servidor detenido
                }
                catch (Exception e)
                {
                    if (_isRunning)
                        Debug.LogError($"[Host] Error en accept loop: {e.Message}");
                }
            }
        }

        private void HandleNewConnection(TcpClient tcpClient)
        {
            try
            {
                // Leer el primer mensaje (CONNECT_REQUEST)
                NetworkStream stream = tcpClient.GetStream();
                stream.ReadTimeout = (int)(Constants.ConnectionTimeout * 1000);

                byte[] headerBuf = new byte[Constants.LengthHeaderSize];
                int read = ReadFull(stream, headerBuf, 0, Constants.LengthHeaderSize);
                if (read < Constants.LengthHeaderSize) { tcpClient.Close(); return; }

                int payloadLen = BitConverter.ToInt32(headerBuf, 0);
                if (payloadLen <= 0 || payloadLen > Constants.MaxPacketSize) { tcpClient.Close(); return; }

                byte[] payload = new byte[payloadLen];
                read = ReadFull(stream, payload, 0, payloadLen);
                if (read < payloadLen) { tcpClient.Close(); return; }

                NetworkMessage msg = PacketSerializer.Deserialize(payload);
                if (msg is not ConnectRequestMessage request)
                {
                    tcpClient.Close();
                    return;
                }

                // Crear sesión
                int playerId = Registry.GeneratePlayerId();
                int colorIdx = Registry.AssignColor();

                var session = new ClientSession(playerId, tcpClient)
                {
                    PlayerName = request.PlayerName ?? $"Player_{playerId}",
                    ColorIndex = colorIdx >= 0 ? colorIdx : 0
                };

                Registry.Add(session);

                // Restaurar timeout normal
                stream.ReadTimeout = Timeout.Infinite;

                // Enviar CONNECT_ACCEPT
                var acceptMsg = new ConnectAcceptMessage
                {
                    SenderId = 0,
                    AssignedPlayerId = playerId,
                    AssignedColorIndex = session.ColorIndex
                };
                byte[] acceptData = PacketSerializer.SerializeTCP(acceptMsg);
                session.SendTcp(acceptData);

                // Notificar a todos los demás: PLAYER_JOINED
                var joinedMsg = new PlayerJoinedMessage
                {
                    SenderId = 0,
                    PlayerId = playerId,
                    PlayerName = session.PlayerName,
                    ColorIndex = session.ColorIndex
                };
                BroadcastTCP(joinedMsg, excludePlayerId: playerId);

                // Encolar para que el main thread notifique
                IncomingQueue.EnqueueInbound(new PlayerJoinedMessage
                {
                    PlayerId = playerId,
                    PlayerName = session.PlayerName,
                    ColorIndex = session.ColorIndex
                });

                // Iniciar hilo de recepción para este cliente
                Thread receiveThread = new Thread(() => ClientReceiveLoop(session))
                {
                    IsBackground = true,
                    Name = $"Host-TCP-Recv-P{playerId}"
                };
                lock (_clientReceiveThreads)
                {
                    _clientReceiveThreads[playerId] = receiveThread;
                }
                receiveThread.Start();

                Debug.Log($"[Host] Cliente conectado: {session}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Error en handshake: {e.Message}");
                try { tcpClient.Close(); } catch { }
            }
        }

        private void RejectConnection(TcpClient tcpClient, RejectReason reason)
        {
            try
            {
                var rejectMsg = new ConnectRejectMessage
                {
                    SenderId = 0,
                    Reason = reason
                };
                byte[] data = PacketSerializer.SerializeTCP(rejectMsg);
                NetworkStream stream = tcpClient.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch { }
            finally
            {
                try { tcpClient.Close(); } catch { }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  TCP RECEIVE LOOP (un hilo por cliente)
        // ═════════════════════════════════════════════════════════

        private void ClientReceiveLoop(ClientSession session)
        {
            NetworkStream stream = session.TcpStream;

            while (_isRunning && session.IsConnected)
            {
                try
                {
                    // Leer length header
                    byte[] headerBuf = new byte[Constants.LengthHeaderSize];
                    int read = ReadFull(stream, headerBuf, 0, Constants.LengthHeaderSize);
                    if (read < Constants.LengthHeaderSize)
                    {
                        // Conexión cerrada
                        break;
                    }

                    int payloadLen = BitConverter.ToInt32(headerBuf, 0);
                    if (payloadLen <= 0 || payloadLen > Constants.MaxPacketSize)
                    {
                        Debug.LogWarning($"[Host] Paquete inválido de P{session.PlayerId}: len={payloadLen}");
                        continue;
                    }

                    // Leer payload
                    byte[] payload = new byte[payloadLen];
                    read = ReadFull(stream, payload, 0, payloadLen);
                    if (read < payloadLen) break;

                    // Deserializar y encolar
                    NetworkMessage msg = PacketSerializer.Deserialize(payload);
                    if (msg != null)
                    {
                        msg.SenderId = session.PlayerId;
                        session.LastHeartbeat = DateTime.UtcNow;
                        IncomingQueue.EnqueueInbound(msg);
                    }
                }
                catch (System.IO.IOException)
                {
                    break; // Conexión perdida
                }
                catch (ObjectDisposedException)
                {
                    break; // Socket cerrado
                }
                catch (Exception e)
                {
                    if (_isRunning && session.IsConnected)
                        Debug.LogError($"[Host] Error recibiendo de P{session.PlayerId}: {e.Message}");
                    break;
                }
            }

            // Cliente desconectado - Encolar para procesar en Main Thread
            IncomingQueue.EnqueueInbound(new DisconnectMessage { SenderId = session.PlayerId });
        }

        // ═════════════════════════════════════════════════════════
        //  UDP RECEIVE LOOP (hilo separado)
        // ═════════════════════════════════════════════════════════

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

                    NetworkMessage msg = PacketSerializer.Deserialize(data);
                    if (msg == null) continue;

                    // Asociar el endpoint UDP con la sesión (para saber a dónde enviar respuesta UDP)
                    ClientSession session = Registry.GetByIP(remoteEP.Address.ToString());
                    if (session != null)
                    {
                        session.UdpEndPoint = remoteEP;
                        msg.SenderId = session.PlayerId;
                        session.LastHeartbeat = DateTime.UtcNow;
                        IncomingQueue.EnqueueInbound(msg);
                    }
                }
                catch (SocketException) when (!_isRunning)
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

        // ═════════════════════════════════════════════════════════
        //  PROCESAMIENTO MAIN THREAD
        // ═════════════════════════════════════════════════════════

        private void ProcessInboundMessages()
        {
            int processed = 0;
            const int maxPerFrame = 100; // Limitar para evitar bloquear el frame

            while (processed < maxPerFrame && IncomingQueue.TryDequeueInbound(out NetworkMessage msg))
            {
                processed++;

                switch (msg)
                {
                    case PingRequestMessage ping:
                        HandlePingRequest(ping);
                        break;

                    case DisconnectMessage dc:
                        HandleClientDisconnect(dc.SenderId);
                        break;

                    case PlayerJoinedMessage joined:
                        OnClientConnected?.Invoke(joined.PlayerId, joined.PlayerName);
                        OnMessageReceived?.Invoke(joined);
                        break;

                    case PlayerStateMessage posMsg:
                        if (Registry.TryGet(posMsg.SenderId, out ClientSession session))
                        {
                            // Actualizar posición y rotación en la sesión para el broadcast
                            session.LastPosition = new Vector3(posMsg.PosX, posMsg.PosY, posMsg.PosZ);
                            session.LastRotationY = posMsg.RotY;
                        }
                        break;

                    case KeepAliveMessage _:
                        // Solo actualiza LastHeartbeat (ya se hizo en el receive loop)
                        break;

                    default:
                        // Delegar al GameManager u otros listeners
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
            SendTCP(ping.SenderId, response);
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
            {
                IncomingQueue.EnqueueInbound(new DisconnectMessage { SenderId = playerId });
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ENVÍO DE MENSAJES
        // ═════════════════════════════════════════════════════════

        /// <summary>Envía un mensaje TCP a un cliente específico.</summary>
        public void SendTCP(int playerId, NetworkMessage msg)
        {
            if (Registry.TryGet(playerId, out ClientSession session))
            {
                byte[] data = PacketSerializer.SerializeTCP(msg);
                if (!session.SendTcp(data))
                {
                    HandleClientDisconnect(playerId);
                }
            }
        }

        /// <summary>Envía un mensaje TCP a todos los clientes (excepto uno opcional).</summary>
        public void BroadcastTCP(NetworkMessage msg, int excludePlayerId = -1)
        {
            byte[] data = PacketSerializer.SerializeTCP(msg);
            var disconnected = new List<int>();

            foreach (var session in Registry.GetConnected())
            {
                if (session.PlayerId == excludePlayerId) continue;

                if (!session.SendTcp(data))
                {
                    disconnected.Add(session.PlayerId);
                }
            }

            foreach (int id in disconnected)
                HandleClientDisconnect(id);
        }

        /// <summary>Envía un mensaje UDP a un cliente específico.</summary>
        public void SendUDP(int playerId, NetworkMessage msg)
        {
            if (Registry.TryGet(playerId, out ClientSession session) && session.UdpEndPoint != null)
            {
                try
                {
                    byte[] data = PacketSerializer.SerializeUDP(msg);
                    _udpClient.Send(data, data.Length, session.UdpEndPoint);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Host] Error UDP a P{playerId}: {e.Message}");
                }
            }
        }

        /// <summary>Envía un mensaje UDP a todos los clientes.</summary>
        public void BroadcastUDP(NetworkMessage msg)
        {
            byte[] data = PacketSerializer.SerializeUDP(msg);

            foreach (var session in Registry.GetConnected())
            {
                if (session.UdpEndPoint == null) continue;

                try
                {
                    _udpClient.Send(data, data.Length, session.UdpEndPoint);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Host] Error UDP broadcast a P{session.PlayerId}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Envía el WorldState a todos los clientes por UDP.
        /// Llamar desde GameManager cada NetworkSendRate.
        /// </summary>
        public void BroadcastWorldState(WorldStateMessage worldState)
        {
            worldState.Tick = _worldTick++;
            worldState.SenderId = 0;
            BroadcastUDP(worldState);
        }

        // ═════════════════════════════════════════════════════════
        //  DESCONEXIÓN
        // ═════════════════════════════════════════════════════════

        private void HandleClientDisconnect(int playerId)
        {
            if (Registry.Remove(playerId, out ClientSession session))
            {
                session.Close();

                lock (_clientReceiveThreads)
                {
                    _clientReceiveThreads.Remove(playerId);
                }

                // Notificar a los demás
                var leftMsg = new PlayerLeftMessage
                {
                    SenderId = 0,
                    PlayerId = playerId
                };
                BroadcastTCP(leftMsg);

                Debug.Log($"[Host] Cliente desconectado: P{playerId} ({session.PlayerName})");

                // Evento para main thread
                OnClientDisconnected?.Invoke(playerId);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ADMIN
        // ═════════════════════════════════════════════════════════

        /// <summary>Expulsa un jugador.</summary>
        public void KickPlayer(int playerId, string reason = "Kicked by host")
        {
            if (Registry.TryGet(playerId, out ClientSession session))
            {
                // Notificar al jugador que va a ser expulsado
                var kickMsg = new KickPlayerMessage
                {
                    SenderId = 0,
                    TargetPlayerId = playerId,
                    Reason = reason
                };
                SendTCP(playerId, kickMsg);

                // Dar un pequeño delay para que el mensaje llegue, luego desconectar
                HandleClientDisconnect(playerId);
                Debug.Log($"[Host] Jugador expulsado: P{playerId} — {reason}");
            }
        }

        /// <summary>Banea permanentemente la IP de un jugador y lo expulsa.</summary>
        public void BanPlayer(int playerId, string reason = "Banned by host")
        {
            if (Registry.TryGet(playerId, out ClientSession session))
            {
                BanList.Ban(session.IPAddress);
                KickPlayer(playerId, reason);
                Debug.Log($"[Host] Jugador baneado: P{playerId} IP={session.IPAddress}");
            }
        }

        // ═════════════════════════════════════════════════════════
        //  UTILIDADES
        // ═════════════════════════════════════════════════════════

        /// <summary>Lee exactamente 'count' bytes de un NetworkStream. Retorna bytes leídos.</summary>
        private static int ReadFull(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0) return totalRead; // Conexión cerrada
                totalRead += read;
            }
            return totalRead;
        }

        /// <summary>Obtiene la IP local del host.</summary>
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
    }
}
