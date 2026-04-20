using UnityEngine;
using TheLostHill.Core;

namespace TheLostHill.Network.Sync
{

    public class InterpolationSystem : MonoBehaviour
    {
        private struct Snapshot
        {
            public float      Time;
            public Vector3    Position;
            public Quaternion Rotation;
        }

        private readonly Snapshot[] _buf = new Snapshot[Constants.SnapshotBufferSize];
        private int _count = 0;

        public bool IsActive = true;

        private void Update()
        {
            if (!IsActive || _count == 0) return;

            if (_count == 1)
            {
                transform.position = _buf[0].Position;
                transform.rotation = _buf[0].Rotation;
                return;
            }

            float renderTime = Time.time - Constants.InterpolationDelay;


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

            if (renderTime > _buf[_count - 1].Time)
            {
                transform.position = _buf[_count - 1].Position;
                transform.rotation = _buf[_count - 1].Rotation;
            }
        }


        public void AddSnapshot(Vector3 pos, float rotY, float timestamp)
        {
            if (_count > 0)
            {
                float lastTime = _buf[_count - 1].Time;


                if (timestamp + 0.0001f < lastTime) return;


                if (timestamp <= lastTime)
                    timestamp = lastTime + 0.0001f;
            }

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

                System.Array.Copy(_buf, 1, _buf, 0, _count - 1);
                _buf[_count - 1] = snap;
            }
        }


        public void AddSnapshot(Vector3 pos, float rotY) => AddSnapshot(pos, rotY, Time.time);


        public void Clear()
        {
            _count = 0;
        }
    }
}