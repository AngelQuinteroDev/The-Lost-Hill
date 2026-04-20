using System.Collections.Generic;
using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Sync
{

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
        private CharacterController _characterController; 
        public float Speed = 5f;
        public float Gravity = 20f;
        private float _verticalVelocity = 0f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
           
        }

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

        private void ApplyMovement(InputRecord record)
        {
            if (_characterController != null)
            {

                if (_characterController.isGrounded)
                {
                    _verticalVelocity = -0.5f; 
                }
                else
                {
                    _verticalVelocity -= Gravity * record.DeltaTime;
                }

                float currentSpeed = record.Sprint ? Speed * 1.5f : Speed;
                Vector3 move = record.InputVector * currentSpeed;
                move.y = _verticalVelocity;

                _characterController.Move(move * record.DeltaTime);
            }
        }


        public void Reconcile(Vector3 authoritativePosition, int hostSequenceNumber)
        {

            _pendingInputs.RemoveAll(i => i.SequenceNumber <= hostSequenceNumber);


            float distanceError = Vector3.Distance(transform.position, authoritativePosition);

            if (distanceError > Constants.ReconciliationThreshold)
            {

                if (_characterController != null) _characterController.enabled = false;
                transform.position = authoritativePosition;
                if (_characterController != null) _characterController.enabled = true;


                foreach (var pendingInput in _pendingInputs)
                {
                    ApplyMovement(pendingInput);
                }
                
                Debug.LogWarning($"[CSP] Reconciliación aplicada (seq: {hostSequenceNumber}, err: {distanceError:F3})");
            }
        }
    }
}
