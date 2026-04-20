using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Sync
{
    public class ExtrapolationSystem : MonoBehaviour
    {
        private Vector3 _lastVelocity;
        private Vector3 _lastPosition;
        private float _lastUpdateTime;

        private bool _isExtrapolating;
        private float _extrapolationStartTime;

        public bool IsActive = true;

        public void UpdateState(Vector3 newPosition)
        {
            if (_lastUpdateTime > 0)
            {
                float deltaTime = Time.time - _lastUpdateTime;
                if (deltaTime > 0)
                {
                    _lastVelocity = (newPosition - _lastPosition) / deltaTime;
                }
            }
            
            _lastPosition = newPosition;
            _lastUpdateTime = Time.time;
            _isExtrapolating = false;
        }

        public void StartExtrapolation()
        {
            _isExtrapolating = true;
            _extrapolationStartTime = Time.time;
        }

        private void Update()
        {
            if (!IsActive || !_isExtrapolating) return;

            float extrapolationTime = Time.time - _extrapolationStartTime;
            

            if (extrapolationTime < Constants.ExtrapolationMaxTime)
            {
                transform.position = _lastPosition + _lastVelocity * extrapolationTime;
            }
        }
    }
}
