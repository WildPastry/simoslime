using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SimoSlime.Game
{
    /// <summary>Esc leaves the fight for the title screen, wherever the fight is.</summary>
    public class BackToTitleOnEsc : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                OnEscape();
            }
        }

        public void OnEscape()
        {
            SceneManager.LoadScene(SceneNames.Title);
        }
    }
}
