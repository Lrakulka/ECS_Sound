using UnityEngine;

namespace ECS_Sound.AudioConfiguration
{
    /// <summary>
    /// Per-kind playback settings. Touch and slide each get their own; everything else
    /// (3D spatial, rolloff, priority, etc.) stays unified on the configuration.
    /// </summary>
    [System.Serializable]
    public struct ClipPlaybackSettings
    {
        [Range(0, 1)] public float volume;
        [Range(-3, 3)] public float pitch;

        public static ClipPlaybackSettings DefaultTouch => new() { volume = 0.7f, pitch = 1f };
        public static ClipPlaybackSettings DefaultSlide => new() { volume = 0.2f, pitch = 1f };
    }

    public class CollisionSoundConfiguration : ScriptableObject
    {
        [Header("Sample variation: one clip is randomly picked per impact.")]
        [Tooltip("Touch (impact) clips. At least one required.")]
        public AudioClip[] touchClips;
        [Tooltip("Slide (sustained contact) clips. At least one required.")]
        public AudioClip[] slideClips;

        [Space(5)] [Header("Per-kind playback")]
        public ClipPlaybackSettings touch = ClipPlaybackSettings.DefaultTouch;
        public ClipPlaybackSettings slide = ClipPlaybackSettings.DefaultSlide;

        [Space(5)] [Header("Shared configuration")]
        public bool mute;
        public bool bypassEffects;
        public bool bypassListenerEffects;
        public bool bypassReverbZones;

        [Range(0, 256)] public int priority = 128;
        [Range(-1, 1)] public float stereoPan;
        [Range(0, 1)] public float spatialBlend;
        [Range(0, 1.1f)] public float reverbZoneMix = 1;

        [Header("3D Sound Settings")]
        [Range(0, 5)] public float dopplerLevel = 1;
        [Range(0, 360)] public float spread;
        public AudioRolloffMode volumeRolloff = AudioRolloffMode.Logarithmic;
        public float minDistance = 1;
        public float maxDistance = 500;

        // ---- Stable identity --------------------------------------------------------------------
        // FNV-1a hash of a per-asset GUID. Deterministic across runtimes/platforms; safe to embed
        // in baked entity data.

        [Header("Identity (auto-assigned, do not edit)")]
        [SerializeField, HideInInspector] private string guid;
        [SerializeField, HideInInspector] private int cachedId;

        public string Guid => guid;

        public int Id
        {
            get
            {
                if (cachedId == 0) RebuildCachedId();
                return cachedId;
            }
        }

        private void RebuildCachedId()
        {
            if (string.IsNullOrEmpty(guid))
            {
#if UNITY_EDITOR
                EnsureGuid();
#else
                Debug.LogError($"CollisionSoundConfiguration '{name}' has no GUID. Re-import in editor.", this);
                return;
#endif
            }
            cachedId = StableHash32(guid);
        }

        public static int StableHash32(string s)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint h = offset;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= prime;
            }
            return h == 0 ? 1 : unchecked((int)h);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureGuid();
            RebuildCachedId();

            ValidateClipArray(touchClips, nameof(touchClips));
            ValidateClipArray(slideClips, nameof(slideClips));
        }

        private void EnsureGuid()
        {
            if (string.IsNullOrEmpty(guid))
            {
                guid = System.Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private void ValidateClipArray(AudioClip[] clips, string fieldName)
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"'{name}'.{fieldName} is empty. Add at least one clip.", this);
                return;
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    Debug.LogWarning($"'{name}'.{fieldName}[{i}] is null.", this);
            }
        }
#endif
    }
}
