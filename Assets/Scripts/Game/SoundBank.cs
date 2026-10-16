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
    /// already playing. Development builds log "SOUND name clip" per play, for autoplay.
    /// </summary>
    public class SoundBank : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip block;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip lose;

        public void Play(Sound sound)
        {
            AudioClip clip = ClipFor(sound);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"SOUND {sound} {clip.name}");
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
