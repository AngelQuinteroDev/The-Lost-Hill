using UnityEngine;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using System.Collections.Generic;

namespace TheLostHill.Network.Sync
{
    /// <summary>
    /// Sistema de interpolación para entidades remotas (jugadores, monstruo).
    /// Mantiene un buffer circular de snapshots pasados.
    /// Renderiza la entidad con un pequeño retraso (~100ms) para interpolar
    /// suavemente entre estados, ocultando los saltos por packet loss y actualización discreta.
    /// </summary>
    public class InterpolationSystem : MonoBehaviour
    {
        private struct StateSnapshot
        {
            public float Timestamp;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly StateSnapshot[] _buffer = new StateSnapshot[Constants.SnapshotBufferSize];
        private int _bufferCount = 0;
        private int _headIndex = 0;

        public bool IsActive = true;

        private void Update()
        {
            if (!IsActive || _bufferCount < 2) return;

            // Buscamos renderizar el estado como era hace 'InterpolationDelay' segundos 
            float renderTime = Time.time - Constants.InterpolationDelay;

            // Encontrar indices de los snapshots que rodean al renderTime
            int idxFrom = -1;
            int idxTo = -1;

            // Empezamos desde el más reciente (headIndex - 1)
            for (int i = 0; i < _bufferCount; i++)
            {
                int currIdx = (_headIndex - 1 - i + Constants.SnapshotBufferSize) % Constants.SnapshotBufferSize;
                int prevIdx = (currIdx - 1 + Constants.SnapshotBufferSize) % Constants.SnapshotBufferSize;
                
                // Si llegamos al final de los datos válidos
                if (i == _bufferCount - 1) break;

                // El time es mayor porque a medida que 'i' avanza vamos más atrás en el tiempo
                if (_buffer[currIdx].Timestamp >= renderTime && _buffer[prevIdx].Timestamp <= renderTime)
                {
                    idxTo = currIdx;
                    idxFrom = prevIdx;
                    break;
                }
            }

            if (idxFrom != -1 && idxTo != -1 && _buffer[idxTo].Timestamp > _buffer[idxFrom].Timestamp)
            {
                StateSnapshot sFrom = _buffer[idxFrom];
                StateSnapshot sTo = _buffer[idxTo];

                float t = (renderTime - sFrom.Timestamp) / (sTo.Timestamp - sFrom.Timestamp);
                
                // Aplicar interpolación
                transform.position = Vector3.Lerp(sFrom.Position, sTo.Position, t);
                transform.rotation = Quaternion.Slerp(sFrom.Rotation, sTo.Rotation, t);
            }
        }

        /// <summary>
        /// Agrega un nuevo snapshot proveniente de la red.
        /// </summary>
        public void AddSnapshot(Vector3 pos, float rotY, float timestamp)
        {
            Quaternion rot = Quaternion.Euler(0, rotY, 0);

            // Evitar snapshots atrasados (out of order UDP)
            if (_bufferCount > 0)
            {
                int newestIdx = (_headIndex - 1 + Constants.SnapshotBufferSize) % Constants.SnapshotBufferSize;
                if (timestamp <= _buffer[newestIdx].Timestamp) return;
            }

            _buffer[_headIndex] = new StateSnapshot
            {
                Timestamp = timestamp, // Usamos un timestamp validado y correlacionado (Time.time local)
                Position = pos,
                Rotation = rot
            };

            _headIndex = (_headIndex + 1) % Constants.SnapshotBufferSize;
            if (_bufferCount < Constants.SnapshotBufferSize) _bufferCount++;
        }

        public void Clear()
        {
            _bufferCount = 0;
            _headIndex = 0;
        }
    }
}
