using UnityEngine;
using UnityEngine.AI;
using TheLostHill.Core;

namespace TheLostHill.Gameplay.Monster
{
    public enum MonsterState : byte
    {
        Idle = 0,
        Patrol = 1,
        Chase = 2
    }

    /// <summary>
    /// IA principal del monstruo. Utiliza NavMesh para el pathfinding.
    /// Solo interactúa el rol de Host; los clientes solo renderizan el
    /// monstruo vía MonsterNetworkSync.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private MonsterSenses _senses;
        
        public MonsterState CurrentState { get; private set; } = MonsterState.Idle;

        [Header("Settings")]
        public float PatrolSpeed = 3.5f;
        public float ChaseSpeed = 6.0f;

        private Transform _targetPlayer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _senses = GetComponent<MonsterSenses>();
        }

        private void Update()
        {
            // La IA solo se ejecuta en el Host
            if (GameManager.Instance.Role != NetworkRole.Host)
            {
                if (_agent.enabled) _agent.enabled = false;
                return;
            }

            if (!_agent.enabled) _agent.enabled = true;

            switch (CurrentState)
            {
                case MonsterState.Idle:
                    // TODO: Esperar inicio de partida o spawn delay
                    break;

                case MonsterState.Patrol:
                    _agent.speed = PatrolSpeed;
                    // TODO: Mover a waypoints aleatorios apoyados por NavMesh
                    // Si _senses.GetClosestVisiblePlayer() != null -> Chase
                    break;

                case MonsterState.Chase:
                    _agent.speed = ChaseSpeed;
                    
                    _targetPlayer = _senses.GetClosestVisiblePlayer();
                    if (_targetPlayer != null)
                    {
                        _agent.SetDestination(_targetPlayer.position);
                    }
                    else
                    {
                        // Perdió al jugador -> Patrol
                        SetState(MonsterState.Patrol);
                    }
                    break;
            }
        }

        public void SetState(MonsterState newState)
        {
            CurrentState = newState;
        }
    }
}
