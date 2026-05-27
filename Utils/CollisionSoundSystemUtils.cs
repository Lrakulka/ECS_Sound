using ECS_Sound.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS_Sound.Utils
{
    [BurstCompile]
    public static class CollisionSoundSystemUtils
    {
        private const float MAX_SUM_LINEAR_VELOCITY_THRESHOLD = 25f;
        private const float TOUCH_THRESHOLD_TIME = 0.1f;
        private const float SLIDING_DELTA = 0.01f;

        [BurstCompile]
        internal static int GetInteractionId(in Entity collidedEntity, ref CollisionSoundInteractionsComponent interactions)
        {
            var interactionId = interactions.GetInteractionId(collidedEntity);
            interactionId = interactionId != -1
                ? interactionId
                : interactions.GetFirstCleanInteractionId();
            interactionId = interactionId != -1
                ? interactionId
                : GetInteractionIdAfterCleaning(ref interactions);
            return interactionId;
        }

        [BurstCompile]
        private static int GetInteractionIdAfterCleaning(ref CollisionSoundInteractionsComponent interactions)
        {
            interactions.Clean();
            var interactionId = interactions.GetFirstCleanInteractionId();
            return interactionId != -1 ? interactionId : interactions.Length - 1;
        }

        /// <summary>
        /// Populate an interaction with the configurations of consumer + provider. The audio system
        /// will pick clips at play time from the respective configurations' arrays.
        ///
        /// MainConfigurationId is always the consumer's. SecondaryConfigurationId is the provider's
        /// when <paramref name="isUsingSecondaryClip"/> is true, otherwise 0 (no layering).
        /// </summary>
        [BurstCompile]
        internal static void SetActiveInteraction(ref CollisionInteraction interaction, int interactionId,
            ref ComponentLookup<ActiveSoundSourceComponent> activeSoundSourceFromEntity,
            in ComponentLookup<CollisionSoundComponent> collisionSoundFromEntity, double time,
            in Entity consumerEntity, in Entity providerEntity, in Entity collidedEntity,
            in float3 averageContactPoint, float impulse, bool isSlidingClip, bool isUsingSecondaryClip = true)
        {
            var consumerSound = collisionSoundFromEntity[consumerEntity];
            var providerSound = collisionSoundFromEntity[providerEntity];
            var activeSoundSource = activeSoundSourceFromEntity[consumerEntity];

            interaction.PlayClipEndTime = time;
            interaction.CollisionEntity = collidedEntity;
            interaction.VolumeScale = GetInteractionVolumeScale(impulse);
            interaction.AverageContactPoint = averageContactPoint;
            interaction.IsSliding = isSlidingClip;

            // Consumer's config drives the AudioSource (pitch, spatial, rolloff). Consumer's clip
            // (picked at play time) plays as the main clip → consumer-material sounds like itself.
            interaction.MainConfigurationId = consumerSound.ConfigurationId;

            // Provider's config controls the layered clip's volume (via PlayOneShot). The layered
            // clip is picked at play time from the provider's array → other-material sounds like itself.
            interaction.SecondaryConfigurationId = isUsingSecondaryClip ? providerSound.ConfigurationId : 0;

            activeSoundSource.SetPlaySoundActive(interactionId, true);
            activeSoundSourceFromEntity[consumerEntity] = activeSoundSource;
        }

        [BurstCompile]
        internal static float GetVelocityImpulse(in PhysicsVelocity physicsVelocity)
        {
            return math.csum(math.abs(physicsVelocity.Linear));
        }

        // TODO: existing limitation — long update cycles can over-trigger; tiny threshold can miss hits.
        [BurstCompile]
        internal static bool IsTouchInteraction(in CollisionInteraction interaction, double time)
        {
            return (time - interaction.UpdatedTime) > TOUCH_THRESHOLD_TIME;
        }

        [BurstCompile]
        internal static bool IsPreviousSoundPlay(in CollisionInteraction interaction, double time)
        {
            return interaction.PlayClipEndTime < time;
        }

        [BurstCompile]
        internal static bool IsSliding(in CollisionInteraction interaction, in LocalToWorld currLocalToWorld)
        {
            var localToWorld = interaction.EntityToWorld;
            return math.any(math.abs(localToWorld.Position - currLocalToWorld.Position) > SLIDING_DELTA)
                   || math.any(math.abs(localToWorld.Rotation.value - currLocalToWorld.Rotation.value) > SLIDING_DELTA);
        }

        [BurstCompile]
        private static float GetInteractionVolumeScale(float impact)
        {
            // Linear scale; the audio system optionally applies sqrt() based on the asset toggle.
            return math.min(impact / MAX_SUM_LINEAR_VELOCITY_THRESHOLD, 1f);
        }
    }
}
