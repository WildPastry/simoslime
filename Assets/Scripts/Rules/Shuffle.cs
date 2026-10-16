using System;
using System.Collections.Generic;

namespace SimoSlime.Rules
{
    public static class Shuffle
    {
        /// <summary>A Fisher-Yates shuffle in place, drawing from <paramref name="rng"/>.</summary>
        public static Action<List<CardKind>> With(Random rng)
        {
            return cards =>
            {
                for (int i = cards.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (cards[i], cards[j]) = (cards[j], cards[i]);
                }
            };
        }
    }
}
