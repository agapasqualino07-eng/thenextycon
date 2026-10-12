using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using OfficinaTycoon.UnityLayer;

namespace OfficinaTycoon.EditorTools
{
    /// <summary>
    /// Editor helpers. UNVERIFIED: written without access to the Unity Editor, so the first run may need small API fixes.
    /// Menu: Officina > Apply Project Settings / Create Main Scene / Export Default Config JSON / Build Android (APK).
    /// Command line: Unity -batchmode -quit -projectPath . -executeMethod OfficinaTycoon.EditorTools.ProjectTools.BuildAndroidDevelopmentApk
    /// </summary>
    public static class ProjectTools
    {
        // TODO(owner): choose your own unique application id before publishing (cannot be changed after the first Play upload).
        const string PlaceholderApplicationId = "com.example.officinatycoon";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Officina/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "TODO Company";
            PlayerSettings.productName = "Officina Tycoon";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PlaceholderApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PlaceholderApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            Debug.Log("Officina: project settings applied. Replace the placeholder company name and application id before publishing.");
        }

        [MenuItem("Officina/Create Main Scene")]
        public static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Officina: Main scene created at " + ScenePath);
        }

        [MenuItem("Officina/Export Default Config JSON")]
        public static void ExportDefaultConfig()
        {
            Directory.CreateDirectory("Assets/Resources");
            string path = "Assets/Resources/game_config.json";
            File.WriteAllText(path, JsonMapper.ToJson(DefaultConfig.Create()));
            AssetDatabase.Refresh();
            Debug.Log("Officina: default config exported to " + path + ". Edit it to retune costs/probabilities (validated at startup). Delete it to return to code defaults.");
        }

        [MenuItem("Officina/Validate Config")]
        public static void ValidateConfig()
        {
            var errors = ConfigValidator.Validate(ConfigLoader.Load());
            if (errors.Count == 0) Debug.Log("Officina: configuration is valid.");
            else Debug.LogError("Officina: configuration errors:\n - " + string.Join("\n - ", errors));
        }

        [MenuItem("Officina/Build Android (development APK)")]
        public static void BuildAndroidDevelopmentApk()
        {
            ApplyProjectSettings();
            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/OfficinaTycoon-dev.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Officina: build result = " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
