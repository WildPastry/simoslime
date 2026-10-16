namespace SimoSlime.Rules
{
    /// <summary>What the enemy's attack did: HP the player lost, and damage their block took.</summary>
    public readonly struct EnemyTurnResult
    {
        public EnemyTurnResult(int hpLost, int blocked)
        {
            HpLost = hpLost;
            Blocked = blocked;
        }

        public int HpLost { get; }
        public int Blocked { get; }
    }
}
