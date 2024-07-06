using ECS_Sound.Components;
using ECS_Sound.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ECS_Sound.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct CollisionSoundSystem : ISystem
    {
        private ComponentLookup<LocalToWorld> lookupLocalToWorld;
        private ComponentLookup<PhysicsVelocity> lookupPhysicsVelocity;
        private ComponentLookup<CollisionSoundComponent> lookupCollisionSound;
        private ComponentLookup<ActiveSoundSourceComponent> lookupActiveSoundSource;
        private ComponentLookup<CollisionSoundInteractionsComponent> lookupCollisionSoundInteractions;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            lookupLocalToWorld = state.GetComponentLookup<LocalToWorld>(true);
            lookupCollisionSound = state.GetComponentLookup<CollisionSoundComponent>(true);
            lookupPhysicsVelocity = state.GetComponentLookup<PhysicsVelocity>(true);
            lookupActiveSoundSource = state.GetComponentLookup<ActiveSoundSourceComponent>();
            lookupCollisionSoundInteractions = state.GetComponentLookup<CollisionSoundInteractionsComponent>();
            
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            
            lookupLocalToWorld.Update(ref state);
            lookupCollisionSound.Update(ref state);
            lookupPhysicsVelocity.Update(ref state);
            lookupActiveSoundSource.Update(ref state);
            lookupCollisionSoundInteractions.Update(ref state);
            
            state.Dependency = new CollisionSoundJob
            {
                ElapsedTime = elapsedTime,
                PhysicsWorld = physicsWorldSingleton.PhysicsWorld,
                LocalToWorldFromEntity = lookupLocalToWorld,
                CollisionSoundFromEntity = lookupCollisionSound,
                PhysicsVelocityFromEntity = lookupPhysicsVelocity,
                ActiveSoundSourceFromEntity = lookupActiveSoundSource,
                CollisionSoundInteractionsFromEntity = lookupCollisionSoundInteractions,
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }
    
        [BurstCompile]
        private struct CollisionSoundJob : ICollisionEventsJob
        {
            [ReadOnly]
            public double ElapsedTime;
            [ReadOnly] 
            public PhysicsWorld PhysicsWorld;
            [ReadOnly]
            public ComponentLookup<LocalToWorld> LocalToWorldFromEntity;
            [ReadOnly]
            public ComponentLookup<CollisionSoundComponent> CollisionSoundFromEntity;
            [ReadOnly]
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityFromEntity;
            
            public ComponentLookup<ActiveSoundSourceComponent> ActiveSoundSourceFromEntity;
            public ComponentLookup<CollisionSoundInteractionsComponent> CollisionSoundInteractionsFromEntity;

            public void Execute(CollisionEvent collisionEvent)
            {
                GetCollidedEntities(in collisionEvent, out var activeSoundSourceEntity, out var soundSourceEntity);
                
                if (!ActiveSoundSourceFromEntity.HasComponent(activeSoundSourceEntity)) return;

                if (ActiveSoundSourceFromEntity.HasComponent(soundSourceEntity))
                {
                    // Both are active sound sources
                    SetInteraction(activeSoundSourceEntity, soundSourceEntity, 
                        soundSourceEntity, ref collisionEvent);
                    SetInteraction(soundSourceEntity, activeSoundSourceEntity, 
                        activeSoundSourceEntity, ref collisionEvent);
                }
                else
                {
                    // Only activeSoundSource is active sound sources
                    var isSecondaryClip = CollisionSoundFromEntity.HasComponent(soundSourceEntity);
                    var configurationProvider = isSecondaryClip ? soundSourceEntity : activeSoundSourceEntity;
                    SetInteraction(activeSoundSourceEntity, configurationProvider, 
                        soundSourceEntity, ref collisionEvent, isSecondaryClip);
                }
            }
            
            private void SetInteraction(in Entity consumerEntity, in Entity providerEntity, in Entity collidedEntity,
                ref CollisionEvent collisionEvent, bool isUsingSecondaryClip = false)
            {
                var consumerLocalToWorld = LocalToWorldFromEntity[consumerEntity];
                var consumerInteractions = CollisionSoundInteractionsFromEntity[consumerEntity];
                var interactionId = CollisionSoundSystemUtils.GetInteractionId(collidedEntity, ref consumerInteractions);
                var interaction = consumerInteractions[interactionId];

                if (CollisionSoundSystemUtils.IsTouchInteraction(interaction, ElapsedTime))
                {
                    var collisionDetails = collisionEvent.CalculateDetails(ref PhysicsWorld);
                    CollisionSoundSystemUtils.SetActiveInteraction(ref interaction, interactionId,
                        ref ActiveSoundSourceFromEntity, CollisionSoundFromEntity, ElapsedTime, 
                        consumerEntity, providerEntity, collidedEntity, 
                        collisionDetails.AverageContactPointPosition, collisionDetails.EstimatedImpulse,
                        false, isUsingSecondaryClip);
                }
                else
                {
                    if (CollisionSoundSystemUtils.IsPreviousSoundPlay(interaction, ElapsedTime) 
                        && CollisionSoundSystemUtils.IsSliding(interaction, consumerLocalToWorld))
                    {
                        var physicsVelocity = PhysicsVelocityFromEntity[consumerEntity];
                        var impulse = CollisionSoundSystemUtils.GetVelocityImpulse(physicsVelocity);
                        var collisionDetails = collisionEvent.CalculateDetails(ref PhysicsWorld);
                        CollisionSoundSystemUtils.SetActiveInteraction(ref interaction, interactionId,
                            ref ActiveSoundSourceFromEntity, CollisionSoundFromEntity, ElapsedTime, 
                            consumerEntity, providerEntity, collidedEntity, 
                            collisionDetails.AverageContactPointPosition, impulse,
                            true, isUsingSecondaryClip);
                    }
                }

                interaction.EntityToWorld = consumerLocalToWorld;
                interaction.UpdatedTime = ElapsedTime;
                consumerInteractions[interactionId] = interaction;
                CollisionSoundInteractionsFromEntity[consumerEntity] = consumerInteractions;
            }

            private void GetCollidedEntities(in CollisionEvent collisionEvent, 
                out Entity activeSoundSource, out Entity soundSource)
            {
                if (ActiveSoundSourceFromEntity.HasComponent(collisionEvent.EntityA))
                {
                    activeSoundSource = collisionEvent.EntityA;
                    soundSource = collisionEvent.EntityB;
                }
                else
                {
                    activeSoundSource = collisionEvent.EntityB;
                    soundSource = collisionEvent.EntityA;
                }
            }
        }
    }
}
