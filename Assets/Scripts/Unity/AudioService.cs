using System;
using UnityEngine;

namespace OfficinaTycoon.UnityLayer
{
    public enum Sfx { Tap, Buy, Upgrade, OrderDone, Unlock, Reward, EventStart, Error }

    /// <summary>
    /// Modular audio: short, soft cues generated procedurally (sine blips) so the prototype has feedback
    /// without audio assets. No casino-style jingles. Replace by AudioClips later (see Docs/ASSETS_TO_REPLACE.md).
    /// Music is not included: the toggle exists and is persisted, but there is no track yet.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int Rate = 22050;
        AudioSource _src;
        readonly AudioClip[] _clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
        public bool SfxOn = true;
        public bool MusicOn = true;
        float _lastTapTime;

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 0.5f;
            _clips[(int)Sfx.Tap] = Make(new[] { 760f }, 0.045f, 0.35f);
            _clips[(int)Sfx.Buy] = Make(new[] { 520f, 660f }, 0.07f, 0.45f);
            _clips[(int)Sfx.Upgrade] = Make(new[] { 440f, 550f, 660f }, 0.07f, 0.45f);
            _clips[(int)Sfx.OrderDone] = Make(new[] { 523f, 659f, 784f }, 0.09f, 0.5f);
            _clips[(int)Sfx.Unlock] = Make(new[] { 392f, 523f, 659f, 784f }, 0.08f, 0.5f);
            _clips[(int)Sfx.Reward] = Make(new[] { 660f, 880f }, 0.09f, 0.45f);
            _clips[(int)Sfx.EventStart] = Make(new[] { 330f, 392f }, 0.12f, 0.35f);
            _clips[(int)Sfx.Error] = Make(new[] { 200f }, 0.12f, 0.4f);
        }

        public void Play(Sfx s)
        {
            if (!SfxOn || _src == null) return;
            if (s == Sfx.Tap)
            {
                if (Time.unscaledTime - _lastTapTime < 0.06f) return;   // never machine-gun the tap cue
                _lastTapTime = Time.unscaledTime;
            }
            var c = _clips[(int)s];
            if (c != null) _src.PlayOneShot(c);
        }

        static AudioClip Make(float[] freqs, float noteSeconds, float volume)
        {
            int per = Mathf.Max(1, (int)(Rate * noteSeconds));
            var data = new float[per * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(i / 120f) * Mathf.Clamp01((per - i) / (per * 0.6f));
                    data[n * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * volume;
                }
            var clip = AudioClip.Create("sfx", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
