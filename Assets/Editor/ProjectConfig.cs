using UnityEditor;
using UnityEngine;

namespace SimoSlime.Editor
{
    /// <summary>
    /// The Player settings the game ships with. The version string lives here and nowhere else;
    /// tools/build.ps1 reads it back from ProjectSettings.asset after a generate.
    /// </summary>
    public static class ProjectConfig
    {
        public const string Version = "0.0.1";

        [MenuItem("SimoSlime/Apply Project Config")]
        public static void Apply()
        {
            PlayerSettings.companyName = "WildPastry";
            PlayerSettings.productName = "SimoSlime";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SplashScreen.show = true;
            AssetDatabase.SaveAssets();
        }
    }
}
