using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SimoSlime.Game
{
    /// <summary>
    /// The title screen. Its Play and Quit buttons call <see cref="Play"/> and
    /// <see cref="Quit"/> through their On Click lists; Enter also plays.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Play();
            }
        }

        public void Play()
        {
            SceneManager.LoadScene(SceneNames.Fight);
        }

        public void Quit()
        {
            Application.Quit();
        }
    }
}
