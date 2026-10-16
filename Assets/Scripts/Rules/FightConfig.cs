using System.Collections.Generic;

namespace SimoSlime.Rules
{
    /// <summary>The numbers a fight starts from. <see cref="Default"/> is the game's one fight.</summary>
    public sealed class FightConfig
    {
        public int PlayerMaxHp = 40;
        public int EnergyPerTurn = 3;
        public int HandSize = 5;
        public string EnemyName = "Slime";
        public int EnemyMaxHp = 30;
        public int EnemyAttack = 6;
        public List<CardKind> Deck = new List<CardKind>
        {
            CardKind.Strike, CardKind.Strike, CardKind.Strike, CardKind.Strike, CardKind.Strike,
            CardKind.Defend, CardKind.Defend, CardKind.Defend, CardKind.Defend, CardKind.Defend,
        };

        public static FightConfig Default()
        {
            return new FightConfig();
        }
    }
}
