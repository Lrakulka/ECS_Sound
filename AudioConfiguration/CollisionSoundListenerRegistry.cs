using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ECS_Sound.Systems
{
    /// <summary>
    /// Tracks AudioListener positions on a periodic rescan. Cheap enough to run every ~1s;
    /// gameplay code can force an immediate refresh via <see cref="Invalidate"/>.
    /// </summary>
    public class CollisionSoundListenerRegistry
    {
        private readonly System.Collections.Generic.List<Transform> listeners = new();
        private NativeList<float3> positions;
        private float timeUntilNextScan;
        private readonly float rescanInterval;
        private bool dirty = true;

        public CollisionSoundListenerRegistry(float rescanInterval, Allocator allocator)
        {
            this.rescanInterval = rescanInterval;
            positions = new NativeList<float3>(2, allocator);
        }

        public void Invalidate() => dirty = true;

        public NativeList<float3> Positions => positions;

        public void UpdatePositions(float deltaTime)
        {
            timeUntilNextScan -= deltaTime;
            if (dirty || timeUntilNextScan <= 0f)
            {
                Rescan();
                dirty = false;
                timeUntilNextScan = rescanInterval;
            }

            positions.Clear();
            for (int i = 0; i < listeners.Count; i++)
            {
                var t = listeners[i];
                if (t == null)
                {
                    dirty = true; // A listener was destroyed; trigger a full rescan next tick.
                    continue;
                }
                positions.Add(t.position);
            }
        }

        public void Dispose()
        {
            if (positions.IsCreated) positions.Dispose();
            listeners.Clear();
        }

        private void Rescan()
        {
            listeners.Clear();
            var found = Object.FindObjectsOfType<AudioListener>(false);
            for (int i = 0; i < found.Length; i++)
                listeners.Add(found[i].transform);
        }
    }
}
