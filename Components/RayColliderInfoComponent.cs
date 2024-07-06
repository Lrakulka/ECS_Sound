using Unity.Entities;
using Unity.Physics;

namespace ECS_Sound.Components
{
    public struct RayColliderInfoComponent : IComponentData
    {
        public readonly Entity Owner;
        public readonly float MinSoundVelocity;
        public readonly CollisionFilter Filter;

        public RayColliderInfoComponent(Entity owner, float minSoundVelocity, CollisionFilter filter)
        {
            Filter = filter;
            MinSoundVelocity = minSoundVelocity;
            Owner = owner;
        }
    }
}
