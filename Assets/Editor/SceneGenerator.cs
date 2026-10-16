using UnityEditor;

namespace SimoSlime.Editor
{
    /// <summary>
    /// Writes every generated asset, so the scenes and settings come from code rather than hand
    /// edits. Batchmode entry point for <c>tools/unity.ps1 -Task generate</c>. For now it only
    /// applies the project config; the Title and Fight scenes arrive with the title screen.
    /// </summary>
    public static class SceneGenerator
    {
        [MenuItem("SimoSlime/Generate Scenes")]
        public static void GenerateAll()
        {
            ProjectConfig.Apply();
        }
    }
}
