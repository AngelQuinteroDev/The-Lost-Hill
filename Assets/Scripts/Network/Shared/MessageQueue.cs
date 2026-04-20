using System.Collections.Concurrent;

namespace TheLostHill.Network.Shared
{
    public class MessageQueue
    {
        private readonly ConcurrentQueue<NetworkMessage> _inbound = new ConcurrentQueue<NetworkMessage>();
        private readonly ConcurrentQueue<NetworkMessage> _outbound = new ConcurrentQueue<NetworkMessage>();

        public void EnqueueInbound(NetworkMessage msg) => _inbound.Enqueue(msg);

        public bool TryDequeueInbound(out NetworkMessage msg) => _inbound.TryDequeue(out msg);

        public int InboundCount => _inbound.Count;

        public void EnqueueOutbound(NetworkMessage msg) => _outbound.Enqueue(msg);

        public bool TryDequeueOutbound(out NetworkMessage msg) => _outbound.TryDequeue(out msg);

        public int OutboundCount => _outbound.Count;

        public void Clear()
        {
            while (_inbound.TryDequeue(out _)) { }
            while (_outbound.TryDequeue(out _)) { }
        }
    }
}