#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SimoSlime.Game.Dev
{
    /// <summary>
    /// Drives a Development build from the command line so a screen can be proved without a
    /// human: <c>SimoSlime.exe -autoplay title -shots Logs/shots/title</c>. It logs
    /// "AUTOPLAY PASS name" or "AUTOPLAY FAIL name: reason" and quits with 0 or 1. Any logged
    /// exception fails the run. <c>-shotscale 2</c> captures at twice the window size, for
    /// resolutions bigger than the display. Not compiled into release builds.
    /// </summary>
    public class DevAutoplay : MonoBehaviour
    {
        private const float TimeoutSeconds = 30f;

        private string script;
        private string shotsFolder;
        private int shotScale = 1;
        private float deadline;
        private string failureMessage;
        private bool finished;
        private readonly List<string> soundsPlayed = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string script = Arg("-autoplay");
            if (script == null)
            {
                return;
            }

            // Autoplay runs while the human works: muted (the SOUND log lines prove each play), and
            // kept running when its window loses focus, which would otherwise pause the player.
            AudioListener.volume = 0f;
            Application.runInBackground = true;
            var go = new GameObject("DevAutoplay");
            DontDestroyOnLoad(go);
            DevAutoplay autoplay = go.AddComponent<DevAutoplay>();
            autoplay.script = script;
            autoplay.shotsFolder = Arg("-shots") ?? "shots";
            if (int.TryParse(Arg("-shotscale"), out int scale) && scale > 1)
            {
                autoplay.shotScale = scale;
            }
        }

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(shotsFolder);
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!SplashScreen.isFinished)
            {
                yield return null;
            }

            IEnumerator run = Script(script);
            if (run == null)
            {
                Finish($"no script named '{script}'");
                yield break;
            }

            yield return run;
            Finish(failureMessage);
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > deadline)
            {
                Finish($"timed out after {TimeoutSeconds} s");
            }
        }

        private IEnumerator Script(string name)
        {
            switch (name)
            {
                case "title": return Title();
                case "enter-selected": return EnterSelected();
                case "fight-win": return FightWin();
                case "fight-lose": return FightLose();
                case "spam": return Spam();
                case "esc": return Esc();
                default: return null;
            }
        }

        // Each turn: a Defend if there is one, then Strikes while any can be played, then End
        // Turn, until the Slime dies. Then Play Again must deal a fresh fight.
        private IEnumerator FightWin()
        {
            yield return EnterFight();
            yield return Screenshot("fight.png");
            bool pausedShot = false;
            while (!ResultShown())
            {
                PlayFirst("DEFEND");
                while (!ResultShown() && PlayFirst("STRIKE"))
                {
                    yield return null;
                }

                if (ResultShown())
                {
                    break;
                }

                Click("EndTurnButton");
                if (!pausedShot)
                {
                    yield return Screenshot("paused.png");
                    pausedShot = true;
                }

                yield return WaitForPlayerTurn();
            }

            yield return Screenshot("won.png");
            if (!Expect("result message", TextOf("ResultMessage"), "You win!") || !ExpectSounds("Hit", "Block", "Win"))
            {
                yield break;
            }

            yield return Reloading(() => Click("PlayAgainButton"));
            Expect("enemy HP after Play Again", TextOf("EnemyHpText"), "HP 30/30");
            Expect("player HP after Play Again", TextOf("PlayerHpText"), "HP 40/40");
        }

        // End Turn until the Slime wins, then Esc from the result panel (Review Focus 2).
        private IEnumerator FightLose()
        {
            yield return EnterFight();
            while (!ResultShown())
            {
                Click("EndTurnButton");
                yield return WaitForPlayerTurn();
            }

            yield return Screenshot("lost.png");
            if (!Expect("result message", TextOf("ResultMessage"), "You died.") || !ExpectSounds("Hit", "Lose"))
            {
                yield break;
            }

            FindAnyObjectByType<BackToTitleOnEsc>().OnEscape();
            yield return WaitForScene(SceneNames.Title);
        }

        // End Turn three times in one frame, then Enter and a card click mid-pause (Review Focus
        // 1). Exactly one attack must land, and no card may be played.
        private IEnumerator Spam()
        {
            yield return EnterFight();
            Button card = ButtonNamed("Card");
            Button endTurn = ButtonNamed("EndTurnButton");
            if (card == null || endTurn == null)
            {
                yield break;
            }

            var screen = FindAnyObjectByType<FightScreen>();
            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke();
            screen.OnEnter();
            card.onClick.Invoke();
            yield return null;
            screen.OnEnter();
            card.onClick.Invoke();
            yield return WaitForPlayerTurn();
            yield return new WaitForSeconds(FightScreen.EnemyTurnPause * 2f);
            if (Expect("player HP", TextOf("PlayerHpText"), "HP 34/40")
                && Expect("enemy HP", TextOf("EnemyHpText"), "HP 30/30")
                && Expect("discard", TextOf("DiscardText"), "Discard 5"))
            {
                Expect("cards in hand", CardsInHand().Count.ToString(), "5");
            }
        }

        // Esc mid-pause: the Title loads cleanly, nothing from the old fight fires later, and the
        // next fight starts fresh (Review Focus 2).
        private IEnumerator Esc()
        {
            yield return EnterFight();
            Click("EndTurnButton");
            yield return null;
            FindAnyObjectByType<BackToTitleOnEsc>().OnEscape();
            yield return WaitForScene(SceneNames.Title);
            yield return new WaitForSeconds(FightScreen.EnemyTurnPause * 2f);
            yield return EnterFight();
            if (Expect("player HP", TextOf("PlayerHpText"), "HP 40/40")
                && Expect("enemy HP", TextOf("EnemyHpText"), "HP 30/30")
                && Expect("cards in hand", CardsInHand().Count.ToString(), "5"))
            {
                Expect("End Turn interactable", InteractableOf("EndTurnButton"), "True");
            }
        }

        private IEnumerator EnterFight()
        {
            yield return WaitForScene(SceneNames.Title);
            Click("PlayButton");
            yield return WaitForScene(SceneNames.Fight);
        }

        // Runs an action that reloads the Fight scene, and waits for the new one.
        private IEnumerator Reloading(Action reload)
        {
            FightScreen before = FindAnyObjectByType<FightScreen>();
            reload();
            while (before != null)
            {
                yield return null;
            }

            yield return WaitForScene(SceneNames.Fight);
        }

        // The enemy's turn is over once End Turn can be pressed again, or the fight has ended.
        private IEnumerator WaitForPlayerTurn()
        {
            Button endTurn = ButtonNamed("EndTurnButton");
            while (endTurn != null && !endTurn.interactable && !ResultShown())
            {
                yield return null;
            }
        }

        private static bool ResultShown()
        {
            return GameObject.Find("ResultPanel") != null;
        }

        private static List<Button> CardsInHand()
        {
            var cards = new List<Button>();
            foreach (Transform card in GameObject.Find("Hand").transform)
            {
                if (card.gameObject.activeSelf)
                {
                    cards.Add(card.GetComponent<Button>());
                }
            }

            return cards;
        }

        // Clicks the first playable card with this name; false if there is none.
        private static bool PlayFirst(string cardName)
        {
            foreach (Button card in CardsInHand())
            {
                if (card.interactable && card.transform.Find("Name").GetComponent<TMP_Text>().text == cardName)
                {
                    card.onClick.Invoke();
                    return true;
                }
            }

            return false;
        }

        private static string TextOf(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
            return text != null ? text.text : $"<no text named {objectName}>";
        }

        private bool Expect(string what, string actual, string expected)
        {
            if (actual == expected)
            {
                return true;
            }

            Finish($"{what}: expected '{expected}', got '{actual}'");
            return false;
        }

        // A button left selected (pressed, then dragged off) must not take Enter as well: on the
        // title, Enter only plays. Fails if Enter also clicks the selected Quit.
        private IEnumerator EnterSelected()
        {
            yield return WaitForScene(SceneNames.Title);
            bool quitRequested = false;
            bool RefuseQuit()
            {
                quitRequested = true;
                return false;
            }

            Application.wantsToQuit += RefuseQuit;
            EventSystem.current.SetSelectedGameObject(GameObject.Find("QuitButton"));
            yield return null;
            yield return PressEnter();
            yield return WaitForScene(SceneNames.Fight);
            Application.wantsToQuit -= RefuseQuit;
            if (quitRequested)
            {
                Finish("Enter clicked the selected QuitButton as well as playing");
                yield break;
            }

            // The same press must not also end the first turn in the new scene.
            Expect("End Turn interactable after Enter played", InteractableOf("EndTurnButton"), "True");
        }

        // A real key press through the Input System, as a keyboard would send it.
        private static IEnumerator PressEnter()
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        // Title screen, then Play, then the fight placeholder.
        private IEnumerator Title()
        {
            yield return WaitForScene(SceneNames.Title);
            yield return Screenshot("title.png");
            Click("PlayButton");
            yield return WaitForScene(SceneNames.Fight);
            yield return Screenshot("fight.png");
        }

        private IEnumerator WaitForScene(string name)
        {
            while (SceneManager.GetActiveScene().name != name)
            {
                yield return null;
            }

            // Let layout settle before anything reads or captures the scene.
            yield return null;
            yield return null;
        }

        private IEnumerator Screenshot(string file)
        {
            string path = Path.GetFullPath(Path.Combine(shotsFolder, file));
            File.Delete(path);
            // Let uGUI's 0.1 s tint fades settle, so a button disabled this frame shows disabled.
            yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, shotScale);
            while (!File.Exists(path))
            {
                yield return null;
            }

            yield return null;
            Debug.Log($"AUTOPLAY SHOT {path}");
        }

        private void Click(string objectName)
        {
            Button button = ButtonNamed(objectName);
            if (button != null)
            {
                button.onClick.Invoke();
            }
        }

        // The active button with this name; fails the run, and returns null, if there is none.
        private Button ButtonNamed(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                Finish($"no button named {objectName}");
                return null;
            }

            return button;
        }

        private static string InteractableOf(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            return button != null ? button.interactable.ToString() : $"<no button named {objectName}>";
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && failureMessage == null)
            {
                failureMessage = $"exception logged: {condition}";
            }

            // SoundBank logs "<LogPrefix><name> <clip>" for each sound it plays.
            if (type == LogType.Log && condition.StartsWith(SoundBank.LogPrefix))
            {
                soundsPlayed.Add(condition.Substring(SoundBank.LogPrefix.Length).Split(' ')[0]);
            }
        }

        private bool ExpectSounds(params string[] names)
        {
            foreach (string name in names)
            {
                if (!soundsPlayed.Contains(name))
                {
                    Finish($"sound {name} never played; played: {string.Join(", ", soundsPlayed)}");
                    return false;
                }
            }

            return true;
        }

        private void Finish(string failure)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            StopAllCoroutines();
            Debug.Log(failure == null ? $"AUTOPLAY PASS {script}" : $"AUTOPLAY FAIL {script}: {failure}");
            Application.Quit(failure == null ? 0 : 1);
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
#endif
