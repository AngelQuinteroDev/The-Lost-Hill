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

            // Loop de Red del Servidor (Host)
            if (Time.time - _lastTickTime >= _tickRate)
            {
                _lastTickTime = Time.time;
                BroadcastWorldUpdate();
            }
        }

        private void BroadcastWorldUpdate()
        {
            if (NetworkSpawner.Instance == null) return;

            var playerDict = NetworkSpawner.Instance.ActivePlayers;
            PlayerSnapshot[] snapshots = new PlayerSnapshot[playerDict.Count];
            int i = 0;

            foreach (var kvp in playerDict)
            {
                var player = kvp.Value;
                int colorIdx = 0;
                
                // Intentar obtener el color asignado desde el Registry del Host
                if (GameManager.Instance.HostManager.Registry.TryGet(kvp.Key, out var session))
                {
                    colorIdx = session.ColorIndex;
                }

                snapshots[i++] = new PlayerSnapshot
                {
                    PlayerId = kvp.Key,
                    PosX = player.transform.position.x,
                    PosY = player.transform.position.y,
                    PosZ = player.transform.position.z,
                    RotY = player.transform.rotation.eulerAngles.y,
                    ColorIndex = colorIdx,
                    IsAlive = true
                };
            }

            // 1. Recopilar datos de todos los jugadores
            WorldStateMessage worldState = new WorldStateMessage
            {
                Players = snapshots
            };

            // 2. Enviar por UDP (Baja latencia)
            GameManager.Instance.HostManager.BroadcastWorldState(worldState);
        }
    }
}
