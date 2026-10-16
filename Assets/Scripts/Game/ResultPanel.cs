using SimoSlime.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimoSlime.Game
{
    /// <summary>
    /// The win or lose panel over the fight, hidden until the fight ends. Its Play Again and Main
    /// Menu buttons call <see cref="PlayAgain"/> and <see cref="MainMenu"/> through their On Click
    /// lists.
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text message;

        public bool IsShown => gameObject.activeSelf;

        public void Show(FightOutcome outcome)
        {
            message.text = outcome == FightOutcome.Won ? "You win!" : "You died.";
            gameObject.SetActive(true);
        }

        public void PlayAgain()
        {
            SceneManager.LoadScene(SceneNames.Fight);
        }

        public void MainMenu()
        {
            SceneManager.LoadScene(SceneNames.Title);
        }
    }
}
