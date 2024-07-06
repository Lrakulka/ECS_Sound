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
            var collisionSoundConfigurations = Object.FindObjectsOfType<CollisionSoundConfigurationHub>();
            if (collisionSoundConfigurations.Length != 1)
            {
                // Build throw warning because CollisionSoundConfigurationHub is not exist yet (Ignore)
                Debug.LogWarning($"Incorrect number of singleton CollisionSoundConfigurationHub {collisionSoundConfigurations}");
                return;
            }
            var configurations = collisionSoundConfigurations[0].configurationList;
            if (!configurations.Contains(configuration))
            {
                configurations.Add(configuration);
            }
        }
    }
}
