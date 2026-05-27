using UnityEngine;

namespace ECS_Sound.AudioConfiguration
{
    /// <summary>
    /// Optional scene-side accessor. The asset is the source of truth; this exists only to let
    /// you wire an asset reference in the inspector instead of relying on Resources.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollisionSoundConfigurationHub : MonoBehaviour
    {
        [SerializeField] private CollisionSoundConfigurationAsset asset;

        private static CollisionSoundConfigurationHub instance;
        private static CollisionSoundConfigurationAsset cachedAsset;

        public CollisionSoundConfigurationAsset Asset => asset;

        public static CollisionSoundConfigurationHub GetInstance()
        {
            if (instance != null) return instance;
            var hubs = FindObjectsOfType<CollisionSoundConfigurationHub>();
            if (hubs.Length == 0) return null;
            if (hubs.Length > 1)
                Debug.LogError($"Multiple CollisionSoundConfigurationHub instances ({hubs.Length}).");
            instance = hubs[0];
            return instance;
        }

        public static CollisionSoundConfigurationAsset LoadAsset()
        {
            if (cachedAsset != null) return cachedAsset;

            var hub = GetInstance();
            if (hub != null && hub.asset != null)
            {
                cachedAsset = hub.asset;
                return cachedAsset;
            }

            cachedAsset = Resources.Load<CollisionSoundConfigurationAsset>(
                CollisionSoundConfigurationAsset.DefaultResourcePath);

            if (cachedAsset == null)
            {
                Debug.LogError(
                    $"CollisionSoundConfigurationAsset not found. Assign one on a " +
                    $"{nameof(CollisionSoundConfigurationHub)}, or place it at " +
                    $"Resources/{CollisionSoundConfigurationAsset.DefaultResourcePath}.asset");
            }
            return cachedAsset;
        }

        protected void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Duplicate CollisionSoundConfigurationHub; auto-destroyed.");
                Destroy(this);
                return;
            }
            instance = this;
            if (asset != null) cachedAsset = asset;
        }

        protected void OnDestroy()
        {
            if (instance == this) instance = null;
            // Don't clear cachedAsset — survives scene unloads.
        }
    }
}
