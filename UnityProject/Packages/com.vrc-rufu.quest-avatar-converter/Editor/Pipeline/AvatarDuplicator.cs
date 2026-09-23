using System;
using UnityEditor;
using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T014: produces a fully independent Prefab copy of the PC avatar — never a Prefab Variant
    /// (FR-023), so subsequent PC prefab edits never propagate to it — placed at the PC avatar's
    /// position plus <c>ConversionSettings.PlacementOffset</c> (FR-004), saved under
    /// `Assets/&lt;QuestConvertedRoot&gt;/&lt;AvatarName&gt;/Avatar/` (FR-002). Never modifies
    /// <see cref="PCAvatar.RootPrefab"/> itself (Constitution I).
    /// </summary>
    public static class AvatarDuplicator
    {
        /// <summary>Writes to the same fixed, deterministic prefab path on every call for a given
        /// avatar/outputRoot — re-running MUST overwrite prior Quest output in place, never create
        /// a separate versioned copy (FR-022). Callers are responsible for the FR-022 overwrite
        /// confirmation gate (ExistingOutputDetector, T015) BEFORE calling this.</summary>
        public static QuestAvatar Duplicate(PCAvatar sourceAvatar, string outputRoot, Vector3 placementOffset)
        {
            var avatarName = sourceAvatar.RootPrefab.name;
            var avatarFolder = QuestOutputPaths.GetAvatarFolder(outputRoot);
            QuestOutputPaths.EnsureFolder(avatarFolder);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceAvatar.RootPrefab);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate PC avatar prefab '{sourceAvatar.RootPrefab.name}' — is it a valid Prefab asset?");
            }

            try
            {
                // Fully disconnect from the source Prefab (no PrefabInstance/Variant relationship
                // left) before saving as a new asset, per FR-023.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                instance.name = avatarName;
                instance.transform.position = sourceAvatar.RootPrefab.transform.position + placementOffset;

                var assetPath = $"{avatarFolder}/{avatarName}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath, out var success);
                if (!success || savedPrefab == null)
                {
                    throw new InvalidOperationException($"Failed to save Quest avatar prefab at '{assetPath}'.");
                }

                return BuildQuestAvatar(sourceAvatar, savedPrefab);
            }
            finally
            {
                // The pipeline's deliverable is the saved Prefab asset, not a scene instance —
                // leave the user's currently open scene untouched by this stage.
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static QuestAvatar BuildQuestAvatar(PCAvatar sourceAvatar, GameObject savedPrefab)
        {
            var questAvatar = new QuestAvatar { RootPrefab = savedPrefab };

            foreach (var pcRenderer in sourceAvatar.Renderers)
            {
                var questTransform = string.IsNullOrEmpty(pcRenderer.TransformPath)
                    ? savedPrefab.transform
                    : savedPrefab.transform.Find(pcRenderer.TransformPath);

                if (questTransform == null)
                {
                    // The duplicate is an exact hierarchy copy, so every PC TransformPath must
                    // resolve on the Quest side too; this would indicate a duplication defect.
                    continue;
                }

                var questRenderer = questTransform.GetComponent<Renderer>();

                // Right after duplication the Quest-side Renderer's sharedMaterials still point
                // at the original PC Materials (nothing has swapped them yet — that's
                // RendererMaterialReplacer, T027), so the PC-side RendererRef's SourceMaterials
                // list is exactly correct to carry over here too, as data-model.md says
                // ("Mirrors PCAvatar.Renderers 1:1").
                questAvatar.Renderers.Add(new RendererRef(pcRenderer.TransformPath, questRenderer, pcRenderer.SourceMaterials));
            }

            return questAvatar;
        }
    }
}
