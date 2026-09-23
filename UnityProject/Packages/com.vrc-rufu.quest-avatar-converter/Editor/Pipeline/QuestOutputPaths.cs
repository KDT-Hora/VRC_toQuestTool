using UnityEditor;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// FR-002 requires "a single fixed top-level convention folder directly under the project's
    /// `Assets` folder" (data-model.md's `&lt;QuestConvertedRoot&gt;` placeholder) with one
    /// automatically-created subfolder per source avatar underneath it — spec.md/plan.md/
    /// data-model.md deliberately leave the literal folder name as an implementation choice
    /// (never pinned to a concrete string anywhere in those docs). This is that choice, made once
    /// and centralized here so every pipeline stage that writes under OutputRoot agrees on it.
    /// </summary>
    public static class QuestOutputPaths
    {
        /// <summary>The fixed top-level convention folder name directly under `Assets/`.</summary>
        public const string ConvertedRootFolderName = "QuestConverted";

        public const string AvatarSubfolderName = "Avatar";
        public const string MaterialsSubfolderName = "Materials";
        public const string TexturesSubfolderName = "Textures";

        /// <summary>`Assets/&lt;QuestConvertedRoot&gt;/&lt;avatarName&gt;/` (FR-002,
        /// data-model.md's ConversionContext.OutputRoot).</summary>
        public static string GetOutputRoot(string avatarName)
        {
            return $"Assets/{ConvertedRootFolderName}/{avatarName}";
        }

        public static string GetAvatarFolder(string outputRoot) => $"{outputRoot}/{AvatarSubfolderName}";
        public static string GetMaterialsFolder(string outputRoot) => $"{outputRoot}/{MaterialsSubfolderName}";
        public static string GetTexturesFolder(string outputRoot) => $"{outputRoot}/{TexturesSubfolderName}";

        /// <summary>Creates every missing folder along <paramref name="assetFolderPath"/> (e.g.
        /// `Assets/QuestConverted/AvatarName/Materials`), one `AssetDatabase.CreateFolder` level
        /// at a time since it only creates a single new leaf per call.</summary>
        public static void EnsureFolder(string assetFolderPath)
        {
            var segments = assetFolderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }
    }
}
