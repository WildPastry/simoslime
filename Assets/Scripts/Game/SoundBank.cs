using System;
using UnityEngine;

namespace SimoSlime.Game
{
    public enum Sound
    {
        Hit,
        Block,
        Win,
        Lose,
    }

    /// <summary>
    /// The fight's four sounds (Kenney, CC0), each played once over the top of whatever is
    /// already playing. Development builds log one <see cref="LogPrefix"/> line per play, for autoplay.
    /// </summary>
    public class SoundBank : MonoBehaviour
    {
        /// <summary>Starts the Development-build log line for each play; autoplay reads it.</summary>
        public const string LogPrefix = "SOUND ";

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip block;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip lose;

        public void Play(Sound sound)
        {
            AudioClip clip = ClipFor(sound);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"{LogPrefix}{sound} {clip.name}");
#endif
            source.PlayOneShot(clip);
        }

        private AudioClip ClipFor(Sound sound)
        {
            switch (sound)
            {
                case Sound.Hit: return hit;
                case Sound.Block: return block;
                case Sound.Win: return win;
                case Sound.Lose: return lose;
                default: throw new ArgumentOutOfRangeException(nameof(sound), sound, null);
            }
        }
    }
}
