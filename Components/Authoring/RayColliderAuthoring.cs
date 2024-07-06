using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace ECS_Sound.Components.Authoring
{
    public class RayColliderAuthoring : MonoBehaviour
    {
        public GameObject owner;
        [Range(0f, 1f)]
        public float minSoundVelocity;
        public LayerMask belongsTo;
        public LayerMask collidesWith;
    }

    public class RayColliderBaker : Baker<RayColliderAuthoring>
    {
        public override void Bake(RayColliderAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new RayColliderInfoComponent
            (
                GetEntity(authoring.owner, TransformUsageFlags.Renderable),
                authoring.minSoundVelocity,
                new CollisionFilter
                {
                    BelongsTo = (uint) authoring.belongsTo.value,
                    CollidesWith = (uint) authoring.collidesWith.value,
                    GroupIndex = 0
                }
            ));
        }
    }
}