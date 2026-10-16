using System;
using SimoSlime.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimoSlime.Game
{
    /// <summary>One card in the hand: the root of the Card prefab.</summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text effectText;

        /// <summary>
        /// Shows <paramref name="def"/>. A card that can't be played is greyed out by its
        /// button's disabled tint.
        /// </summary>
        public void Show(CardDef def, bool playable, Action onClick)
        {
            nameText.text = def.Name.ToUpperInvariant();
            costText.text = def.Cost.ToString();
            effectText.text = def.Damage > 0 ? $"Deal {def.Damage}" : $"Gain {def.Block} block";
            button.interactable = playable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
