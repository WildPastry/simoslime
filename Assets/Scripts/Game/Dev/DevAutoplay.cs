#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SimoSlime.Game.Dev
{
    /// <summary>
    /// Drives a Development build from the command line so a screen can be proved without a
    /// human: <c>SimoSlime.exe -autoplay title -shots Logs/shots/title</c>. It logs
    /// "AUTOPLAY PASS name" or "AUTOPLAY FAIL name: reason" and quits with 0 or 1. Any logged
    /// exception fails the run. Not compiled into release builds.
    /// </summary>
    public class DevAutoplay : MonoBehaviour
    {
        private const float TimeoutSeconds = 30f;

        private string script;
        private string shots;
        private float deadline;
        private string exception;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string script = Arg("-autoplay");
            if (script == null)
            {
                return;
            }

            var go = new GameObject("DevAutoplay");
            DontDestroyOnLoad(go);
            DevAutoplay autoplay = go.AddComponent<DevAutoplay>();
            autoplay.script = script;
            autoplay.shots = Arg("-shots") ?? "shots";
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
            Directory.CreateDirectory(shots);
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
            Finish(exception);
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
                default: return null;
            }
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
            string path = Path.GetFullPath(Path.Combine(shots, file));
            File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            while (!File.Exists(path))
            {
                yield return null;
            }

            yield return null;
            Debug.Log($"AUTOPLAY SHOT {path}");
        }

        private void Click(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                Finish($"no button named {objectName}");
                return;
            }

            button.onClick.Invoke();
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && exception == null)
            {
                exception = $"exception logged: {condition}";
            }
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
