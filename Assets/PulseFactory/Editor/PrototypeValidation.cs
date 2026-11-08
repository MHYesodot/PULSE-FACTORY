using System;
using System.Linq;
using PulseFactory.Core;
using PulseFactory.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PulseFactory.Editor
{
    public static class PrototypeValidation
    {
        private const string TutorialPath = "Assets/Scenes/Tutorial.unity";

        [MenuItem("Pulse Factory/Validate Tutorial Scene")]
        public static void ValidateTutorial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before validating the authored scene.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close modified scenes before validation.");
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == TutorialPath))
                throw new InvalidOperationException("Tutorial is missing from enabled build scenes.");
            var scene = EditorSceneManager.OpenScene(TutorialPath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            if (UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsSortMode.None).Length != 1
                || Camera.main == null)
                throw new InvalidOperationException("Tutorial requires exactly one controller and a main camera.");
            var data = Resources.Load<TextAsset>("Levels/tutorial");
            if (data == null) throw new InvalidOperationException("Missing tutorial resource.");
            LevelJson.Parse(data.text);
            Debug.Log("Tutorial scene validation passed: build reference, components, camera, and level JSON.");
        }

        [MenuItem("Pulse Factory/Play Tutorial")]
        public static void PlayTutorial()
        {
            ValidateTutorial();
            EditorApplication.isPlaying = true;
        }

        public static void BuildMacValidationPlayer()
        {
            ValidateTutorial();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TutorialPath },
                locationPathName = "Builds/macOS/PulseFactory.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("macOS validation build failed: " + report.summary.result);
            Debug.Log("macOS validation build succeeded: " + report.summary.outputPath);
        }
    }
}
