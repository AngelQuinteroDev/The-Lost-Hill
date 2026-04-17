using System.Collections.Concurrent;

namespace TheLostHill.Network.Shared
{
    /// <summary>
    /// Cola de mensajes thread-safe que sirve de puente entre los hilos de red
    /// y el hilo principal de Unity.
    /// 
    /// - Los hilos de red escriben en Inbound (mensajes recibidos).
    /// - El hilo principal lee Inbound en Update() y procesa los mensajes.
    /// - El hilo principal escribe en Outbound (mensajes a enviar).
    /// - Los hilos de red leen Outbound y envían por TCP/UDP.
    /// 
    /// Usa ConcurrentQueue internamente, que es lock-free para operaciones
    /// individuales de Enqueue/TryDequeue.
    /// </summary>
    public class MessageQueue
    {
        private readonly ConcurrentQueue<NetworkMessage> _inbound = new ConcurrentQueue<NetworkMessage>();
        private readonly ConcurrentQueue<NetworkMessage> _outbound = new ConcurrentQueue<NetworkMessage>();

        // ── Inbound (Red → Main Thread) ─────────────────────────

        /// <summary>Llamado desde hilos de red. Encola un mensaje recibido.</summary>
        public void EnqueueInbound(NetworkMessage msg) => _inbound.Enqueue(msg);

        /// <summary>Llamado desde el main thread (Update). Intenta sacar un mensaje.</summary>
        public bool TryDequeueInbound(out NetworkMessage msg) => _inbound.TryDequeue(out msg);

        /// <summary>Cantidad aproximada de mensajes inbound pendientes.</summary>
        public int InboundCount => _inbound.Count;

        // ── Outbound (Main Thread → Red) ────────────────────────

        /// <summary>Llamado desde el main thread. Encola un mensaje para enviar.</summary>
        public void EnqueueOutbound(NetworkMessage msg) => _outbound.Enqueue(msg);

        /// <summary>Llamado desde hilos de red. Intenta sacar un mensaje para enviar.</summary>
        public bool TryDequeueOutbound(out NetworkMessage msg) => _outbound.TryDequeue(out msg);

        /// <summary>Cantidad aproximada de mensajes outbound pendientes.</summary>
        public int OutboundCount => _outbound.Count;

        // ── Utilidades ──────────────────────────────────────────

        /// <summary>Vacía ambas colas. Llamar al desconectar/resetear.</summary>
        public void Clear()
        {
            while (_inbound.TryDequeue(out _)) { }
            while (_outbound.TryDequeue(out _)) { }
        }
    }
}
