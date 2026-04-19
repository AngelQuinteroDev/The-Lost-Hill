using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Sync
{
    /// <summary>
    /// Interpola la posición de entidades remotas (jugadores, monstruo).
    /// Mantiene un buffer de snapshots y renderiza con 'InterpolationDelay' de retraso.
    /// 
    /// FIX vs versión original:
    ///   · El buffer ahora es una lista ordenada simple (no circular con headIndex),
    ///     eliminando el bug donde los índices from/to se calculaban en orden inverso.
    ///   · AddSnapshot descarta paquetes out-of-order por timestamp.
    ///   · Clear() limpia correctamente antes de recibir WorldSnapshot inicial.
    /// </summary>
    public class InterpolationSystem : MonoBehaviour
    {
        private struct Snapshot
        {
            public float      Time;
            public Vector3    Position;
            public Quaternion Rotation;
        }

        // Buffer ordenado por tiempo. Tamaño máximo = SnapshotBufferSize.
        private readonly Snapshot[] _buf = new Snapshot[Constants.SnapshotBufferSize];
        private int _count = 0;

        public bool IsActive = true;

        // ════════════════════════════════════════════════════════
        private void Update()
        {
            if (!IsActive || _count < 2) return;

            float renderTime = Time.time - Constants.InterpolationDelay;

            // Buscar dos snapshots contiguos que rodeen a renderTime
            // _buf[0] es el más antiguo, _buf[_count-1] el más nuevo
            for (int i = 0; i < _count - 1; i++)
            {
                if (_buf[i].Time <= renderTime && _buf[i + 1].Time >= renderTime)
                {
                    float span = _buf[i + 1].Time - _buf[i].Time;
                    float t    = (span > 0.0001f) ? (renderTime - _buf[i].Time) / span : 1f;

                    transform.position = Vector3.Lerp(_buf[i].Position, _buf[i + 1].Position, t);
                    transform.rotation = Quaternion.Slerp(_buf[i].Rotation, _buf[i + 1].Rotation, t);
                    return;
                }
            }

            // Si renderTime es más reciente que todos los snapshots, quedarse en el último
            if (renderTime > _buf[_count - 1].Time)
            {
                transform.position = _buf[_count - 1].Position;
                transform.rotation = _buf[_count - 1].Rotation;
            }
        }

        // ════════════════════════════════════════════════════════
        //  API PÚBLICA
        // ════════════════════════════════════════════════════════

        /// <summary>Agrega un snapshot recibido de la red.</summary>
        public void AddSnapshot(Vector3 pos, float rotY, float timestamp)
        {
            // Descartar paquetes atrasados (UDP out-of-order)
            if (_count > 0 && timestamp <= _buf[_count - 1].Time) return;

            var snap = new Snapshot
            {
                Time     = timestamp,
                Position = pos,
                Rotation = Quaternion.Euler(0, rotY, 0)
            };

            if (_count < Constants.SnapshotBufferSize)
            {
                _buf[_count++] = snap;
            }
            else
            {
                // Descartar el más antiguo (shift hacia la izquierda)
                System.Array.Copy(_buf, 1, _buf, 0, _count - 1);
                _buf[_count - 1] = snap;
            }
        }

        /// <summary>Sobrecarga que acepta Vector3 directamente.</summary>
        public void AddSnapshot(Vector3 pos, float rotY) => AddSnapshot(pos, rotY, Time.time);

        /// <summary>Vacía el buffer. Llamar antes de aplicar WorldSnapshot inicial.</summary>
        public void Clear()
        {
            _count = 0;
        }
    }
}