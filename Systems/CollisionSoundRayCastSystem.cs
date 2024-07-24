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
    public partial struct CollisionSoundRayCastSystem : ISystem
    {
        private const float MAX_SUM_LINEAR_VELOCITY_THRESHOLD = 25f;
        private const float RAY_CAST_MULTIPLIER = 0.05f;

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
            
            state.RequireForUpdate<RayColliderInfoComponent>();
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
                in RayColliderInfoComponent rayColliderInfo, in LocalToWorld consumerLocalToWorld)
            {
                var rayCastFrom = consumerLocalToWorld.Position;
                var rayCastTo = consumerLocalToWorld.Position - consumerLocalToWorld.Up * RAY_CAST_MULTIPLIER;

                if (!GetFirstCollision(rayCastFrom, rayCastTo, rayColliderInfo.Filter, out var hit)) return;
                    
                var collidedEntity = hit.Entity;
                var contactPoint = hit.Position;
                var isUsingSecondaryClip = CollisionSoundFromEntity.HasComponent(collidedEntity);
                var providerEntity = isUsingSecondaryClip ? collidedEntity : consumerEntity;
                    
                SetInteraction(consumerEntity, providerEntity, collidedEntity, rayColliderInfo, consumerLocalToWorld,
                    contactPoint, isUsingSecondaryClip);
            }
            
            private void SetInteraction(in Entity consumerEntity, in Entity providerEntity, in Entity collidedEntity, 
                in RayColliderInfoComponent rayColliderInfo, in LocalToWorld consumerLocalToWorld, 
                in float3 contactPoint, bool isUsingSecondaryClip = false)
            {
                var consumerInteractions = CollisionSoundInteractionsFromEntity[consumerEntity];
                var interactionId = CollisionSoundSystemUtils.GetInteractionId(collidedEntity, ref consumerInteractions);
                var interaction = consumerInteractions[interactionId];

                float impulse;
                if (PhysicsVelocityFromEntity.HasComponent(rayColliderInfo.Owner))
                {
                    var physicsVelocity = PhysicsVelocityFromEntity[rayColliderInfo.Owner];
                    impulse = GetImpulse(physicsVelocity, rayColliderInfo);
                }
                else
                {
                    impulse = MAX_SUM_LINEAR_VELOCITY_THRESHOLD * rayColliderInfo.MinSoundVelocity;
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

            private static float GetImpulse(in PhysicsVelocity physicsVelocity, in RayColliderInfoComponent rayColliderInfo)
            {
                var impulse = math.max(
                    CollisionSoundSystemUtils.GetVelocityImpulse(physicsVelocity),
                    MAX_SUM_LINEAR_VELOCITY_THRESHOLD * rayColliderInfo.MinSoundVelocity);
                return impulse;
            }

            private bool GetFirstCollision(in float3 rayFrom, in float3 rayTo, in CollisionFilter collisionFilter, 
                out RaycastHit hit)
            {
                var input = new RaycastInput
                {
                    Start = rayFrom,
                    End = rayTo,
                    Filter = collisionFilter,
                };
                return PhysicsWorld.CollisionWorld.CastRay(input, out hit);
            }
        }
    }
}
