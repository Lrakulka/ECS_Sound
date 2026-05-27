using ECS_Sound.AudioConfiguration;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECS_Sound.Components.Authoring
{
    public class CollisionSoundAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("isMakingSound")] 
        public bool isActiveSoundSource;

        [Space(5)] [Header("Sound configuration for collided object")]
        public CollisionSoundConfiguration configuration;
    }

#if UNITY_EDITOR
    public class CollisionSoundBaker : Baker<CollisionSoundAuthoring>
    {
        public override void Bake(CollisionSoundAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (authoring.isActiveSoundSource)
            {
                AddComponent<ActiveSoundSourceComponent>(entity);
                AddComponent<CollisionSoundInteractionsComponent>(entity);
            }

            if (!authoring.configuration)
            {
                Debug.LogError($"{authoring.gameObject}: missing CollisionSoundConfiguration");
                return;
            }

            // Asset references for incremental baking. Clip arrays are checked at runtime by the
            // audio system; we don't fail the bake here so partially-authored content can still iterate.
            DependsOn(authoring.configuration);
            if (authoring.configuration.touchClips != null)
                foreach (var c in authoring.configuration.touchClips) DependsOn(c);
            if (authoring.configuration.slideClips != null)
                foreach (var c in authoring.configuration.slideClips) DependsOn(c);

            AddComponent(entity, new CollisionSoundComponent(authoring.configuration.Id));
        }
    }
#endif
}
