using ECS_Common.Utils;
using ECS_Sound.AudioConfiguration;
using ECS_Sound.Components;
using ECS_Sound.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS_Sound.Systems
{
    [UpdateInGroup(typeof(CollisionSoundSystemGroup))]
    public partial class CollisionSoundHybridAudioSystem : SystemBase
    {
        private EntityQuery entityQuery;
        private CollisionSoundConfigurationHub configurationsHub;
        
        private NativeParallelHashMap<int, float> audioClipLengthMap;
        private NativeParallelHashMap<int, float> audioSourcePitchMap;

        private AudioSourcesHub audioSourcesHub;
        private NativeList<CollisionInteraction> touchPriorityInteractionToPlaySoundList;
        private NativeList<CollisionInteraction> slidePriorityInteractionToPlaySoundList;
        private NativeList<CollisionInteraction> touchInteractionToPlaySoundList;
        private NativeList<CollisionInteraction> slideInteractionToPlaySoundList;
        private NativeArray<float3> audioListenerPositions;
        private Transform[] audioListenerTransforms;

        private const int AUDIO_SOURCES_LIST_SIZE = 64; // should be more or equal to CollectionHelper.CacheLineSize (64)
        private const double SOUND_PRIORITY_RADIUS_SQ = 100;

        public class AudioSourcesHub
        {
            private uint currentAudioSourceId;
            private AudioSource[] audioSourcePull;

            public AudioSourcesHub(int size)
            {
                audioSourcePull = new AudioSource[size];
                for (var i = 0; i < audioSourcePull.Length; i++)
                {
                    var companionGameObject = new GameObject($"AudioSource for active CollisionSound {i}");
                    var audioSource = companionGameObject.AddComponent<AudioSource>();
                    companionGameObject.AddComponent<AutoDisablePlayGameObject>();
                    audioSourcePull[i] = audioSource;
                }
            }

            public AudioSource GetAudioSource()
            {
                currentAudioSourceId++;
                return audioSourcePull[currentAudioSourceId % audioSourcePull.Length];
            }
        }

        private void Initialize()
        {
            audioClipLengthMap = new NativeParallelHashMap<int, float>(configurationsHub.audioClips.Count, Allocator.Persistent);
            audioSourcePitchMap = new NativeParallelHashMap<int, float>(configurationsHub.configurations.Count, Allocator.Persistent);
            
            foreach (var audioClipKeyValue in configurationsHub.audioClips)
            {
                audioClipLengthMap.Add(audioClipKeyValue.Key, audioClipKeyValue.Value.length);
            }
            foreach (var configurationKeyValue in configurationsHub.configurations)
            {
                audioSourcePitchMap.Add(configurationKeyValue.Key, math.abs(configurationKeyValue.Value.pitch));
            }

            audioSourcesHub = new AudioSourcesHub(AUDIO_SOURCES_LIST_SIZE);
            touchPriorityInteractionToPlaySoundList =
                new NativeList<CollisionInteraction>(AUDIO_SOURCES_LIST_SIZE, Allocator.Persistent);
            slidePriorityInteractionToPlaySoundList =
                new NativeList<CollisionInteraction>(AUDIO_SOURCES_LIST_SIZE, Allocator.Persistent);
            touchInteractionToPlaySoundList =
                new NativeList<CollisionInteraction>(AUDIO_SOURCES_LIST_SIZE, Allocator.Persistent);
            slideInteractionToPlaySoundList =
                new NativeList<CollisionInteraction>(AUDIO_SOURCES_LIST_SIZE, Allocator.Persistent);

            var audioListeners = Object.FindObjectsOfType<AudioListener>(false);
            audioListenerTransforms = new Transform[audioListeners.Length];
            audioListenerPositions = new NativeArray<float3>(audioListeners.Length, Allocator.Persistent);
            for (var i = 0; i < audioListenerTransforms.Length; i++)
            {
                audioListenerTransforms[i] = audioListeners[i].transform;
            }
        }

        protected override void OnDestroy()
        {
            if (audioClipLengthMap.IsCreated) audioClipLengthMap.Dispose();
            if (audioSourcePitchMap.IsCreated) audioSourcePitchMap.Dispose();
            if (audioListenerPositions.IsCreated) audioListenerPositions.Dispose();
            if (touchPriorityInteractionToPlaySoundList.IsCreated) touchPriorityInteractionToPlaySoundList.Dispose();
            if (slidePriorityInteractionToPlaySoundList.IsCreated) slidePriorityInteractionToPlaySoundList.Dispose();
            if (touchInteractionToPlaySoundList.IsCreated) touchInteractionToPlaySoundList.Dispose();
            if (slideInteractionToPlaySoundList.IsCreated) slideInteractionToPlaySoundList.Dispose();
        }

        protected override void OnUpdate()
        {
            if (configurationsHub == null)
            {
                // Object CollisionSoundConfigurationHub is not found on Scene load by another Scene in OnCreate()
                var configurationsHubGameObject = GameObject.Find("CollisionSoundConfigurationHub");
                if (configurationsHubGameObject == null)
                {
#if UNITY_EDITOR
                    // Throw a lot in profiler. Eat performance.
                    Debug.LogWarning("CollisionSoundConfigurationHub not found, sound not initialized");
#endif
                    return;
                }
                configurationsHub = configurationsHubGameObject.GetComponent<CollisionSoundConfigurationHub>();
                Initialize();
            }

            PlayActualSound();
            
            touchPriorityInteractionToPlaySoundList.Clear();
            slidePriorityInteractionToPlaySoundList.Clear();
            touchInteractionToPlaySoundList.Clear();
            slideInteractionToPlaySoundList.Clear();
            
            for (var i = 0; i < audioListenerTransforms.Length; i++)
            {
                audioListenerPositions[i] = audioListenerTransforms[i].position;
            }

            var localAudioClipLengthMap = audioClipLengthMap;
            var localAudioSourcePitchMap = audioSourcePitchMap;
            var localAudioListenerPositions = audioListenerPositions;
            var localTouchPriorityInteractionToPlaySoundList = touchPriorityInteractionToPlaySoundList;
            var localSlidePriorityInteractionToPlaySoundList = slidePriorityInteractionToPlaySoundList;
            var localTouchInteractionToPlaySoundList = touchInteractionToPlaySoundList;
            var localSlideInteractionToPlaySoundList = slideInteractionToPlaySoundList;
            
            Entities
                .WithStoreEntityQueryInField(ref entityQuery)
                .WithReadOnly(localAudioClipLengthMap)
                .WithReadOnly(localAudioSourcePitchMap)
                .WithReadOnly(localAudioListenerPositions)
                .WithChangeFilter<ActiveSoundSourceComponent>()
                .ForEach((Entity entity, ref ActiveSoundSourceComponent activeSoundSource,
                    ref CollisionSoundInteractionsComponent interactions, in LocalToWorld localToWorld) =>
                {
                    for (var interactionId = 0; interactionId < interactions.Length; interactionId++)
                    {
                        if (!activeSoundSource.IsPlaySound(interactionId)) continue;

                        var distanceSq = double.MinValue;
                        foreach (var listenerPosition in localAudioListenerPositions)
                        {
                            distanceSq = math.min(distanceSq, math.distancesq(listenerPosition, localToWorld.Position));
                        }
                        
                        var interaction = interactions[interactionId];
                        if (distanceSq < SOUND_PRIORITY_RADIUS_SQ)
                        {
                            if (interaction.IsSliding)
                                localSlidePriorityInteractionToPlaySoundList.Add(interaction);
                            else
                                localTouchPriorityInteractionToPlaySoundList.Add(interaction);
                        }
                        else
                        {
                            if (interaction.IsSliding)
                                localSlideInteractionToPlaySoundList.Add(interaction);
                            else
                                localTouchInteractionToPlaySoundList.Add(interaction);
                        }
                        
                        var maxClipDuration = HybridAudioUtils.GetAudioClipLength(
                            localAudioClipLengthMap[interaction.MainClipId],
                            localAudioSourcePitchMap[interaction.ConfigurationId]
                        );
                        if (interaction.SecondaryClipId != 0)
                        {
                            maxClipDuration = math.max(
                                maxClipDuration,
                                HybridAudioUtils.GetAudioClipLength(
                                    localAudioClipLengthMap[interaction.SecondaryClipId],
                                    localAudioSourcePitchMap[interaction.ConfigurationId]
                                )
                            );
                        }

                        interaction.PlayClipEndTime = interaction.UpdatedTime + maxClipDuration;
                        interactions[interactionId] = interaction;
                    } 
                    
                    activeSoundSource.interactionsIds = 0;
                })
                .WithName("CollisionSoundHybridAudioCollectionJob")
                .Schedule();
        }

        private void PlayActualSound()
        {
#if UNITY_EDITOR
            if (touchPriorityInteractionToPlaySoundList.Length > AUDIO_SOURCES_LIST_SIZE)
                // Throw a lot in profiler. Eat performance.
                Debug.LogWarning("Not enough space in AudioInteractionList, the last interactions will be dropped");
#endif
            void PlayInteraction(in CollisionInteraction interaction)
            {
                var isAudioSource = HybridAudioUtils
                    .PlayClipSound(ref audioSourcesHub, interaction.ConfigurationId, interaction.MainClipId,
                        interaction.AverageContactPoint, interaction.VolumeScale, configurationsHub, out var audioSource);
                if (interaction.SecondaryClipId != 0 && isAudioSource)
                {
                    audioSource.PlayOneShot(configurationsHub.GetAudioClip(interaction.SecondaryClipId));
                }
            }
            
            var audioSourceCount = 0;
            for (var interactionId = 0; 
                interactionId < touchPriorityInteractionToPlaySoundList.Length && audioSourceCount < AUDIO_SOURCES_LIST_SIZE; 
                interactionId++, audioSourceCount++)
            {
                PlayInteraction(touchPriorityInteractionToPlaySoundList[interactionId]);
            }
            for (var interactionId = 0; 
                interactionId < slidePriorityInteractionToPlaySoundList.Length && audioSourceCount < AUDIO_SOURCES_LIST_SIZE; 
                interactionId++, audioSourceCount++)
            {
                PlayInteraction(slidePriorityInteractionToPlaySoundList[interactionId]);
            }
            for (var interactionId = 0; 
                interactionId < touchInteractionToPlaySoundList.Length && audioSourceCount < AUDIO_SOURCES_LIST_SIZE; 
                interactionId++, audioSourceCount++)
            {
                PlayInteraction(touchInteractionToPlaySoundList[interactionId]);
            }
            for (var interactionId = 0; 
                interactionId < slideInteractionToPlaySoundList.Length && audioSourceCount < AUDIO_SOURCES_LIST_SIZE; 
                interactionId++, audioSourceCount++)
            {
                PlayInteraction(slideInteractionToPlaySoundList[interactionId]);
            }
        }
    }
}
