using UnityEngine;

namespace SimoSlime.Game
{
    /// <summary>
    /// The game's one dark look (design gate pick P3 Moss). Contrast against Background:
    /// Text 14.97, Hp 6.58, Block 7.97, Slime 9.14; CardText on Card 15.07.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = new Color32(0x1A, 0x1F, 0x1B, 0xFF);
        public static readonly Color Text = new Color32(0xF1, 0xF3, 0xEE, 0xFF);
        public static readonly Color Slime = new Color32(0x7C, 0xD3, 0x7E, 0xFF);
        public static readonly Color Card = new Color32(0xF3, 0xEE, 0xDF, 0xFF);
        public static readonly Color CardText = new Color32(0x1A, 0x1A, 0x14, 0xFF);
        public static readonly Color Hp = new Color32(0xFF, 0x7A, 0x6E, 0xFF);
        public static readonly Color Block = new Color32(0x7D, 0xB6, 0xFF, 0xFF);
        public static readonly Color Panel = new Color32(0x26, 0x2D, 0x27, 0xFF);
    }
}
