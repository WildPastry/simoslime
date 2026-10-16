using System;
using System.IO;
using SimoSlime.Game;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SimoSlime.Editor
{
    /// <summary>
    /// Writes every generated asset, so the scenes and settings come from code rather than hand
    /// edits; anything changed by hand in a generated scene is overwritten on the next run.
    /// Batchmode entry point for <c>tools/unity.ps1 -Task generate</c>. Positions are the
    /// design-gate picks, in units of the 1920x1080 reference canvas.
    /// </summary>
    public static class SceneGenerator
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string TitlePath = ScenesFolder + "/Title.unity";
        private const string FightPath = ScenesFolder + "/Fight.unity";
        private const string SampleScenePath = ScenesFolder + "/SampleScene.unity";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ButtonSpritePath = "UI/Skin/UISprite.psd";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 MenuButtonSize = new Vector2(360f, 96f);

        private static TMP_FontAsset font;

        /// <summary>
        /// In batchmode this exits the editor itself (0 or 1), because importing TMP Essential
        /// Resources finishes on a later editor tick: run it without <c>-quit</c>.
        /// </summary>
        [MenuItem("SimoSlime/Generate Scenes")]
        public static void GenerateAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Run(() =>
            {
                ProjectConfig.Apply();
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null)
                {
                    ImportTmpResourcesThen();
                }
                else
                {
                    WriteEverything();
                }
            });
        }

        private static void WriteEverything()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                throw new InvalidOperationException($"TMP Essential Resources are missing: no {FontPath}");
            }

            WriteTitle();
            WriteFight();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) != null)
            {
                AssetDatabase.DeleteAsset(SampleScenePath);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitlePath, true),
                new EditorBuildSettingsScene(FightPath, true),
            };
            AssetDatabase.SaveAssets();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
            else
            {
                EditorSceneManager.OpenScene(TitlePath);
            }
        }

        // The package import completes on a later editor tick and can reload the script domain,
        // which drops every static and event subscription. A SessionState flag survives the
        // reload, so generation resumes from whichever comes first: the import callback, or
        // ResumeAfterReload once scripts are back. Clearing the flag stops it running twice.
        private const string ResumeKey = "SimoSlime.GenerateAfterTmpImport";

        private static void ImportTmpResourcesThen()
        {
            string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly).resolvedPath
                + "/Package Resources/TMP Essential Resources.unitypackage";

            void Completed(string name)
            {
                Unhook();
                Resume();
            }

            void Failed(string name, string error)
            {
                Unhook();
                SessionState.EraseBool(ResumeKey);
                Run(() => throw new InvalidOperationException($"Importing {name} failed: {error}"));
            }

            void Unhook()
            {
                AssetDatabase.importPackageCompleted -= Completed;
                AssetDatabase.importPackageFailed -= Failed;
            }

            SessionState.SetBool(ResumeKey, true);
            AssetDatabase.importPackageCompleted += Completed;
            AssetDatabase.importPackageFailed += Failed;
            UnityEditor.AssetPackage.Package.Import(package, false);
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (SessionState.GetBool(ResumeKey, false))
            {
                EditorApplication.delayCall += Resume;
            }
        }

        private static void Resume()
        {
            if (!SessionState.GetBool(ResumeKey, false))
            {
                return;
            }

            // The reload can land before the font finishes importing; wait for it.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null && !EditorApplication.isUpdating)
            {
                AssetDatabase.Refresh();
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) == null)
            {
                EditorApplication.delayCall += Resume;
                return;
            }

            SessionState.EraseBool(ResumeKey);
            Run(WriteEverything);
        }

        // Any failure exits a batchmode editor with code 1 instead of leaving it running.
        private static void Run(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void WriteTitle()
        {
            Scene scene = NewScene();
            RectTransform canvas = NewCanvas();
            var screen = new GameObject("TitleScreen").AddComponent<TitleScreen>();

            AddText(canvas, "TitleText", "SimoSlime", Centre, new Vector2(0f, 190f), new Vector2(1200f, 200f), 150f, Palette.Text, bold: true);
            Button play = AddButton(canvas, "PlayButton", "Play", Centre, new Vector2(0f, -50f), MenuButtonSize);
            Button quit = AddButton(canvas, "QuitButton", "Quit", Centre, new Vector2(0f, -170f), MenuButtonSize);
            UnityEventTools.AddPersistentListener(play.onClick, screen.Play);
            UnityEventTools.AddPersistentListener(quit.onClick, screen.Quit);

            SaveStable(scene, TitlePath);
        }

        private static void WriteFight()
        {
            Scene scene = NewScene();
            RectTransform canvas = NewCanvas();
            new GameObject("BackToTitleOnEsc").AddComponent<BackToTitleOnEsc>();

            AddText(canvas, "PlaceholderText", "Fight goes here. Esc for menu.", Centre, Vector2.zero, new Vector2(1400f, 120f), 48f, Palette.Text);

            SaveStable(scene, FightPath);
        }

        // Saves, then swaps Unity's random object IDs for stable ones so a regenerate that
        // changes nothing leaves the file byte-for-byte the same.
        private static void SaveStable(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            string file = Path.GetFullPath(path);
            File.WriteAllText(file, SceneIds.Stabilise(File.ReadAllText(file)));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // A scene with the shared camera and event system.
        private static Scene NewScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            cameraObject.AddComponent<AudioListener>();

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            ObjectFactory.AddComponent<InputSystemUIInputModule>(events);

            return scene;
        }

        // Screen-space canvas scaled by height (design pick W1), so 21:9 only adds width.
        private static RectTransform NewCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, position, size);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        // Unity's stock "Button - TextMeshPro", sized and relabelled.
        private static Button AddButton(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>(ButtonSpritePath),
            };
            GameObject go = TMP_DefaultControls.CreateButton(resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, position, size);

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = 40f;

            // uGUI's stock disabled tint, (200,200,200) at half alpha, as drawn on the design sheet;
            // TMP's factory would otherwise leave an opaque mid grey.
            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.disabledColor = ColorBlock.defaultColorBlock.disabledColor;
            button.colors = colors;
            return button;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Centre;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
