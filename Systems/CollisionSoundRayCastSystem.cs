using System.Runtime.CompilerServices;
using ECS_Common.Utils;
using ECS_Sound.Components;
using ECS_Sound.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS_Sound.Systems
{
    [UpdateInGroup(typeof(CollisionSoundSystemGroup))]
    [BurstCompile]
    public partial struct CollisionSoundRayCastSystem : ISystem
    {
        private const float MAX_SUM_LINEAR_VELOCITY_THRESHOLD = 25f;

        private ComponentLookup<PhysicsVelocity> lookupPhysicsVelocity;
        private ComponentLookup<CollisionSoundComponent> lookupCollisionSound;
        private ComponentLookup<ActiveSoundSourceComponent> lookupActiveSoundSource;
        private ComponentLookup<CollisionSoundInteractionsComponent> lookupCollisionSoundInteractions;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            lookupPhysicsVelocity = state.GetComponentLookup<PhysicsVelocity>(true);
            lookupCollisionSound = state.GetComponentLookup<CollisionSoundComponent>(true);
            lookupActiveSoundSource = state.GetComponentLookup<ActiveSoundSourceComponent>();
            lookupCollisionSoundInteractions = state.GetComponentLookup<CollisionSoundInteractionsComponent>();
            
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<RaySoundColliderInfoComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>(); 
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            
            lookupCollisionSound.Update(ref state);
            lookupPhysicsVelocity.Update(ref state);
            lookupActiveSoundSource.Update(ref state);
            lookupCollisionSoundInteractions.Update(ref state);

            new RayColliderJob
            {
                ElapsedTime = elapsedTime,
                PhysicsWorld = worldSingleton.PhysicsWorld,
                CollisionSoundFromEntity = lookupCollisionSound,
                PhysicsVelocityFromEntity = lookupPhysicsVelocity,
                ActiveSoundSourceFromEntity = lookupActiveSoundSource,
                CollisionSoundInteractionsFromEntity = lookupCollisionSoundInteractions,
            }.ScheduleParallel();
        }
        
        [BurstCompile]
        private partial struct RayColliderJob : IJobEntity
        {
            [ReadOnly]
            public double ElapsedTime;
            [ReadOnly]
            public PhysicsWorld PhysicsWorld;
            [ReadOnly]
            public ComponentLookup<CollisionSoundComponent> CollisionSoundFromEntity;
            [ReadOnly]
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityFromEntity;
            [NativeDisableParallelForRestriction] 
            public ComponentLookup<ActiveSoundSourceComponent> ActiveSoundSourceFromEntity;
            [NativeDisableParallelForRestriction] 
            public ComponentLookup<CollisionSoundInteractionsComponent> CollisionSoundInteractionsFromEntity;
            
            [BurstCompile]
            public void Execute(Entity consumerEntity, 
                in RaySoundColliderInfoComponent raySoundColliderInfo, in LocalToWorld consumerLocalToWorld)
            {
                var rayCastFrom = consumerLocalToWorld.Position;
                var rayCastTo = consumerLocalToWorld.Position - raySoundColliderInfo.RayPath;

                if (!CommonUtils.RayCast(rayCastFrom, rayCastTo, raySoundColliderInfo.Filter, ref PhysicsWorld.CollisionWorld, out var hit)) return;
                    
                var collidedEntity = hit.Entity;
                var contactPoint = hit.Position;
                var isUsingSecondaryClip = CollisionSoundFromEntity.HasComponent(collidedEntity);
                var providerEntity = isUsingSecondaryClip ? collidedEntity : consumerEntity;
                    
                SetInteraction(consumerEntity, providerEntity, collidedEntity, raySoundColliderInfo, consumerLocalToWorld,
                    contactPoint, isUsingSecondaryClip);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetInteraction(in Entity consumerEntity, in Entity providerEntity, in Entity collidedEntity, 
                in RaySoundColliderInfoComponent raySoundColliderInfo, in LocalToWorld consumerLocalToWorld, 
                in float3 contactPoint, bool isUsingSecondaryClip = false)
            {
                var consumerInteractions = CollisionSoundInteractionsFromEntity[consumerEntity];
                var interactionId = CollisionSoundSystemUtils.GetInteractionId(collidedEntity, ref consumerInteractions);
                var interaction = consumerInteractions[interactionId];

                float impulse;
                if (PhysicsVelocityFromEntity.HasComponent(raySoundColliderInfo.Owner))
                {
                    var physicsVelocity = PhysicsVelocityFromEntity[raySoundColliderInfo.Owner];
                    impulse = GetImpulse(physicsVelocity, raySoundColliderInfo);
                }
                else
                {
                    impulse = MAX_SUM_LINEAR_VELOCITY_THRESHOLD * raySoundColliderInfo.MinSoundVelocity;
                }
                
                if (CollisionSoundSystemUtils.IsTouchInteraction(interaction, ElapsedTime))
                {
                    CollisionSoundSystemUtils.SetActiveInteraction(ref interaction, interactionId,
                        ref ActiveSoundSourceFromEntity, CollisionSoundFromEntity, ElapsedTime,
                        consumerEntity, providerEntity, collidedEntity, contactPoint, impulse,false, isUsingSecondaryClip);
                }
                else
                {
                    if (CollisionSoundSystemUtils.IsPreviousSoundPlay(interaction, ElapsedTime)
                        && CollisionSoundSystemUtils.IsSliding(interaction, consumerLocalToWorld))
                    {
                        CollisionSoundSystemUtils.SetActiveInteraction(ref interaction, interactionId,
                            ref ActiveSoundSourceFromEntity, CollisionSoundFromEntity, ElapsedTime,
                            consumerEntity, providerEntity, collidedEntity, contactPoint, impulse, true, isUsingSecondaryClip);
                    }
                }

                interaction.EntityToWorld = consumerLocalToWorld;
                interaction.UpdatedTime = ElapsedTime;
                consumerInteractions[interactionId] = interaction;
                CollisionSoundInteractionsFromEntity[consumerEntity] = consumerInteractions;
            }

            [BurstCompile]
            private static float GetImpulse(in PhysicsVelocity physicsVelocity, in RaySoundColliderInfoComponent raySoundColliderInfo)
            {
                var impulse = math.max(
                    CollisionSoundSystemUtils.GetVelocityImpulse(physicsVelocity),
                    MAX_SUM_LINEAR_VELOCITY_THRESHOLD * raySoundColliderInfo.MinSoundVelocity);
                return impulse;
            }
        }
    }
}
