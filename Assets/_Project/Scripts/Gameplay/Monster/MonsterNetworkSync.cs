using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Monster
{
    /// <summary>
    /// Sincroniza al monstruo por la red.
    /// Host: Lee la posición de NavMeshAgent y actualiza el WorldState.
    /// Client: Lee datos de InterpolationSystem y aplica el estado (animaciones).
    /// </summary>
    public class MonsterNetworkSync : MonoBehaviour
    {
        private MonsterAI _ai;
        private float _lastSendTime;
        
        private void Awake()
        {
            _ai = GetComponent<MonsterAI>();
        }

        /// <summary>
        /// Lee el estado actual para que el HostNetworkManager lo meta en el WorldState.
        /// </summary>
        public Network.Shared.MonsterSnapshot GetSnapshot()
        {
            return new Network.Shared.MonsterSnapshot
            {
                PosX = transform.position.x,
                PosY = transform.position.y,
                PosZ = transform.position.z,
                RotY = transform.rotation.eulerAngles.y,
                State = (byte)(_ai != null ? _ai.CurrentState : MonsterState.Idle)
            };
        }

        /// <summary>
        /// Aplicado por el cliente cuando recibe un estado en la red.
        /// </summary>
        public void ApplyServerState(byte stateValue)
        {
            // MonsterState newState = (MonsterState)stateValue;
            // TODO: En base a esto disparar triggers de animación IsChasing, IsIdle
        }
    }
}
