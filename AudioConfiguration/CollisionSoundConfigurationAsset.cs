using System.Collections.Generic;
using UnityEngine;

namespace ECS_Sound.AudioConfiguration
{
    /// <summary>
    /// ScriptableObject that owns every <see cref="CollisionSoundConfiguration"/> used by the
    /// collision-sound system, plus runtime tuning constants. The asset is the single source of
    /// truth — there is no scene-side data.
    ///
    /// Populate via the editor menu "ECS Sound > Collect Configurations Into Asset".
    /// </summary>
    [CreateAssetMenu(fileName = "CollisionSoundConfigurationAsset",
        menuName = "Scriptable Objects/Collision Sound Configuration Asset",
        order = 0)]
    public class CollisionSoundConfigurationAsset : ScriptableObject
    {
        public const string DefaultResourcePath = "CollisionSoundConfigurationAsset";

        [Header("Configurations")]
        [Tooltip("All CollisionSoundConfiguration assets used by the runtime. Populate via menu.")]
        [SerializeField] private List<CollisionSoundConfiguration> configurationList = new();

        [Header("Distance culling (squared world units)")]
        [SerializeField] private float soundPriorityRadiusSq = 100f;
        [SerializeField] private float soundIgnoreRadiusSq = 900f;

        [Header("Audio source pool")]
        [SerializeField, Min(1)] private int audioSourcePoolSize = 64;

        [Header("Listener discovery")]
        [SerializeField, Min(0.05f)] private float listenerRescanInterval = 1.0f;

        [Header("Slide management")]
        [Tooltip("If a slide interaction hasn't been updated in this many seconds, " +
                 "stop its audio source. Matches CollisionSoundSystemUtils.TOUCH_THRESHOLD_TIME.")]
        [SerializeField, Min(0.01f)] private float slideStaleTime = 0.1f;
        [Tooltip("Don't stop a slide source if it has less than this many seconds remaining " +
                 "(let it finish naturally to avoid clicks).")]
        [SerializeField, Min(0f)] private float slideStopGraceTime = 0.15f;

        [Header("Perceptual polish")]
        [Tooltip("Per-impact pitch randomization, fractional (0.05 = ±5%).")]
        [SerializeField, Range(0f, 0.5f)] private float pitchJitter = 0.05f;
        [Tooltip("Per-impact volume randomization, fractional (0.10 = ±10%).")]
        [SerializeField, Range(0f, 0.5f)] private float volumeJitter = 0.10f;
        [Tooltip("Heavy impacts get lower pitch, light impacts get higher pitch. " +
                 "0.10 = ±10% pitch over the impulse range.")]
        [SerializeField, Range(0f, 0.5f)] private float velocityPitchRange = 0.10f;
        [Tooltip("Apply sqrt() to impulse-derived volume scale (more natural perceived loudness).")]
        [SerializeField] private bool useSqrtVolumeCurve = true;

        // ---- Public read-only API ----

        public IReadOnlyList<CollisionSoundConfiguration> ConfigurationList => configurationList;
        public float SoundPriorityRadiusSq => soundPriorityRadiusSq;
        public float SoundIgnoreRadiusSq => soundIgnoreRadiusSq;
        public int AudioSourcePoolSize => audioSourcePoolSize;
        public float ListenerRescanInterval => listenerRescanInterval;
        public float SlideStaleTime => slideStaleTime;
        public float SlideStopGraceTime => slideStopGraceTime;
        public float PitchJitter => pitchJitter;
        public float VolumeJitter => volumeJitter;
        public float VelocityPitchRange => velocityPitchRange;
        public bool UseSqrtVolumeCurve => useSqrtVolumeCurve;

        private Dictionary<int, CollisionSoundConfiguration> configurationsMap;

        public IReadOnlyDictionary<int, CollisionSoundConfiguration> Configurations
        {
            get { EnsureBuilt(); return configurationsMap; }
        }

        public CollisionSoundConfiguration GetConfiguration(int configurationId)
        {
            EnsureBuilt();
            return configurationsMap[configurationId];
        }

        public bool TryGetConfiguration(int configurationId, out CollisionSoundConfiguration configuration)
        {
            EnsureBuilt();
            return configurationsMap.TryGetValue(configurationId, out configuration);
        }

#if UNITY_EDITOR
        public bool EditorAddConfiguration(CollisionSoundConfiguration configuration)
        {
            if (configuration == null) return false;
            if (configurationList.Contains(configuration)) return false;
            configurationList.Add(configuration);
            configurationsMap = null;
            UnityEditor.EditorUtility.SetDirty(this);
            return true;
        }

        public void EditorClearConfigurations()
        {
            configurationList.Clear();
            configurationsMap = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnEnable() => configurationsMap = null;

        private void EnsureBuilt()
        {
            if (configurationsMap != null) return;

            configurationsMap = new Dictionary<int, CollisionSoundConfiguration>(configurationList.Count);
            foreach (var configuration in configurationList)
            {
                if (configuration == null)
                {
                    Debug.LogError($"Null entry in '{name}'.", this);
                    continue;
                }
                var id = configuration.Id;
                if (configurationsMap.TryGetValue(id, out var existing))
                {
                    if (existing != configuration)
                        Debug.LogError(
                            $"Duplicate configuration ID {id} between '{existing.name}' and '{configuration.name}'. " +
                            "Re-import one of them to regenerate its GUID.", this);
                    continue;
                }
                configurationsMap.Add(id, configuration);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (soundIgnoreRadiusSq < soundPriorityRadiusSq)
            {
                Debug.LogWarning(
                    $"'{name}': ignore radius ({soundIgnoreRadiusSq}) < priority radius " +
                    $"({soundPriorityRadiusSq}). All sounds will be classified as priority.", this);
            }

            var ignoreRadius = Mathf.Sqrt(soundIgnoreRadiusSq);
            foreach (var c in configurationList)
            {
                if (c == null) continue;
                if (c.maxDistance > ignoreRadius)
                {
                    Debug.LogWarning(
                        $"'{c.name}'.maxDistance ({c.maxDistance}) exceeds ignore radius " +
                        $"({ignoreRadius:F1}). Sound will be culled before its rolloff finishes.",
                        this);
                }
            }
        }
#endif
    }
}
