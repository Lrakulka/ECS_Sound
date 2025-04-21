using ECS_Sound.AudioConfiguration;
using ECS_Sound.Systems;
using Unity.Mathematics;
using UnityEngine;

namespace ECS_Sound.Utils
{
    public static class HybridAudioUtils
    {
        public static float GetAudioClipLength(float audioClipLength, float audioSourcePitchAbs)
        {
            return audioSourcePitchAbs != 0f ? audioClipLength / audioSourcePitchAbs : 0f;
        }
        
        public static bool PlayClipSound(ref CollisionSoundHybridAudioSystem.AudioSourcesHub audioSourceHub,
            int configurationId, int audioClipId, in float3 position, float volume, 
            in CollisionSoundConfigurationHub collisionSoundConfigurationHub,
            out AudioSource audioSource)
        {
            audioSource = audioSourceHub.GetAudioSource();

            if (audioSource.isPlaying)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{audioSource} is still playing. Can't play sound");
#endif
                return false;
            }
            
            audioSource.gameObject.SetActive(true);
            audioSource.transform.position = position;
            var audioClip = collisionSoundConfigurationHub.GetAudioClip(audioClipId);
            var audioSourceConfiguration = collisionSoundConfigurationHub.GetConfiguration(configurationId);
            SetConfiguration(ref audioSource, audioSourceConfiguration);

            // TODO: Make volumeScale - sound depend on impulse angle too, dot(velocity, normal)
            audioSource.volume = volume;
            audioSource.clip = audioClip;
            audioSource.Play();
            return true;
        }

        private static void SetConfiguration(ref AudioSource audioSource, in CollisionSoundConfiguration configuration)
        {
            audioSource.mute = configuration.mute;
            audioSource.pitch = configuration.pitch;
            audioSource.priority = configuration.priority;
            audioSource.spread = configuration.spread;
            audioSource.volume = configuration.volume;
            audioSource.bypassEffects = configuration.bypassEffects;
            audioSource.dopplerLevel = configuration.dopplerLevel;
            audioSource.maxDistance = configuration.maxDistance;
            audioSource.minDistance = configuration.minDistance;
            audioSource.panStereo = configuration.stereoPan;
            audioSource.rolloffMode = configuration.volumeRolloff;
            audioSource.spatialBlend = configuration.spatialBlend;
            audioSource.bypassListenerEffects = configuration.bypassListenerEffects;
            audioSource.bypassReverbZones = configuration.bypassReverbZones;
            audioSource.reverbZoneMix = configuration.reverbZoneMix;
        }
    }
}
