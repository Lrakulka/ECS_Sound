using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace ECS_Sound.Components.Authoring
{
    public class RaySoundColliderAuthoring : MonoBehaviour
    {
        public GameObject owner;
        [Range(0f, 1f)]
        public float minSoundVelocity;
        public LayerMask belongsTo;
        public LayerMask collidesWith;
        public float rayLength = 0.02f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            var start = transform.position;
            var end = start - gameObject.transform.up * rayLength;

            Gizmos.DrawLine(start, end);
            var rotationMatrix = Matrix4x4.TRS(end, transform.rotation, new Vector3(0.1f, 0.01f, 0.1f));
            Gizmos.matrix = rotationMatrix;	
            Gizmos.color = Color.green;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
    }

#if UNITY_EDITOR
    public class RayColliderBaker : Baker<RaySoundColliderAuthoring>
    {
        public override void Bake(RaySoundColliderAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new RaySoundColliderInfoComponent
            (
                GetEntity(authoring.owner, TransformUsageFlags.Renderable),
                authoring.minSoundVelocity,
                authoring.transform.up * authoring.rayLength,
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