using System;
using System.Net;
using System.Net.Sockets;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Host
{
    /// <summary>
    /// Representa una conexión activa de un cliente al host.
    /// Almacena el socket TCP, el endpoint UDP, datos del jugador y métricas.
    /// 
    /// El host mantiene una instancia de ClientSession por cada cliente conectado.
    /// Es manipulado desde hilos de red (envío/recepción) y el main thread (lectura de datos).
    /// </summary>
    public class ClientSession
    {
        // ── Identificación ──────────────────────────────────────
        public int PlayerId { get; private set; }
        public string PlayerName { get; set; }
        public int ColorIndex { get; set; }
        public string IPAddress { get; private set; }

        // ── Red ─────────────────────────────────────────────────
        public TcpClient TcpConnection { get; private set; }
        public NetworkStream TcpStream { get; private set; }
        public IPEndPoint UdpEndPoint { get; set; }

        // ── Estado ──────────────────────────────────────────────
        public bool IsConnected { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public float Ping { get; set; } // RTT en ms

        // ── Gameplay ────────────────────────────────────────────
        public bool IsAlive { get; set; } = true;
        public int Score { get; set; }
        public int LastProcessedInputSeq { get; set; }

        public UnityEngine.Vector3 LastPosition { get; set; }
        public float LastRotationY { get; set; }

        // ── Buffer TCP ──────────────────────────────────────────
        /// <summary>Buffer para acumular datos TCP parciales (framing).</summary>
        public byte[] ReceiveBuffer { get; private set; }
        public int BufferOffset { get; set; }

        // ═════════════════════════════════════════════════════════

        public ClientSession(int playerId, TcpClient tcpClient)
        {
            PlayerId = playerId;
            TcpConnection = tcpClient;
            TcpStream = tcpClient.GetStream();
            IPAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();

            IsConnected = true;
            LastHeartbeat = DateTime.UtcNow;
            Ping = 0f;

            ReceiveBuffer = new byte[Core.Constants.TcpBufferSize];
            BufferOffset = 0;
        }

        /// <summary>
        /// Envía datos TCP a este cliente de forma síncrona.
        /// Llamado desde el hilo de envío.
        /// </summary>
        public bool SendTcp(byte[] data)
        {
            try
            {
                if (!IsConnected || TcpStream == null || !TcpConnection.Connected)
                    return false;

                TcpStream.Write(data, 0, data.Length);
                TcpStream.Flush();
                return true;
            }
            catch (Exception)
            {
                IsConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Cierra la conexión TCP de forma limpia.
        /// </summary>
        public void Close()
        {
            IsConnected = false;
            try
            {
                TcpStream?.Close();
                TcpConnection?.Close();
            }
            catch (Exception)
            {
                // Ignorar errores al cerrar — ya estamos desconectando
            }
        }

        public override string ToString()
        {
            return $"[Session] Player={PlayerId} Name={PlayerName} IP={IPAddress} Ping={Ping:F0}ms";
        }
    }
}
