using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using System.Collections.Generic;

namespace TheLostHill.Gameplay
{
    /// <summary>
    /// Gestiona la sesión de juego activa en el Host.
    /// Controla los ticks de red y genera el WorldStateMessage para todos los clientes.
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        private float _lastTickTime;
        private float _tickRate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _tickRate = Constants.NetworkSendRate;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.Role != NetworkRole.Host) return;
            if (GameManager.Instance.StateMachine.CurrentState != GameState.Playing) return;

            // NetworkSpawner ya es la fuente de WorldState para evitar desdobles.
            if (NetworkSpawner.Instance != null) return;

            if (Time.time - _lastTickTime >= _tickRate)
            {
                _lastTickTime = Time.time;
                BroadcastWorldUpdate();
            }
        }

        private void BroadcastWorldUpdate()
        {
            if (NetworkSpawner.Instance == null || GameManager.Instance == null) return;

            var gm = GameManager.Instance;
            var playerDict = NetworkSpawner.Instance.ActivePlayers;
            PlayerSnapshot[] snapshots = new PlayerSnapshot[playerDict.Count];
            int i = 0;

            foreach (var kvp in playerDict)
            {
                var player = kvp.Value;
                int colorIdx = 0;
                Vector3 pos = player.transform.position;
                float rotY = player.transform.rotation.eulerAngles.y;

                if (gm.HostManager != null && gm.HostManager.Registry.TryGet(kvp.Key, out var session))
                {
                    colorIdx = session.ColorIndex;

                    // Para clientes remotos, usar la última posición confirmada por red
                    if (kvp.Key != gm.LocalPlayerId)
                    {
                        pos = session.LastPosition;
                        rotY = session.LastRotationY;
                    }
                }

                snapshots[i++] = new PlayerSnapshot
                {
                    PlayerId = kvp.Key,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotY = rotY,
                    ColorIndex = colorIdx,
                    IsAlive = true
                };
            }

            gm.HostManager.BroadcastWorldState(new WorldStateMessage
            {
                Players = snapshots
            });
        }
    }
}
