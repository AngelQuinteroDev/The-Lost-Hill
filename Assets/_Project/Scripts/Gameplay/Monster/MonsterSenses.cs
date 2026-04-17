using UnityEngine;

namespace TheLostHill.Gameplay.Monster
{
    /// <summary>
    /// Subsistema de la IA del monstruo para detectar jugadores cercanos (visión / ruido).
    /// </summary>
    public class MonsterSenses : MonoBehaviour
    {
        public float VisionRadius = 20f;
        public float VisionAngle = 120f;
        public LayerMask ObstacleMask;
        public LayerMask PlayerMask;

        public Transform GetClosestVisiblePlayer()
        {
            // TODO: OverlapSphere sobre PlayerMask
            // Iterar sobre jugadores en rango
            // Comprobar ángulo
            // Raycast a los jugadores para asegurar que no hay paredes en ObstacleMask
            // Devolver el transform más cercano, o null si ninguno cumple
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawSphere(transform.position, VisionRadius);
        }
    }
}
