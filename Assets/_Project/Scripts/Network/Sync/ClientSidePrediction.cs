using System.Collections.Generic;
using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Sync
{
    /// <summary>
    /// Predicción del lado del cliente.
    /// Permite al jugador local moverse inmediatamente en respuesta a sus inputs
    /// (sin esperar confirmación del host). Si el host devuelve una posición diferente
    /// porque hubo lag o choque, la posición se reconcilia usando el historial.
    /// </summary>
    public class ClientSidePrediction : MonoBehaviour
    {
        public struct InputRecord
        {
            public int SequenceNumber;
            public Vector3 InputVector;
            public float DeltaTime;
            public bool Sprint;
        }

        private readonly List<InputRecord> _pendingInputs = new List<InputRecord>();
        private int _currentSequenceNumber = 0;
        
        [Tooltip("Referencia al controlador local para simular la predicción/reconciliación")]
        private CharacterController _characterController; // Asume el uso de CharacterController
        public float Speed = 5f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            // Es un dummy implementation a la espera del PlayerController real que maneje físicas
        }

        /// <summary>Registra input, genera SequenceNumber y aplica movimiento de forma local</summary>
        public InputRecord ProcessLocalInput(Vector3 input, bool sprint, float deltaTime)
        {
            _currentSequenceNumber++;

            InputRecord record = new InputRecord
            {
                SequenceNumber = _currentSequenceNumber,
                InputVector = input,
                DeltaTime = deltaTime,
                Sprint = sprint
            };

            _pendingInputs.Add(record);
            
            ApplyMovement(record);

            return record;
        }

        /// <summary>Aplica el movimiento a Unity de forma predictiva</summary>
        private void ApplyMovement(InputRecord record)
        {
            if (_characterController != null)
            {
                float currentSpeed = record.Sprint ? Speed * 1.5f : Speed;
                _characterController.Move(record.InputVector * currentSpeed * record.DeltaTime);
            }
        }

        /// <summary>Llamado cuando el host confirma nuestro estado pasado (WorldState UDP)</summary>
        public void Reconcile(Vector3 authoritativePosition, int hostSequenceNumber)
        {
            // Remover inputs ya procesados por el host
            _pendingInputs.RemoveAll(i => i.SequenceNumber <= hostSequenceNumber);

            // Calcular el error / distancia entre lo local previsto y lo real del host
            float distanceError = Vector3.Distance(transform.position, authoritativePosition);

            if (distanceError > Constants.ReconciliationThreshold)
            {
                // Rubber banding necesario.
                // 1. Resetear posición a la autoritativa
                if (_characterController != null) _characterController.enabled = false;
                transform.position = authoritativePosition;
                if (_characterController != null) _characterController.enabled = true;

                // 2. Re-aplicar (replay) todos los inputs locales pendientes sobre la posición validad
                foreach (var pendingInput in _pendingInputs)
                {
                    ApplyMovement(pendingInput);
                }
                
                Debug.LogWarning($"[CSP] Reconciliación aplicada (seq: {hostSequenceNumber}, err: {distanceError:F3})");
            }
        }
    }
}
