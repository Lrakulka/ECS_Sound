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
        public float rayLength = 0.02f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;

            var start = transform.position;
            var end = start + Vector3.down * rayLength;

            Gizmos.DrawLine(start, end);
            Gizmos.color = Color.green;
            Gizmos.DrawCube(end, new Vector3(0.01f, 0.001f, 0.01f));
        }
    }

#if UNITY_EDITOR
    public class RayColliderBaker : Baker<RayColliderAuthoring>
    {
        public override void Bake(RayColliderAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new RayColliderInfoComponent
            (
                GetEntity(authoring.owner, TransformUsageFlags.Renderable),
                authoring.minSoundVelocity,
                authoring.rayLength,
                new CollisionFilter
                {
                    BelongsTo = (uint) authoring.belongsTo.value,
                    CollidesWith = (uint) authoring.collidesWith.value,
                    GroupIndex = 0
                }
            ));
        }
    }
#endif
}