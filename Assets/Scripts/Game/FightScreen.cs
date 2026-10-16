using System.Collections;
using System.Collections.Generic;
using SimoSlime.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimoSlime.Game
{
    /// <summary>
    /// Shows one <see cref="Fight"/> and forwards clicks to it. A card click plays the card. End
    /// Turn, or Enter, clears the hand, waits <see cref="EnemyTurnPause"/>, then the enemy attacks
    /// and a new hand is dealt; nothing else can be played or ended until then. When the fight
    /// is over the result panel shows, and Enter plays again.
    /// </summary>
    public class FightScreen : MonoBehaviour
    {
        public const float EnemyTurnPause = 0.5f;

        [SerializeField] private TMP_Text enemyNameText;
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text blockText;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text drawText;
        [SerializeField] private TMP_Text discardText;
        [SerializeField] private RectTransform hand;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TMP_Text endTurnLabel;
        [SerializeField] private ResultPanel resultPanel;
        [SerializeField] private SoundBank sounds;

        private readonly List<CardView> cards = new List<CardView>();
        private Fight fight;
        private bool busy;

        private void Awake()
        {
            fight = new Fight(FightConfig.Default(), Shuffle.With(new System.Random()));
            Refresh();
        }

        private void Update()
        {
            if (EnterKey.WasPressedThisFrame())
            {
                OnEnter();
            }
        }

        public void OnEnter()
        {
            if (resultPanel.IsShown)
            {
                resultPanel.PlayAgain();
            }
            else
            {
                EndTurn();
            }
        }

        /// <summary>End Turn's On Click. Does nothing during the enemy's turn or once the fight is over.</summary>
        public void EndTurn()
        {
            if (busy || fight.Outcome != FightOutcome.Ongoing)
            {
                return;
            }

            StartCoroutine(EnemyTurn());
        }

        private IEnumerator EnemyTurn()
        {
            busy = true;
            Refresh();
            yield return new WaitForSeconds(EnemyTurnPause);
            EnemyTurnResult attack = fight.EndTurn();
            sounds.Play(attack.HpLost > 0 ? Sound.Hit : Sound.Block);
            busy = false;
            Refresh();
            ShowResultIfOver();
        }

        private void PlayCard(int handIndex)
        {
            if (busy || !fight.CanPlay(handIndex))
            {
                return;
            }

            CardDef played = CardDef.Of(fight.Play(handIndex));
            sounds.Play(played.Damage > 0 ? Sound.Hit : Sound.Block);
            Refresh();
            ShowResultIfOver();
        }

        private void ShowResultIfOver()
        {
            if (fight.Outcome != FightOutcome.Ongoing)
            {
                sounds.Play(fight.Outcome == FightOutcome.Won ? Sound.Win : Sound.Lose);
                resultPanel.Show(fight.Outcome);
            }
        }

        // During the enemy's turn the hand is already shown as discarded.
        private void Refresh()
        {
            int shown = busy ? 0 : fight.Hand.Count;
            enemyNameText.text = fight.EnemyName;
            enemyHpText.text = $"HP {fight.EnemyHp}/{fight.EnemyMaxHp}";
            intentText.text = $"Intent: attack {fight.EnemyIntent}";
            playerHpText.text = $"HP {fight.PlayerHp}/{fight.PlayerMaxHp}";
            blockText.text = $"Block {fight.PlayerBlock}";
            energyText.text = $"Energy {fight.Energy}";
            drawText.text = $"Draw {fight.DrawCount}";
            discardText.text = $"Discard {fight.DiscardCount + fight.Hand.Count - shown}";
            endTurnButton.interactable = !busy && fight.Outcome == FightOutcome.Ongoing;
            endTurnLabel.text = busy ? $"{fight.EnemyName}'s turn" : "End Turn";

            // Card views are reused; inactive ones drop out of the hand's layout.
            for (int i = 0; i < shown; i++)
            {
                if (i == cards.Count)
                {
                    CardView card = Instantiate(cardPrefab, hand);
                    card.name = "Card";
                    cards.Add(card);
                }

                int handIndex = i;
                cards[i].gameObject.SetActive(true);
                cards[i].Show(CardDef.Of(fight.Hand[i]), fight.CanPlay(i), () => PlayCard(handIndex));
            }

            for (int i = shown; i < cards.Count; i++)
            {
                cards[i].gameObject.SetActive(false);
            }
        }
    }
}
