using ECS_Sound.AudioConfiguration;
using ECS_Sound.Components;
using Unity.Mathematics;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace ECS_Sound.Utils
{
    public static class HybridAudioUtils
    {
        /// <summary>
        /// Configure and play the interaction on the given source. Picks one random clip from the
        /// appropriate (touch or slide) array, applies the per-kind playback settings (volume,
        /// pitch) plus jitter, velocity-weighted pitch, and optional sqrt volume curve.
        /// Returns true on success. Writes the real (pitch-adjusted) play-end-time via out param.
        /// </summary>
        public static bool PlayClipSound(
            AudioSource source,
            in CollisionInteraction interaction,
            CollisionSoundConfigurationAsset asset,
            double now,
            out double actualPlayEndTime)
        {
            actualPlayEndTime = now;

            if (!asset.TryGetConfiguration(interaction.MainConfigurationId, out var mainCfg))
            {
                Debug.LogWarning($"Unknown configuration {interaction.MainConfigurationId}");
                return false;
            }

            var mainClips = interaction.IsSliding ? mainCfg.slideClips : mainCfg.touchClips;
            if (mainClips == null || mainClips.Length == 0)
            {
                Debug.LogWarning($"Configuration '{mainCfg.name}' has no {(interaction.IsSliding ? "slide" : "touch")} clips.");
                return false;
            }

            var mainClip = mainClips[URandom.Range(0, mainClips.Length)];
            if (mainClip == null) return false;

            // Pick the per-kind playback settings (touch vs slide volume + pitch).
            var mainSettings = interaction.IsSliding ? mainCfg.slide : mainCfg.touch;

            // Position & shared 3D / mixer configuration.
            source.gameObject.SetActive(true);
            source.transform.position = interaction.AverageContactPoint;
            ApplyConfiguration(source, mainCfg);

            // Pitch: per-kind base × velocity curve × random jitter.
            // Heavy hits sound thuddier (lower pitch); light hits sound crisper (higher pitch).
            var velocityPitch = math.lerp(
                1f + asset.VelocityPitchRange,   // light (low VolumeScale)  → higher pitch
                1f - asset.VelocityPitchRange,   // heavy (high VolumeScale) → lower pitch
                math.saturate(interaction.VolumeScale));
            var pitchJitter = 1f + (URandom.value - 0.5f) * 2f * asset.PitchJitter;
            source.pitch = mainSettings.pitch * pitchJitter * velocityPitch;

            // Volume: per-kind base × optional sqrt curve × random jitter.
            var volumeCurve = asset.UseSqrtVolumeCurve
                ? math.sqrt(interaction.VolumeScale)
                : interaction.VolumeScale;
            var volumeJitter = 1f + (URandom.value - 0.5f) * 2f * asset.VolumeJitter;
            source.volume = mainSettings.volume * volumeCurve * volumeJitter;

            source.clip = mainClip;
            source.Play();

            var pitchAbs = math.max(math.abs(source.pitch), 0.0001f);
            actualPlayEndTime = now + (mainClip.length / pitchAbs);

            // Secondary layer (provider's clip, picked from provider's array, with provider's
            // per-kind volume). PlayOneShot inherits the source's pitch and spatial settings;
            // only volume is independently controllable. Adequate for collision layering.
            if (interaction.SecondaryConfigurationId != 0
                && asset.TryGetConfiguration(interaction.SecondaryConfigurationId, out var secCfg))
            {
                var secClips = interaction.IsSliding ? secCfg.slideClips : secCfg.touchClips;
                if (secClips != null && secClips.Length > 0)
                {
                    var secClip = secClips[URandom.Range(0, secClips.Length)];
                    if (secClip != null)
                    {
                        var secSettings = interaction.IsSliding ? secCfg.slide : secCfg.touch;
                        var secVolume = secSettings.volume * volumeCurve * volumeJitter;
                        source.PlayOneShot(secClip, secVolume);

                        var secEnd = now + (secClip.length / pitchAbs);
                        if (secEnd > actualPlayEndTime) actualPlayEndTime = secEnd;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Apply the shared (non-per-kind) configuration to the source. Volume and pitch are set
        /// separately by the caller, since those come from the per-kind settings.
        /// </summary>
        private static void ApplyConfiguration(AudioSource source, CollisionSoundConfiguration c)
        {
            source.mute = c.mute;
            source.priority = c.priority;
            source.spread = c.spread;
            source.bypassEffects = c.bypassEffects;
            source.dopplerLevel = c.dopplerLevel;
            source.maxDistance = c.maxDistance;
            source.minDistance = c.minDistance;
            source.panStereo = c.stereoPan;
            source.rolloffMode = c.volumeRolloff;
            source.spatialBlend = c.spatialBlend;
            source.bypassListenerEffects = c.bypassListenerEffects;
            source.bypassReverbZones = c.bypassReverbZones;
            source.reverbZoneMix = c.reverbZoneMix;
            // volume and pitch are set by the caller from per-kind settings.
        }
    }
}
