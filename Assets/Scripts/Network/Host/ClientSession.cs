using System;
using System.Net;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Host
{

    public class ClientSession
    {
        public int PlayerId { get; private set; }
        public string PlayerName { get; set; }
        public int ColorIndex { get; set; }
        public string IPAddress { get; private set; }

        public IPEndPoint UdpEndPoint { get; set; }

        public bool IsConnected { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public float Ping { get; set; }

        public bool IsAlive { get; set; } = true;
        public int Score { get; set; }
        public int LastProcessedInputSeq { get; set; }

        public UnityEngine.Vector3 LastPosition { get; set; }
        public float LastRotationY { get; set; }


        public bool LastIsMoving { get; set; }
        public bool LastIsRunning { get; set; }
        public bool LastIsPickingUp { get; set; }


        public bool HasReceivedState { get; set; }

        public ClientSession(int playerId, IPEndPoint endPoint)
        {
            PlayerId = playerId;
            UdpEndPoint = endPoint;
            IPAddress = endPoint?.Address.ToString() ?? "";

            IsConnected = true;
            LastHeartbeat = DateTime.UtcNow;
            Ping = 0f;
            HasReceivedState = false;
        }

        public void Close()
        {
            IsConnected = false;
        }

        public override string ToString()
        {
            return $"[Session] Player={PlayerId} Name={PlayerName} IP={IPAddress} Ping={Ping:F0}ms";
        }
    }
}
