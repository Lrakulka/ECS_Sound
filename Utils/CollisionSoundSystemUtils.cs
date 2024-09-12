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

        private static int GetInteractionIdAfterCleaning(ref CollisionSoundInteractionsComponent interactions)
        {
            interactions.Clean();
            var interactionId = interactions.GetFirstCleanInteractionId();
            return interactionId != -1 ? interactionId : interactions.Length - 1;
        }

        // TODO: simplify 
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
            interaction.ConfigurationId = providerSound.ConfigurationId;
            interaction.MainClipId = isSlidingClip ? consumerSound.SlideClipId : consumerSound.TouchClipId;
            if (isUsingSecondaryClip)
            {
                interaction.SecondaryClipId = isSlidingClip ? providerSound.SlideClipId : providerSound.TouchClipId;
            }

            activeSoundSource.SetPlaySoundActive(interactionId, true);
            activeSoundSourceFromEntity[consumerEntity] = activeSoundSource;
        }

        internal static float GetVelocityImpulse(in PhysicsVelocity physicsVelocity)
        {
            return math.csum(math.abs(physicsVelocity.Linear));
        }

        // TODO: Fix method logic, this approach is not reliable, if update cycle was long it will trigger it
        // if TOUCH_THRESHOLD_TIME to large then some hits will be missed
        internal static bool IsTouchInteraction(in CollisionInteraction interaction, double time)
        {
            return (time - interaction.UpdatedTime) > TOUCH_THRESHOLD_TIME;
        }

        internal static bool IsPreviousSoundPlay(in CollisionInteraction interaction, double time)
        {
            return interaction.PlayClipEndTime < time;
        }
        
        internal static bool IsSliding(in CollisionInteraction interaction, in LocalToWorld currLocalToWorld)
        {
            var localToWorld = interaction.EntityToWorld;
            return math.any(math.abs(localToWorld.Position - currLocalToWorld.Position) > SLIDING_DELTA)
                   || math.any(math.abs(localToWorld.Rotation.value - currLocalToWorld.Rotation.value) > SLIDING_DELTA);
        }

        private static float GetInteractionVolumeScale(float impact)
        {
            return math.min(1f, impact / MAX_SUM_LINEAR_VELOCITY_THRESHOLD);
        }
    }
}
