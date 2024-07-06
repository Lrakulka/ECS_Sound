using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ECS_Sound.AudioConfiguration
{
    // Singleton
    [DisallowMultipleComponent]
    public class CollisionSoundConfigurationHub : MonoBehaviour
    {
        [Header("All Collision Sound Configuration should be added here to have proper Audio representation")]
        public List<CollisionSoundConfiguration> configurationList = new();
        
        public Dictionary<int, CollisionSoundConfiguration> configurations = new();
        public Dictionary<int, AudioClip> audioClips = new();
        
        private bool isSingleton;
        private static CollisionSoundConfigurationHub instance;
        
        protected void Awake() {
            if (instance != null)
            {
                Debug.LogWarning("Creation more than one CollisionSoundConfigurationHub instances, instance - auto destroyed");
                Destroy(this);
            }
            instance = this;
            isSingleton = true;

            foreach(var configuration in configurationList)
            {
                AddToMap(configuration, ref configurations);
                AddToMap(configuration.touchClip, ref audioClips);
                AddToMap(configuration.slideClip, ref audioClips);
            }
        }

        private static void AddToMap<T>(in T candidate, ref Dictionary<int, T> candidates) where T : Object
        {
            if (candidate == null)
            {
                Debug.LogError($"Found null candidate for unique map {candidates}");
                return;
            }
            var candidateId = GetCandidateId(candidate);
            if (candidates.ContainsKey(candidateId))
            {
                var probableDuplicate = candidates[candidateId];
                if (!probableDuplicate.name.Equals(candidate.name))
                {
                    Debug.LogWarning($"Found duplicate of {candidate.name} hashCode in unique map {candidates}");
                }
                return;
            }

            candidates.Add(candidateId, candidate);
        }

        protected void OnDestroy()
        {
            if (isSingleton)
                instance = null;
        }
        
        public CollisionSoundConfiguration GetConfiguration(int configurationId)
        {
            return configurations[configurationId];
        }
        
        public AudioClip GetAudioClip(int audioClipId)
        {
            return audioClips[audioClipId];
        }
        
        public static int GetConfigurationId(in CollisionSoundConfiguration configuration)
        {
            return GetCandidateId(configuration);
        }
        
        public static int GetAudioClipId(in AudioClip audioClip)
        {
            return GetCandidateId(audioClip);
        }

        private static int GetCandidateId<T>(in T candidate) where T : Object
        {
            return math.abs(candidate.name.GetHashCode());
        }
    }
}
