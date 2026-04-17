using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace TheLostHill.Network.Host
{
    /// <summary>
    /// Registro thread-safe de todas las sesiones de clientes conectados.
    /// Genera PlayerIDs únicos y proporciona lookups por ID, IP y EndPoint.
    /// </summary>
    public class ConnectionRegistry
    {
        private readonly ConcurrentDictionary<int, ClientSession> _sessions
            = new ConcurrentDictionary<int, ClientSession>();

        private int _nextPlayerId = 1; // 0 reservado para el host
        private readonly object _idLock = new object();

        // ── Colores disponibles para asignar a jugadores ────────
        private readonly ConcurrentQueue<int> _availableColors = new ConcurrentQueue<int>();
        private const int TotalColors = 8;

        public ConnectionRegistry()
        {
            // Inicializar colores disponibles
            for (int i = 0; i < TotalColors; i++)
                _availableColors.Enqueue(i);
        }

        // ═════════════════════════════════════════════════════════
        //  GESTIÓN DE SESIONES
        // ═════════════════════════════════════════════════════════

        /// <summary>Genera un nuevo PlayerID único y atómico.</summary>
        public int GeneratePlayerId()
        {
            lock (_idLock)
            {
                return _nextPlayerId++;
            }
        }

        /// <summary>Asigna un color único al jugador. Devuelve -1 si no hay colores.</summary>
        public int AssignColor()
        {
            if (_availableColors.TryDequeue(out int color))
                return color;
            return -1;
        }

        /// <summary>Devuelve un color al pool de disponibles.</summary>
        public void ReleaseColor(int colorIndex)
        {
            if (colorIndex >= 0 && colorIndex < TotalColors)
                _availableColors.Enqueue(colorIndex);
        }

        /// <summary>Registra una nueva sesión de cliente.</summary>
        public bool Add(ClientSession session)
        {
            return _sessions.TryAdd(session.PlayerId, session);
        }

        /// <summary>Elimina una sesión por PlayerID.</summary>
        public bool Remove(int playerId, out ClientSession removed)
        {
            bool result = _sessions.TryRemove(playerId, out removed);
            if (result && removed != null)
            {
                ReleaseColor(removed.ColorIndex);
            }
            return result;
        }

        /// <summary>Obtiene una sesión por PlayerID.</summary>
        public bool TryGet(int playerId, out ClientSession session)
        {
            return _sessions.TryGetValue(playerId, out session);
        }

        /// <summary>Obtiene una sesión por dirección IP.</summary>
        public ClientSession GetByIP(string ip)
        {
            return _sessions.Values.FirstOrDefault(s => s.IPAddress == ip);
        }

        /// <summary>Obtiene una sesión por UDP EndPoint.</summary>
        public ClientSession GetByUdpEndPoint(IPEndPoint endPoint)
        {
            return _sessions.Values.FirstOrDefault(s =>
                s.UdpEndPoint != null &&
                s.UdpEndPoint.Address.Equals(endPoint.Address) &&
                s.UdpEndPoint.Port == endPoint.Port);
        }

        // ═════════════════════════════════════════════════════════
        //  CONSULTAS
        // ═════════════════════════════════════════════════════════

        /// <summary>Todas las sesiones activas.</summary>
        public IReadOnlyCollection<ClientSession> GetAll()
        {
            return _sessions.Values.ToList().AsReadOnly();
        }

        /// <summary>Todas las sesiones conectadas.</summary>
        public IReadOnlyCollection<ClientSession> GetConnected()
        {
            return _sessions.Values.Where(s => s.IsConnected).ToList().AsReadOnly();
        }

        /// <summary>Número de clientes conectados (no incluye al host).</summary>
        public int Count => _sessions.Count;

        /// <summary>Número total de jugadores (clientes + host).</summary>
        public int TotalPlayers => _sessions.Count + 1;

        // ═════════════════════════════════════════════════════════
        //  LIMPIEZA
        // ═════════════════════════════════════════════════════════

        /// <summary>Cierra y elimina todas las sesiones.</summary>
        public void Clear()
        {
            foreach (var session in _sessions.Values)
            {
                session.Close();
            }
            _sessions.Clear();

            // Reiniciar colores
            while (_availableColors.TryDequeue(out _)) { }
            for (int i = 0; i < TotalColors; i++)
                _availableColors.Enqueue(i);
        }
    }
}
