using Unity.Entities;

namespace ECS_Sound.AudioConfiguration
{
    /// <summary>
    /// Per-configuration blob data, sorted by ConfigurationId for binary search.
    /// Only what the burst-side audio job needs: per-kind pitch for end-time prediction.
    /// Clip selection and actual playback happen on the main thread.
    /// </summary>
    public struct CollisionSoundConfigBlob
    {
        public int ConfigurationId;
        public float TouchPitchAbs;
        public float SlidePitchAbs;
    }

    public struct CollisionSoundBlobData
    {
        public BlobArray<CollisionSoundConfigBlob> Configs;
        public BlobArray<int> ConfigIds;  // Sorted ascending; indexes Configs 1:1.

        public float SoundPriorityRadiusSq;
        public float SoundIgnoreRadiusSq;
        public int AudioSourcePoolSize;
    }

    public struct CollisionSoundBlobReference : IComponentData
    {
        public BlobAssetReference<CollisionSoundBlobData> Blob;
    }

    public static class CollisionSoundBlobLookup
    {
        /// <summary>Binary-search a sorted blob id array. Returns -1 if not found.</summary>
        public static int IndexOf(ref BlobArray<int> ids, int id)
        {
            int lo = 0;
            int hi = ids.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = ids[mid];
                if (v == id) return mid;
                if (v < id) lo = mid + 1;
                else hi = mid - 1;
            }
            return -1;
        }
    }
}
