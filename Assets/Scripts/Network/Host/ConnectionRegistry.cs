using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using TheLostHill.Network.Shared;

namespace TheLostHill.Network.Host
{

    public class ConnectionRegistry
    {
        private readonly ConcurrentDictionary<int, ClientSession> _sessions
            = new ConcurrentDictionary<int, ClientSession>();

        private int _nextPlayerId = 1; 
        private readonly object _idLock = new object();

   
        private readonly ConcurrentQueue<int> _availableColors = new ConcurrentQueue<int>();
        private const int TotalColors = 8;

        public ConnectionRegistry()
        {

            for (int i = 0; i < TotalColors; i++)
                _availableColors.Enqueue(i);
        }


        public int GeneratePlayerId()
        {
            lock (_idLock)
            {
                return _nextPlayerId++;
            }
        }


        public int AssignColor()
        {
            if (_availableColors.TryDequeue(out int color))
                return color;
            return -1;
        }


        public void ReleaseColor(int colorIndex)
        {
            if (colorIndex >= 0 && colorIndex < TotalColors)
                _availableColors.Enqueue(colorIndex);
        }


        public bool Add(ClientSession session)
        {
            return _sessions.TryAdd(session.PlayerId, session);
        }


        public bool Remove(int playerId, out ClientSession removed)
        {
            bool result = _sessions.TryRemove(playerId, out removed);
            if (result && removed != null)
            {
                ReleaseColor(removed.ColorIndex);
            }
            return result;
        }


        public bool TryGet(int playerId, out ClientSession session)
        {
            return _sessions.TryGetValue(playerId, out session);
        }


        public ClientSession GetByIP(string ip)
        {
            return _sessions.Values.FirstOrDefault(s => s.IPAddress == ip);
        }


        public ClientSession GetByEndPoint(IPEndPoint endPoint)
        {
            if (endPoint == null) return null;
            return _sessions.Values.FirstOrDefault(s =>
                s.UdpEndPoint != null &&
                UdpEndpointUtil.EndPointsMatch(s.UdpEndPoint, endPoint));
        }


        public IReadOnlyCollection<ClientSession> GetAll()
        {
            return _sessions.Values.ToList().AsReadOnly();
        }


        public IReadOnlyCollection<ClientSession> GetConnected()
        {
            return _sessions.Values.Where(s => s.IsConnected).ToList().AsReadOnly();
        }

        public int Count => _sessions.Count;

 
        public int TotalPlayers => _sessions.Count + 1;


        public void Clear()
        {
            foreach (var session in _sessions.Values)
            {
                session.Close();
            }
            _sessions.Clear();

            while (_availableColors.TryDequeue(out _)) { }
            for (int i = 0; i < TotalColors; i++)
                _availableColors.Enqueue(i);
        }
    }
}
