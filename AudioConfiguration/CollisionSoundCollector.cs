#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ECS_Sound.AudioConfiguration.Editor
{
    public static class CollisionSoundCollector
    {
        [MenuItem("Tools/ECS Sound/Collect Configurations Into Selected Asset")]
        public static void CollectIntoSelected()
        {
            var target = Selection.activeObject as CollisionSoundConfigurationAsset;
            if (target == null)
            {
                Debug.LogError("Select a CollisionSoundConfigurationAsset in the Project window first.");
                return;
            }
            Collect(target);
        }
        
        [MenuItem("Tools/ECS Sound/Assign GUIDs To All Configurations")]
        public static void AssignGuidsToAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(CollisionSoundConfiguration)}");
            int touched = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CollisionSoundConfiguration>(path);
                if (config == null) continue;
                if (string.IsNullOrEmpty(config.Guid))
                {
                    EditorUtility.SetDirty(config); // Triggers OnValidate, which assigns the GUID.
                    touched++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned GUIDs to {touched} configuration(s) (of {guids.Length} scanned).");
        }

        private static void Collect(CollisionSoundConfigurationAsset target)
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(CollisionSoundConfiguration)}");
            var added = 0;
            var seen = new HashSet<int>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CollisionSoundConfiguration>(path);
                if (config == null) continue;

                ValidateConfig(config, path);

                if (!seen.Add(config.Id))
                {
                    Debug.LogError(
                        $"Duplicate ID on '{config.name}' (path: {path}). " +
                        "Re-import this asset to regenerate its GUID.", config);
                    continue;
                }
                if (target.EditorAddConfiguration(config)) added++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Collected {added} new configuration(s) into '{target.name}'. Total scanned: {guids.Length}.",
                target);
        }

        private static void ValidateConfig(CollisionSoundConfiguration config, string path)
        {
            if (config.touchClips == null || config.touchClips.Length == 0)
                Debug.LogWarning($"'{config.name}' ({path}) has no touchClips.", config);
            if (config.slideClips == null || config.slideClips.Length == 0)
                Debug.LogWarning($"'{config.name}' ({path}) has no slideClips.", config);
        }
    }
}
#endif
