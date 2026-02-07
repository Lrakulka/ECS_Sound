using ECS_Sound.AudioConfiguration;
using Unity.Entities;
using UnityEngine;

namespace ECS_Sound.Components.Authoring
{
    public class CollisionSoundAuthoring : MonoBehaviour
    {
        public bool isMakingSound;
        [Space(5)] [Header("Sound configuration for collided object")]
        public CollisionSoundConfiguration configuration;
    }

#if UNITY_EDITOR
    public class CollisionSoundBaker : Baker<CollisionSoundAuthoring>
    {
        public override void Bake(CollisionSoundAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (authoring.isMakingSound)
            {
                AddComponent<ActiveSoundSourceComponent>(entity);
                AddComponent<CollisionSoundInteractionsComponent>(entity);
            }

            if (!authoring.configuration|| !authoring.configuration.touchClip || !authoring.configuration.slideClip)
                Debug.LogError($"{authoring.gameObject} has incorrect collision sound configuration");
            AddComponent(entity, new CollisionSoundComponent
                (
                    CollisionSoundConfigurationHub.GetAudioClipId(authoring.configuration.touchClip),
                    CollisionSoundConfigurationHub.GetAudioClipId(authoring.configuration.slideClip),
                    CollisionSoundConfigurationHub.GetConfigurationId(authoring.configuration)
                )
            );

            AddConfiguration(authoring.configuration);
        }
        
        // Add Sound Source configuration to CollisionSoundConfigurationHub
        private static void AddConfiguration(CollisionSoundConfiguration configuration)
        {
            var configurations = CollisionSoundConfigurationHub.GetInstance().configurationList;
            if (!configurations.Contains(configuration))
            {
                configurations.Add(configuration);
            }
        }
    }
#endif
}
