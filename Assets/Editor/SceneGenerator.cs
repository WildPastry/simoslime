using System;
using System.IO;
using System.Linq;
using SimoSlime.Game;
using SimoSlime.Rules;
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
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string CardPrefabPath = PrefabsFolder + "/Card.prefab";
        private const string ArtFolder = "Assets/Art";
        private const string CirclePath = ArtFolder + "/Circle.png";
        private const string AudioFolder = "Assets/Audio";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ButtonSpritePath = "UI/Skin/UISprite.psd";
        private const string PanelSpritePath = "UI/Skin/Background.psd";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 MenuButtonSize = new Vector2(360f, 96f);
        private static readonly Vector2 CardSize = new Vector2(200f, 280f);
        private const float CardSpacing = 24f;

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

            WriteCircle();
            WriteCardPrefab();
            WriteTitle();
            WriteFight();

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
        private const string ResumeTriesKey = "SimoSlime.GenerateAfterTmpImport.Tries";
        private const int MaxResumeTries = 600;

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
            SessionState.SetInt(ResumeTriesKey, 0);
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
                int tries = SessionState.GetInt(ResumeTriesKey, 0) + 1;
                SessionState.SetInt(ResumeTriesKey, tries);
                if (tries > MaxResumeTries)
                {
                    SessionState.EraseBool(ResumeKey);
                    Run(() => throw new InvalidOperationException($"TMP Essential Resources still missing after {MaxResumeTries} editor ticks: no {FontPath}"));
                    return;
                }

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

        // Layout F1 Corners: the Slime above centre, the player's numbers in the bottom corners,
        // the hand along the bottom, and the result panel (R1) hidden over everything.
        private static void WriteFight()
        {
            Scene scene = NewScene();
            // Loaded after NewScene, which unloads assets nothing in the open scene references.
            CardView cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath)
                ?? throw new InvalidOperationException($"No CardView on {CardPrefabPath}");
            RectTransform canvas = NewCanvas();
            var screen = new GameObject("FightScreen").AddComponent<FightScreen>();
            new GameObject("BackToTitleOnEsc").AddComponent<BackToTitleOnEsc>();

            var enemyAnchor = new Vector2(0.5f, 0.6f);
            var slime = new GameObject("Slime", typeof(RectTransform));
            slime.transform.SetParent(canvas, false);
            Place((RectTransform)slime.transform, enemyAnchor, Vector2.zero, new Vector2(300f, 300f));
            var slimeImage = slime.AddComponent<Image>();
            slimeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            slimeImage.color = Palette.Slime;
            slimeImage.raycastTarget = false;

            var lineSize = new Vector2(400f, 60f);
            TextMeshProUGUI intent = AddText(canvas, "IntentText", "Intent: attack 6", enemyAnchor, new Vector2(0f, 200f), lineSize, 36f, Palette.Text);
            TextMeshProUGUI enemyName = AddText(canvas, "EnemyNameText", "Slime", enemyAnchor, new Vector2(0f, -190f), lineSize, 36f, Palette.Text);
            TextMeshProUGUI enemyHp = AddText(canvas, "EnemyHpText", "HP 30/30", enemyAnchor, new Vector2(0f, -245f), lineSize, 40f, Palette.Hp, bold: true);

            var leftCorner = new Vector2(0f, 0f);
            var rightCorner = new Vector2(1f, 0f);
            var statSize = new Vector2(320f, 60f);
            var pileSize = new Vector2(320f, 50f);
            TextMeshProUGUI energy = AddText(canvas, "EnergyText", "Energy 3", leftCorner, new Vector2(200f, 310f), statSize, 48f, Palette.Text, bold: true);
            TextMeshProUGUI draw = AddText(canvas, "DrawText", "Draw 5", leftCorner, new Vector2(200f, 170f), pileSize, 32f, Palette.Text);
            Button endTurn = AddButton(canvas, "EndTurnButton", "End Turn", rightCorner, new Vector2(-220f, 430f), new Vector2(320f, 90f));
            UnityEventTools.AddPersistentListener(endTurn.onClick, screen.EndTurn);
            TextMeshProUGUI playerHp = AddText(canvas, "PlayerHpText", "HP 40/40", rightCorner, new Vector2(-220f, 310f), statSize, 48f, Palette.Hp, bold: true);
            TextMeshProUGUI block = AddText(canvas, "BlockText", "Block 0", rightCorner, new Vector2(-220f, 250f), statSize, 40f, Palette.Block);
            TextMeshProUGUI discard = AddText(canvas, "DiscardText", "Discard 0", rightCorner, new Vector2(-220f, 170f), pileSize, 32f, Palette.Text);

            // A centred row of cards, as wide as a full hand.
            var hand = new GameObject("Hand", typeof(RectTransform));
            hand.transform.SetParent(canvas, false);
            int handSize = FightConfig.Default().HandSize;
            float handWidth = handSize * CardSize.x + (handSize - 1) * CardSpacing;
            Place((RectTransform)hand.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(handWidth, CardSize.y));
            var row = hand.AddComponent<HorizontalLayoutGroup>();
            row.spacing = CardSpacing;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            ResultPanel result = AddResultPanel(canvas);
            SoundBank sounds = AddSoundBank();

            Wire(screen,
                ("enemyNameText", enemyName), ("enemyHpText", enemyHp), ("intentText", intent),
                ("playerHpText", playerHp), ("blockText", block), ("energyText", energy),
                ("drawText", draw), ("discardText", discard), ("hand", hand.transform),
                ("cardPrefab", cardPrefab), ("endTurnButton", endTurn),
                ("endTurnLabel", endTurn.GetComponentInChildren<TextMeshProUGUI>()), ("resultPanel", result),
                ("sounds", sounds));

            SaveStable(scene, FightPath);
        }

        // One 2D AudioSource that plays the four clips in Assets/Audio.
        private static SoundBank AddSoundBank()
        {
            var go = new GameObject("SoundBank");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var bank = go.AddComponent<SoundBank>();
            Wire(bank, ("source", source), ("hit", Clip("hit")), ("block", Clip("block")), ("win", Clip("win")), ("lose", Clip("lose")));
            return bank;
        }

        private static AudioClip Clip(string name)
        {
            string path = $"{AudioFolder}/{name}.ogg";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path)
                ?? throw new InvalidOperationException($"No audio clip at {path}");
        }

        // Layout R1: a 60% black dim over the whole fight, which also blocks clicks to it, and a
        // centred panel with the message and two stacked buttons. Saved hidden.
        private static ResultPanel AddResultPanel(RectTransform canvas)
        {
            var root = new GameObject("ResultPanel", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var result = root.AddComponent<ResultPanel>();

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(rootRect, false);
            var panelRect = (RectTransform)panel.transform;
            Place(panelRect, Centre, Vector2.zero, new Vector2(720f, 560f));
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(PanelSpritePath);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Palette.Panel;

            TextMeshProUGUI message = AddText(panelRect, "ResultMessage", "You win!", Centre, new Vector2(0f, 140f), new Vector2(680f, 140f), 96f, Palette.Text, bold: true);
            Button playAgain = AddButton(panelRect, "PlayAgainButton", "Play Again", Centre, new Vector2(0f, -60f), MenuButtonSize);
            Button mainMenu = AddButton(panelRect, "MainMenuButton", "Main Menu", Centre, new Vector2(0f, -180f), MenuButtonSize);
            UnityEventTools.AddPersistentListener(playAgain.onClick, result.PlayAgain);
            UnityEventTools.AddPersistentListener(mainMenu.onClick, result.MainMenu);

            Wire(result, ("message", message));
            root.SetActive(false);
            return result;
        }

        // The Card prefab: a button tinted the card colour, with its cost top-left and its name
        // and effect centred below. It shows a Strike until FightScreen fills it in.
        private static void WriteCardPrefab()
        {
            EnsureFolder(PrefabsFolder);
            var go = new GameObject("Card", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = CardSize;

            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(ButtonSpritePath);
            image.type = Image.Type.Sliced;
            image.color = Palette.Card;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = ColorBlock.defaultColorBlock.disabledColor;
            button.colors = colors;

            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            TextMeshProUGUI cost = AddText(rect, "Cost", "1", topLeft, new Vector2(16f, -10f), new Vector2(80f, 50f), 40f, Palette.CardText, bold: true, pivot: topLeft);
            cost.alignment = TextAlignmentOptions.TopLeft;
            TextMeshProUGUI cardName = AddText(rect, "Name", "STRIKE", top, new Vector2(0f, -72f), new Vector2(CardSize.x, 40f), 28f, Palette.CardText, bold: true, pivot: top);
            cardName.alignment = TextAlignmentOptions.Top;
            TextMeshProUGUI effect = AddText(rect, "Effect", "Deal 6", top, new Vector2(0f, -150f), new Vector2(CardSize.x - 24f, 110f), 28f, Palette.CardText, pivot: top);
            effect.alignment = TextAlignmentOptions.Top;
            effect.textWrappingMode = TextWrappingModes.Normal;

            var view = go.AddComponent<CardView>();
            Wire(view, ("button", button), ("nameText", cardName), ("costText", cost), ("effectText", effect));

            PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            StabiliseFile(CardPrefabPath);
        }

        // A white anti-aliased circle, tinted per Image. Written only when its pixels change, so
        // a regenerate leaves the file alone.
        private static void WriteCircle()
        {
            const int size = 256;
            const float radius = size / 2f - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f;
                    float dy = y + 0.5f - size / 2f;
                    float coverage = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            EnsureFolder(ArtFolder);
            string file = Path.GetFullPath(CirclePath);
            if (!File.Exists(file) || !File.ReadAllBytes(file).SequenceEqual(png))
            {
                File.WriteAllBytes(file, png);
            }

            AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
            }
        }

        // Sets a component's [SerializeField] private fields, as dragging into them in the
        // Inspector would.
        private static void Wire(Component component, params (string Field, UnityEngine.Object Value)[] fields)
        {
            var serialized = new SerializedObject(component);
            foreach ((string field, UnityEngine.Object value) in fields)
            {
                SerializedProperty property = serialized.FindProperty(field)
                    ?? throw new InvalidOperationException($"{component.GetType().Name} has no serialized field {field}");
                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Saves, then swaps Unity's random object IDs for stable ones so a regenerate that
        // changes nothing leaves the file byte-for-byte the same.
        private static void SaveStable(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            StabiliseFile(path);
        }

        private static void StabiliseFile(string path)
        {
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
            // Mouse only, plus Esc and Enter handled by each screen: no arrow-key navigation, and
            // Enter never also clicks whichever button was last pressed.
            var module = ObjectFactory.AddComponent<InputSystemUIInputModule>(events);
            module.move = null;
            module.submit = null;

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

        private static TextMeshProUGUI AddText(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, bool bold = false, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, pivot ?? Centre, position, size);

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
            Place(rect, anchor, Centre, position, size);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
