using System;

namespace SimoSlime.Rules
{
    /// <summary>What a kind of card costs and does.</summary>
    public sealed class CardDef
    {
        public static readonly CardDef Strike = new CardDef(CardKind.Strike, "Strike", 1, 6, 0);
        public static readonly CardDef Defend = new CardDef(CardKind.Defend, "Defend", 1, 0, 5);

        public readonly CardKind Kind;
        public readonly string Name;
        public readonly int Cost;
        public readonly int Damage;
        public readonly int Block;

        private CardDef(CardKind kind, string name, int cost, int damage, int block)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
            Damage = damage;
            Block = block;
        }

        public static CardDef Of(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Strike: return Strike;
                case CardKind.Defend: return Defend;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
