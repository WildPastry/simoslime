using System;
using System.Collections.Generic;

namespace SimoSlime.Rules
{
    /// <summary>
    /// One fight against one enemy. Play cards from the hand, then <see cref="EndTurn"/>: the hand
    /// is discarded, the enemy attacks into the player's block, and a new hand is drawn. When
    /// the draw pile runs out, the discard pile is shuffled back into it.
    /// </summary>
    public sealed class Fight
    {
        private readonly int energyPerTurn;
        private readonly int handSize;
        private readonly Action<List<CardKind>> shuffle;
        private readonly List<CardKind> drawPile;
        private readonly List<CardKind> hand = new List<CardKind>();
        private readonly List<CardKind> discardPile = new List<CardKind>();

        public Fight(FightConfig config, Action<List<CardKind>> shuffle)
        {
            energyPerTurn = config.EnergyPerTurn;
            handSize = config.HandSize;
            this.shuffle = shuffle;
            PlayerMaxHp = config.PlayerMaxHp;
            PlayerHp = config.PlayerMaxHp;
            Energy = config.EnergyPerTurn;
            EnemyName = config.EnemyName;
            EnemyMaxHp = config.EnemyMaxHp;
            EnemyHp = config.EnemyMaxHp;
            EnemyIntent = config.EnemyAttack;

            drawPile = new List<CardKind>(config.Deck);
            shuffle(drawPile);
            DrawHand();
        }

        public int PlayerHp { get; private set; }
        public int PlayerMaxHp { get; }
        public int PlayerBlock { get; private set; }
        public int Energy { get; private set; }
        public string EnemyName { get; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; }
        public int EnemyIntent { get; }
        public IReadOnlyList<CardKind> Hand => hand;
        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;
        public FightOutcome Outcome { get; private set; }

        public bool CanPlay(int handIndex)
        {
            return Outcome == FightOutcome.Ongoing
                && handIndex >= 0
                && handIndex < hand.Count
                && CardDef.Of(hand[handIndex]).Cost <= Energy;
        }

        public CardKind Play(int handIndex)
        {
            if (!CanPlay(handIndex))
            {
                throw new InvalidOperationException($"Card {handIndex} can't be played now.");
            }

            CardKind kind = hand[handIndex];
            CardDef def = CardDef.Of(kind);
            Energy -= def.Cost;
            EnemyHp = Math.Max(0, EnemyHp - def.Damage);
            PlayerBlock += def.Block;
            hand.RemoveAt(handIndex);
            discardPile.Add(kind);
            if (EnemyHp == 0)
            {
                Outcome = FightOutcome.Won;
            }

            return kind;
        }

        public EnemyTurnResult EndTurn()
        {
            if (Outcome != FightOutcome.Ongoing)
            {
                throw new InvalidOperationException($"The fight is over: {Outcome}.");
            }

            discardPile.AddRange(hand);
            hand.Clear();

            int blocked = Math.Min(PlayerBlock, EnemyIntent);
            int hpLost = Math.Min(PlayerHp, EnemyIntent - blocked);
            PlayerHp -= hpLost;
            if (PlayerHp == 0)
            {
                Outcome = FightOutcome.Lost;
                return new EnemyTurnResult(hpLost, blocked);
            }

            PlayerBlock = 0;
            Energy = energyPerTurn;
            DrawHand();
            return new EnemyTurnResult(hpLost, blocked);
        }

        private void DrawHand()
        {
            for (int i = 0; i < handSize; i++)
            {
                if (drawPile.Count == 0)
                {
                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    shuffle(drawPile);
                }

                hand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }
        }
    }
}
