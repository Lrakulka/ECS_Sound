using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ECS_Sound.Components
{
    public struct RaySoundColliderInfoComponent : IComponentData
    {
        public readonly Entity Owner;
        public readonly float MinSoundVelocity;
        public readonly float3 RayPath;
        public readonly CollisionFilter Filter;

        public RaySoundColliderInfoComponent(Entity owner, float minSoundVelocity, float3 rayPath, CollisionFilter filter)
        {
            Filter = filter;
            MinSoundVelocity = minSoundVelocity;
            RayPath = rayPath;
            Owner = owner;
        }
    }
}
