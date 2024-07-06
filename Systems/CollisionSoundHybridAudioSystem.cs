using ECS_Sound.AudioConfiguration;
using ECS_Sound.Components;
using ECS_Sound.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        private NativeList<CollisionInteraction> interactionToPlaySoundList;

        private const int AUDIO_SOURCES_LIST_SIZE = 50;
        private const int INTERACTIONS_PLAY_SOUND_LIST_SIZE = 30;

        public class AudioSourcesHub
        {
            private int currentAudioSourceId;
            private AudioSource[] audioSourcePull;

            public AudioSourcesHub(int size)
            {
                audioSourcePull = new AudioSource[size];
                for (var i = 0; i < audioSourcePull.Length; i++)
                {
                    var companionGameObject = new GameObject($"AudioSource for active CollisionSound {i}");
                    var audioSource = companionGameObject.AddComponent<AudioSource>();
                    audioSourcePull[i] = audioSource;
                }
            }

            public AudioSource GetAudioSource()
            {
                currentAudioSourceId++;
                currentAudioSourceId = currentAudioSourceId == audioSourcePull.Length ? 0 : currentAudioSourceId;
                return audioSourcePull[currentAudioSourceId];
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
            interactionToPlaySoundList =
                new NativeList<CollisionInteraction>(INTERACTIONS_PLAY_SOUND_LIST_SIZE, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (audioClipLengthMap.IsCreated) audioClipLengthMap.Dispose();
            if (audioSourcePitchMap.IsCreated) audioSourcePitchMap.Dispose();
            if (interactionToPlaySoundList.IsCreated) interactionToPlaySoundList.Dispose();
        }

        protected override void OnUpdate()
        {
            if (configurationsHub == null)
            {
                // Object CollisionSoundConfigurationHub is not found on Scene load by another Scene in OnCreate()
                var configurationsHubGameObject = GameObject.Find("CollisionSoundConfigurationHub");
                if (configurationsHubGameObject == null)
                {
                    Debug.LogWarning("CollisionSoundConfigurationHub not found, sound not initialized");
                    return;
                }
                configurationsHub = configurationsHubGameObject.GetComponent<CollisionSoundConfigurationHub>();
                Initialize();
            }
            
            // Play actual sound
            foreach (var interaction in interactionToPlaySoundList)
            {
                HybridAudioUtils
                    .PlayClipSound(ref audioSourcesHub, interaction.ConfigurationId,
                        interaction.MainClipId, interaction.VolumeScale, interaction.AverageContactPoint, configurationsHub);
                if (interaction.SecondaryClipId != 0)
                {
                    HybridAudioUtils
                        .PlayClipSound(ref audioSourcesHub, interaction.ConfigurationId,
                            interaction.SecondaryClipId, interaction.VolumeScale, interaction.AverageContactPoint, configurationsHub);
                }
            }
            
            interactionToPlaySoundList.Clear();
            var localAudioClipLengthMap = audioClipLengthMap;
            var localAudioSourcePitchMap = audioSourcePitchMap;
            var localInteractionToPlaySoundList = interactionToPlaySoundList;
            
            Entities
                .WithStoreEntityQueryInField(ref entityQuery)
                .WithReadOnly(localAudioClipLengthMap)
                .WithReadOnly(localAudioSourcePitchMap)
                .WithChangeFilter<ActiveSoundSourceComponent>()
                .ForEach((Entity entity, ref ActiveSoundSourceComponent activeSoundSource,
                    ref CollisionSoundInteractionsComponent interactions, in LocalToWorld localToWorld) =>
                {
                    for (var interactionId = 0; interactionId < interactions.Length; interactionId++)
                    {
                        if (!activeSoundSource.IsPlaySound(interactionId)) continue;
                        
                        var interaction = interactions[interactionId];

                        if (localInteractionToPlaySoundList.Length != INTERACTIONS_PLAY_SOUND_LIST_SIZE)
                        {
                            localInteractionToPlaySoundList.Add(interaction);
                        }
                        else
                        {
                            Debug.LogWarning("Not enough space in AudioInteractionList, the interaction dropped");
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
                }).Schedule();
        }
    }
}
