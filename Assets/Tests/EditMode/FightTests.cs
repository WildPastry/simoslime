using System.Collections.Generic;
using NUnit.Framework;

namespace SimoSlime.Rules.Tests
{
    public class FightTests
    {
        // The identity shuffle keeps the deck in order, so the opening hand is 5 Strike.
        private static Fight Start(FightConfig config = null)
        {
            return new Fight(config ?? FightConfig.Default(), _ => { });
        }

        [Test]
        public void New_fight_uses_config()
        {
            Fight fight = Start();

            Assert.That(fight.PlayerHp, Is.EqualTo(40));
            Assert.That(fight.PlayerMaxHp, Is.EqualTo(40));
            Assert.That(fight.Energy, Is.EqualTo(3));
            Assert.That(fight.Hand.Count, Is.EqualTo(5));
            Assert.That(fight.DrawCount, Is.EqualTo(5));
            Assert.That(fight.DiscardCount, Is.EqualTo(0));
            Assert.That(fight.EnemyName, Is.EqualTo("Slime"));
            Assert.That(fight.EnemyHp, Is.EqualTo(30));
            Assert.That(fight.EnemyMaxHp, Is.EqualTo(30));
            Assert.That(fight.EnemyIntent, Is.EqualTo(6));
            Assert.That(fight.Outcome, Is.EqualTo(FightOutcome.Ongoing));
        }

        [Test]
        public void Strike_deals_6_and_costs_1()
        {
            Fight fight = Start();

            Assert.That(fight.Play(0), Is.EqualTo(CardKind.Strike));

            Assert.That(fight.EnemyHp, Is.EqualTo(24));
            Assert.That(fight.Energy, Is.EqualTo(2));
            Assert.That(fight.Hand.Count, Is.EqualTo(4));
            Assert.That(fight.DiscardCount, Is.EqualTo(1));
        }

        [Test]
        public void Defend_gains_5_block()
        {
            Fight fight = Start(AllDefend());

            Assert.That(fight.Play(0), Is.EqualTo(CardKind.Defend));

            Assert.That(fight.PlayerBlock, Is.EqualTo(5));
        }

        [Test]
        public void Two_defends_stack_and_absorb_attack()
        {
            Fight fight = Start(AllDefend());
            fight.Play(0);
            fight.Play(0);
            Assert.That(fight.PlayerBlock, Is.EqualTo(10));

            EnemyTurnResult result = fight.EndTurn();

            Assert.That(result.HpLost, Is.EqualTo(0));
            Assert.That(result.Blocked, Is.EqualTo(6));
            Assert.That(fight.PlayerHp, Is.EqualTo(40));
            Assert.That(fight.PlayerBlock, Is.EqualTo(0));
        }

        [Test]
        public void Partial_block()
        {
            Fight fight = Start(AllDefend());
            fight.Play(0);

            EnemyTurnResult result = fight.EndTurn();

            Assert.That(result.HpLost, Is.EqualTo(1));
            Assert.That(result.Blocked, Is.EqualTo(5));
            Assert.That(fight.PlayerHp, Is.EqualTo(39));
        }

        [Test]
        public void Cannot_play_without_energy()
        {
            Fight fight = Start();
            fight.Play(0);
            fight.Play(0);
            fight.Play(0);

            Assert.That(fight.CanPlay(0), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => fight.Play(0));
        }

        [Test]
        public void Invalid_index_cannot_play()
        {
            Fight fight = Start();

            Assert.That(fight.CanPlay(-1), Is.False);
            Assert.That(fight.CanPlay(5), Is.False);
        }

        [Test]
        public void End_turn_discards_hand_and_draws_5()
        {
            Fight fight = Start();
            fight.Play(0);

            fight.EndTurn();

            Assert.That(fight.Hand.Count, Is.EqualTo(5));
            Assert.That(fight.Hand, Is.All.EqualTo(CardKind.Defend));
            Assert.That(fight.DrawCount, Is.EqualTo(0));
            Assert.That(fight.DiscardCount, Is.EqualTo(5));
            Assert.That(fight.Energy, Is.EqualTo(3));
        }

        [Test]
        public void Empty_draw_pile_reshuffles_discard()
        {
            int shuffles = 0;
            var fight = new Fight(FightConfig.Default(), _ => shuffles++);

            fight.EndTurn();
            fight.EndTurn();

            Assert.That(shuffles, Is.EqualTo(2));
            Assert.That(fight.Hand.Count, Is.EqualTo(5));
            Assert.That(fight.DrawCount, Is.EqualTo(5));
            Assert.That(fight.DiscardCount, Is.EqualTo(0));
        }

        [Test]
        public void Strike_to_zero_wins_and_locks()
        {
            FightConfig config = FightConfig.Default();
            config.EnemyMaxHp = 6;
            Fight fight = Start(config);

            fight.Play(0);

            Assert.That(fight.Outcome, Is.EqualTo(FightOutcome.Won));
            Assert.That(fight.EnemyHp, Is.EqualTo(0));
            Assert.That(fight.CanPlay(0), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => fight.EndTurn());
        }

        [Test]
        public void Overkill_floors_at_zero()
        {
            FightConfig config = FightConfig.Default();
            config.EnemyMaxHp = 4;
            Fight fight = Start(config);

            fight.Play(0);

            Assert.That(fight.EnemyHp, Is.EqualTo(0));
            Assert.That(fight.Outcome, Is.EqualTo(FightOutcome.Won));
        }

        [Test]
        public void Enemy_kills_player_loses()
        {
            FightConfig config = FightConfig.Default();
            config.PlayerMaxHp = 4;
            Fight fight = Start(config);

            EnemyTurnResult result = fight.EndTurn();

            Assert.That(fight.Outcome, Is.EqualTo(FightOutcome.Lost));
            Assert.That(fight.PlayerHp, Is.EqualTo(0));
            Assert.That(result.HpLost, Is.EqualTo(4));
            Assert.That(fight.Hand.Count, Is.EqualTo(0));
            Assert.Throws<System.InvalidOperationException>(() => fight.EndTurn());
        }

        [Test]
        public void Shuffle_with_is_permutation()
        {
            List<CardKind> original = FightConfig.Default().Deck;
            var deck = new List<CardKind>(original);

            Shuffle.With(new System.Random(1))(deck);

            Assert.That(deck, Is.EquivalentTo(original));
            Assert.That(deck, Is.Not.EqualTo(original));
        }

        private static FightConfig AllDefend()
        {
            FightConfig config = FightConfig.Default();
            config.Deck = new List<CardKind>();
            for (int i = 0; i < 10; i++)
            {
                config.Deck.Add(CardKind.Defend);
            }

            return config;
        }
    }
}
