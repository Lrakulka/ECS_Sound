using UnityEngine;

namespace ECS_Sound.AudioConfiguration
{
    public class CollisionSoundConfiguration : ScriptableObject
    {
        public AudioClip touchClip;
        public AudioClip slideClip;
        
        [Space(5)] [Header("Configuration for clip play")]
        public bool mute;
        public bool bypassEffects;
        public bool bypassListenerEffects;
        public bool bypassReverbZones;
        
        [Range(0, 256)]
        public int priority = 128;
        [Range(0, 1)]
        public float volume = 1;
        [Range(-3, 3)]
        public float pitch = 1;
        [Range(-1, 1)]
        public float stereoPan;
        [Range(0, 1)]
        public float spatialBlend;
        [Range(0, 1.1f)]
        public float reverbZoneMix = 1;
        
        [Header("3D Sound Settings")]
        [Range(0, 5)]
        public float dopplerLevel = 1;
        [Range(0, 360)]
        public float spread;
        public AudioRolloffMode volumeRolloff = AudioRolloffMode.Logarithmic;
        public float minDistance = 1;
        public float maxDistance = 500;
    }
}
