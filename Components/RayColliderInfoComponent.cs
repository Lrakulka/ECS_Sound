using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ECS_Sound.Components
{
    public struct RayColliderInfoComponent : IComponentData
    {
        public readonly Entity Owner;
        public readonly float MinSoundVelocity;
        public readonly float3 RayLength;
        public readonly CollisionFilter Filter;

        public RayColliderInfoComponent(Entity owner, float minSoundVelocity, float rayLength, CollisionFilter filter)
        {
            Filter = filter;
            MinSoundVelocity = minSoundVelocity;
            RayLength = math.down() * rayLength;
            Owner = owner;
        }
    }
}
