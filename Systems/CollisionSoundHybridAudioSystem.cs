using ECS_Sound.AudioConfiguration;
using ECS_Sound.Components;
using ECS_Sound.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECS_Sound.Systems
{
    /// <summary>Queued playback entry, sorted descending by Score before draining.</summary>
    public struct ScoredInteraction
    {
        public CollisionInteraction Interaction;
        public Entity ConsumerEntity;
        public int InteractionId;
        public float Score;
        public double PlayEndTime;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ClientSimulation)]
    public partial class CollisionSoundHybridAudioSystem : SystemBase
    {
        private EntityQuery entityQuery;
        private AudioSourcesHub audioSourcesHub;
        private CollisionSoundListenerRegistry listenerRegistry;
        private CollisionSoundConfigurationAsset assetRef;

        private ComponentLookup<CollisionSoundInteractionsComponent> interactionsLookup;
        private NativeList<ScoredInteraction> scoredInteractions;

        protected override void OnCreate()
        {
            RequireForUpdate<CollisionSoundBlobReference>();
            interactionsLookup = GetComponentLookup<CollisionSoundInteractionsComponent>(isReadOnly: true);
        }

        private void LazyInitialize(ref CollisionSoundBlobData blob)
        {
            assetRef = CollisionSoundConfigurationHub.LoadAsset();
            audioSourcesHub = new AudioSourcesHub(blob.AudioSourcePoolSize);
            scoredInteractions = new NativeList<ScoredInteraction>(blob.AudioSourcePoolSize * 2, Allocator.Persistent);

            var rescan = assetRef != null ? assetRef.ListenerRescanInterval : 1.0f;
            listenerRegistry = new CollisionSoundListenerRegistry(rescan, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (scoredInteractions.IsCreated) scoredInteractions.Dispose();
            listenerRegistry?.Dispose();
            audioSourcesHub?.Dispose();
        }

        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            var blobRef = SystemAPI.GetSingleton<CollisionSoundBlobReference>();
            ref var blob = ref blobRef.Blob.Value;

            if (audioSourcesHub == null) LazyInitialize(ref blob);

            var now = SystemAPI.Time.ElapsedTime;
            audioSourcesHub.Tick(now);

            // Stop slide sources whose interaction has gone stale (sliding ended).
            EntityManager.CompleteDependencyBeforeRO<CollisionSoundInteractionsComponent>();
            interactionsLookup.Update(this);
            audioSourcesHub.StopExpiredSlides(now,
                staleThreshold: assetRef.SlideStaleTime,
                graceTime: assetRef.SlideStopGraceTime,
                interactionsLookup);

            listenerRegistry.UpdatePositions(SystemAPI.Time.DeltaTime);

            // Wait for last frame's collection job before reading scoredInteractions on main thread.
            Dependency.Complete();

            // Drain whatever was collected on the previous frame.
            PlayQueuedSounds(now);
            scoredInteractions.Clear();

            // Capacity sizing for AddNoResize (ParallelWriter requirement).
            var maxPossible = entityQuery.CalculateEntityCount() * 4;
            if (scoredInteractions.Capacity < maxPossible)
            {
                scoredInteractions.Capacity = math.max(maxPossible, blob.AudioSourcePoolSize * 4);
            }

            var listenerPositions = listenerRegistry.Positions.AsArray();
            if (listenerPositions.Length == 0) return; // Nothing to score against.

            var writer = scoredInteractions.AsParallelWriter();
            var priorityRadiusSq = blob.SoundPriorityRadiusSq;
            var ignoreRadiusSq = blob.SoundIgnoreRadiusSq;
            var blobRefLocal = blobRef;

            Entities
                .WithStoreEntityQueryInField(ref entityQuery)
                .WithReadOnly(listenerPositions)
                .WithChangeFilter<ActiveSoundSourceComponent>()
                .ForEach((Entity entity, ref ActiveSoundSourceComponent activeSoundSource,
                    ref CollisionSoundInteractionsComponent interactions, in LocalToWorld localToWorld) =>
                {
                    ref var b = ref blobRefLocal.Blob.Value;

                    for (var interactionId = 0; interactionId < interactions.Length; interactionId++)
                    {
                        if (!activeSoundSource.IsPlaySound(interactionId)) continue;

                        // Nearest-listener distance.
                        var distanceSq = double.MaxValue;
                        for (int li = 0; li < listenerPositions.Length; li++)
                        {
                            distanceSq = math.min(distanceSq,
                                math.distancesq(listenerPositions[li], localToWorld.Position));
                        }
                        if (distanceSq > ignoreRadiusSq) continue;

                        var interaction = interactions[interactionId];

                        // Look up the main configuration's per-kind pitch for end-time prediction.
                        var configIdx = CollisionSoundBlobLookup.IndexOf(ref b.ConfigIds, interaction.MainConfigurationId);
                        if (configIdx < 0)
                        {
                            Debug.LogWarning($"Can't find id of configuration in collision blob assets for MainConfigurationId ${interaction.MainConfigurationId}");
                            continue;
                        }
                        var pitchAbs = interaction.IsSliding
                            ? b.Configs[configIdx].SlidePitchAbs
                            : b.Configs[configIdx].TouchPitchAbs;

                        // Predicted end-time: we don't know the actual picked clip yet (main thread),
                        // so we use a conservative 1-second placeholder scaled by pitch. The real
                        // end-time is written back by PlayQueuedSounds after picking.
                        const float clipLengthEstimateSeconds = 1.0f;
                        var maxClipDuration = pitchAbs > 0 ? clipLengthEstimateSeconds / pitchAbs : clipLengthEstimateSeconds;
                        var playEndTime = interaction.UpdatedTime + maxClipDuration;

                        // Score: priority-radius dominates; within a band, touch beats slide;
                        // within (band, kind), closer wins.
                        var inPriority = distanceSq < priorityRadiusSq;
                        var bandScore = inPriority ? 1000f : 0f;
                        var kindScore = interaction.IsSliding ? 0f : 100f;
                        var distScore = (float)(ignoreRadiusSq - distanceSq);
                        var score = bandScore + kindScore + distScore * 0.01f;

                        writer.AddNoResize(new ScoredInteraction
                        {
                            Interaction = interaction,
                            ConsumerEntity = entity,
                            InteractionId = interactionId,
                            Score = score,
                            PlayEndTime = playEndTime,
                        });
                    }

                    activeSoundSource.interactionsIds = 0;
                })
                .WithName("CollisionSoundHybridAudioCollectionJob")
                .Schedule();
        }

        private void PlayQueuedSounds(double now)
        {
            if (scoredInteractions.Length == 0 || assetRef == null) return;

            scoredInteractions.AsArray().Sort(default(ScoreDescendingComparer));

            for (int i = 0; i < scoredInteractions.Length; i++)
            {
                var s = scoredInteractions[i];
                if (!audioSourcesHub.TryAcquire(
                        s.Score, s.PlayEndTime,
                        s.Interaction.IsSliding, s.ConsumerEntity, s.InteractionId,
                        out var audioSource, out _))
                {
                    // Pool full, score didn't outrank anything; remaining entries score even lower.
                    break;
                }

                if (HybridAudioUtils.PlayClipSound(audioSource, s.Interaction, assetRef, now,
                        out var actualEndTime))
                {
                    // Write the real end-time back to the entity so collection systems gate retriggers correctly.
                    WriteBackPlayEndTime(s.ConsumerEntity, s.InteractionId, actualEndTime);
                }
            }
        }

        private void WriteBackPlayEndTime(Entity entity, int interactionId, double endTime)
        {
            if (!EntityManager.HasComponent<CollisionSoundInteractionsComponent>(entity)) return;
            var interactions = EntityManager.GetComponentData<CollisionSoundInteractionsComponent>(entity);
            var interaction = interactions[interactionId];
            interaction.PlayClipEndTime = endTime;
            interactions[interactionId] = interaction;
            EntityManager.SetComponentData(entity, interactions);
        }

        private struct ScoreDescendingComparer : System.Collections.Generic.IComparer<ScoredInteraction>
        {
            public int Compare(ScoredInteraction a, ScoredInteraction b) => b.Score.CompareTo(a.Score);
        }
    }
}
