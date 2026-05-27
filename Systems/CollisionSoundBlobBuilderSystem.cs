using System;
using ECS_Sound.AudioConfiguration;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS_Sound.Systems
{
    /// <summary>
    /// Builds the <see cref="CollisionSoundBlobReference"/> singleton from the
    /// <see cref="CollisionSoundConfigurationAsset"/> once the asset is reachable, then disables itself.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CollisionSoundBlobBuilderSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (SystemAPI.HasSingleton<CollisionSoundBlobReference>())
            {
                Enabled = false;
                return;
            }

            var asset = CollisionSoundConfigurationHub.LoadAsset();
            if (asset == null) return; // Try again next frame.

            var blobRef = BuildBlob(asset);
            var entity = EntityManager.CreateEntity(typeof(CollisionSoundBlobReference));
            EntityManager.SetName(entity, "CollisionSoundBlobReference");
            EntityManager.SetComponentData(entity, new CollisionSoundBlobReference { Blob = blobRef });

            Enabled = false;
        }

        private static BlobAssetReference<CollisionSoundBlobData> BuildBlob(CollisionSoundConfigurationAsset asset)
        {
            var configurations = asset.Configurations;
            var count = configurations.Count;

            var sortedIds = new int[count];
            var sortedConfigs = new CollisionSoundConfiguration[count];
            int i = 0;
            foreach (var kv in configurations)
            {
                sortedIds[i] = kv.Key;
                sortedConfigs[i] = kv.Value;
                i++;
            }
            Array.Sort(sortedIds, sortedConfigs);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CollisionSoundBlobData>();

            var configArray = builder.Allocate(ref root.Configs, count);
            var idArray = builder.Allocate(ref root.ConfigIds, count);

            for (int idx = 0; idx < count; idx++)
            {
                var c = sortedConfigs[idx];
                idArray[idx] = sortedIds[idx];
                configArray[idx] = new CollisionSoundConfigBlob
                {
                    ConfigurationId = sortedIds[idx],
                    TouchPitchAbs = math.abs(c.touch.pitch),
                    SlidePitchAbs = math.abs(c.slide.pitch),
                };
            }

            root.SoundPriorityRadiusSq = asset.SoundPriorityRadiusSq;
            root.SoundIgnoreRadiusSq = asset.SoundIgnoreRadiusSq;
            root.AudioSourcePoolSize = asset.AudioSourcePoolSize;

            return builder.CreateBlobAssetReference<CollisionSoundBlobData>(Allocator.Persistent);
        }
    }
}
