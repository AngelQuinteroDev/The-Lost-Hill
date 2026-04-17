using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Client
{
    /// <summary>
    /// Maneja la conexión del cliente al host.
    /// Utiliza TCP para mensajes de control y UDP para baja latencia.
    /// Se conecta, realiza el handshake y provee interfaces para enviar datos.
    /// </summary>
    public class ClientNetworkHandler : MonoBehaviour
    {
        [Header("Client Config")]
        public string ServerIP = "127.0.0.1";
        public int TcpPort = Constants.DefaultTcpPort;
        public int UdpPort = Constants.DefaultUdpPort;
        public string PlayerName = "Player";

        // ── Componentes internos ────────────────────────────────
        public MessageQueue IncomingQueue { get; private set; }
        private ReconnectHandler _reconnectHandler;

        // ── Estado ──────────────────────────────────────────────
        public int LocalPlayerId { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsConnecting { get; private set; }

        // ── Hilos y Sockets ─────────────────────────────────────
        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;
        private Thread _tcpReceiveThread;
        private volatile bool _isRunning;
        private string _cachedVersion; // Para evitar errores de hilos con Application.version

        // ── Eventos ─────────────────────────────────────────────
        public event Action OnConnected;
        public event Action<RejectReason> OnConnectionRejected;
        public event Action OnDisconnected;
        public event Action<NetworkMessage> OnMessageReceived;

        // ═════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════

        private void Awake()
        {
            IncomingQueue = new MessageQueue();
            _reconnectHandler = gameObject.AddComponent<ReconnectHandler>();
        }

        private void Update()
        {
            if (!_isRunning) return;
            ProcessInboundMessages();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        // ═════════════════════════════════════════════════════════
        //  CONEXIÓN
        // ═════════════════════════════════════════════════════════

        public void Connect(string ip, int tcpPort, string playerName)
        {
            if (IsConnected || IsConnecting) return;

            ServerIP = ip;
            TcpPort = tcpPort;
            PlayerName = playerName;
            IsConnecting = true;
            _isRunning = true;
            _cachedVersion = Application.version; // Capturamos en el Main Thread
            IncomingQueue.Clear();

            Thread connectThread = new Thread(ConnectLoop)
            {
                IsBackground = true,
                Name = "Client-Connect"
            };
            connectThread.Start();
        }

        private void ConnectLoop()
        {
            try
            {
                Debug.Log($"[Client] Conectando a {ServerIP}:{TcpPort}...");

                _tcpClient = new TcpClient();
                // Conexión síncrona con timeout
                var result = _tcpClient.BeginConnect(ServerIP, TcpPort, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(Constants.ConnectionTimeout));

                if (!success || !_tcpClient.Connected)
                {
                    throw new Exception("Timeout de conexión");
                }

                _tcpClient.EndConnect(result);
                _tcpClient.NoDelay = true;
                _tcpStream = _tcpClient.GetStream();

                // 2. Enviar CONNECT_REQUEST
                var requestMsg = new ConnectRequestMessage
                {
                    PlayerName = PlayerName,
                    GameVersion = _cachedVersion
                };

                byte[] data = PacketSerializer.SerializeTCP(requestMsg);
                _tcpStream.Write(data, 0, data.Length);
                _tcpStream.Flush();

                // 3. Esperar CONNECT_ACCEPT o CONNECT_REJECT
                _tcpStream.ReadTimeout = (int)(Constants.ConnectionTimeout * 1000);
                byte[] headerBuf = new byte[Constants.LengthHeaderSize];
                int read = ReadFull(_tcpStream, headerBuf, 0, Constants.LengthHeaderSize);
                if (read < Constants.LengthHeaderSize) throw new Exception("Conexión cerrada por el host");

                int payloadLen = BitConverter.ToInt32(headerBuf, 0);
                byte[] payload = new byte[payloadLen];
                read = ReadFull(_tcpStream, payload, 0, payloadLen);
                if (read < payloadLen) throw new Exception("Conexión cerrada leyendo payload");

                NetworkMessage responseMsg = PacketSerializer.Deserialize(payload);
                _tcpStream.ReadTimeout = Timeout.Infinite;

                if (responseMsg is ConnectRejectMessage reject)
                {
                    Debug.LogWarning($"[Client] Conexión rechazada: {reject.Reason}");
                    RejectConnection(reject.Reason);
                    return;
                }
                else if (responseMsg is ConnectAcceptMessage accept)
                {
                    LocalPlayerId = accept.AssignedPlayerId;
                    ColorIndex = accept.AssignedColorIndex;
                    Debug.Log($"[Client] Conectado. PlayerId: {LocalPlayerId}");

                    IncomingQueue.EnqueueInbound(accept);
                }
                else
                {
                    throw new Exception("Respuesta inesperada");
                }

                IsConnected = true;
                IsConnecting = false;

                // 5. Iniciar hilos de recepción
                _tcpReceiveThread = new Thread(TcpReceiveLoop) { IsBackground = true, Name = "Client-TCP-Recv" };
                _tcpReceiveThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Error al conectar: {e.Message}");
                ConnectionFailed();
            }
        }

        private void RejectConnection(RejectReason reason)
        {
            IsConnecting = false;
            _isRunning = false;
            _tcpClient?.Close();
            IncomingQueue.EnqueueInbound(new ConnectRejectMessage { Reason = reason });
        }

        private void ConnectionFailed()
        {
            IsConnecting = false;
            _isRunning = false;
            _tcpClient?.Close();
            IncomingQueue.EnqueueInbound(new ConnectRejectMessage { Reason = RejectReason.None });
        }

        public void Disconnect()
        {
            if (!_isRunning) return;

            _isRunning = false;
            IsConnected = false;
            IsConnecting = false;

            // Enviar mensaje de desconexión antes de cerrar
            if (_tcpClient != null && _tcpClient.Connected)
            {
                try
                {
                    var dcMsg = new DisconnectMessage { SenderId = LocalPlayerId };
                    SendTCP(dcMsg);
                }
                catch { }
            }

            try { _tcpStream?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            
            _reconnectHandler?.StopReconnect();
            
            _tcpReceiveThread?.Join(500);

            Debug.Log("[Client] Desconectado.");
            OnDisconnected?.Invoke();
        }

        // ═════════════════════════════════════════════════════════
        //  RECEIVE LOOPS (Hilos Separados)
        // ═════════════════════════════════════════════════════════

        private void TcpReceiveLoop()
        {
            while (_isRunning && IsConnected)
            {
                try
                {
                    byte[] headerBuf = new byte[Constants.LengthHeaderSize];
                    int read = ReadFull(_tcpStream, headerBuf, 0, Constants.LengthHeaderSize);
                    if (read < Constants.LengthHeaderSize) break; // Host desconectó

                    int payloadLen = BitConverter.ToInt32(headerBuf, 0);
                    if (payloadLen <= 0 || payloadLen > Constants.MaxPacketSize) continue;

                    byte[] payload = new byte[payloadLen];
                    read = ReadFull(_tcpStream, payload, 0, payloadLen);
                    if (read < payloadLen) break;

                    NetworkMessage msg = PacketSerializer.Deserialize(payload);
                    if (msg != null)
                    {
                        IncomingQueue.EnqueueInbound(msg);
                    }
                }
                catch (System.IO.IOException)
                {
                    Debug.LogWarning("[Client] System.IO.IOException: Conexión cerrada por el host u OS.");
                    if (_isRunning) break;
                }
                catch (System.ObjectDisposedException)
                {
                    Debug.LogWarning("[Client] System.ObjectDisposedException: El stream TCP ya está cerrado.");
                    if (_isRunning) break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Client] Error crítico en TcpReceiveLoop: {e.GetType().Name} - {e.Message}\n{e.StackTrace}");
                    if (_isRunning) break;
                }
            }

            if (_isRunning)
            {
                Debug.LogWarning("[Client] Conexión TCP perdida. (Fin del bucle Receive)");
                IncomingQueue.EnqueueInbound(new DisconnectMessage { SenderId = 0 }); // Signal loss
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ENVÍO DE MENSAJES (Main Thread)
        // ═════════════════════════════════════════════════════════

        public void SendTCP(NetworkMessage msg)
        {
            if (!IsConnected || _tcpClient == null || !_tcpClient.Connected) return;

            msg.SenderId = LocalPlayerId;
            byte[] data = PacketSerializer.SerializeTCP(msg);

            try
            {
                _tcpStream.Write(data, 0, data.Length);
                _tcpStream.Flush();
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PROCESAMIENTO (Main Thread)
        // ═════════════════════════════════════════════════════════

        private void ProcessInboundMessages()
        {
            int limit = 100;
            while (limit-- > 0 && IncomingQueue.TryDequeueInbound(out NetworkMessage msg))
            {
                if (msg is ConnectAcceptMessage)
                {
                    OnConnected?.Invoke();
                }
                else if (msg is ConnectRejectMessage reject)
                {
                    OnConnectionRejected?.Invoke(reject.Reason);
                }
                else if (msg is DisconnectMessage)
                {
                    // Si recibimos esto de forma local en la cola, el servidor nos cerró o hubo error TCP
                    Disconnect();
                    _reconnectHandler.AttemptReconnect(this);
                }
                else
                {
                    OnMessageReceived?.Invoke(msg);
                }
            }
        }

        private static int ReadFull(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }
    }
}
