using System;
using UnityEngine;

namespace ChainDrop
{
    /// <summary>Procedurally generated placeholder sounds (no audio files needed).</summary>
    public class Sfx : MonoBehaviour
    {
        AudioSource src;
        AudioClip shot, empty, clank, thud, crush, win, lose;

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            var rnd = new System.Random(7);
            Func<float> noise = () => (float)(rnd.NextDouble() * 2 - 1);
            const float TAU = Mathf.PI * 2f;

            shot = Make("shot", 0.18f, t => noise() * Mathf.Exp(-t * 35f) * 0.8f + Mathf.Sin(TAU * 140f * t) * Mathf.Exp(-t * 30f) * 0.5f);
            empty = Make("empty", 0.06f, t => Mathf.Sin(TAU * 900f * t) * Mathf.Exp(-t * 80f) * 0.4f);
            clank = Make("clank", 0.35f, t => (Mathf.Sin(TAU * 1750f * t) + 0.6f * Mathf.Sin(TAU * 2630f * t)) * Mathf.Exp(-t * 14f) * 0.35f);
            thud = Make("thud", 0.4f, t => Mathf.Sin(TAU * (90f - 60f * t) * t) * Mathf.Exp(-t * 9f) * 0.9f + noise() * Mathf.Exp(-t * 40f) * 0.3f);
            crush = Make("crush", 0.55f, t => noise() * Mathf.Exp(-t * 10f) * 0.6f + Mathf.Sin(TAU * 55f * t) * Mathf.Exp(-t * 5f) * 0.8f
                                             + Mathf.Sin(TAU * 320f * t) * Mathf.Exp(-t * 20f) * 0.3f);
            win = Make("win", 0.7f, t =>
            {
                float[] notes = { 523f, 659f, 784f, 1047f };
                int i = Mathf.Min(3, (int)(t / 0.12f));
                float lt = t - i * 0.12f;
                return Mathf.Sin(TAU * notes[i] * t) * Mathf.Exp(-lt * 6f) * 0.35f;
            });
            lose = Make("lose", 0.7f, t => Mathf.Sin(TAU * (300f - 220f * t) * t) * Mathf.Exp(-t * 3f) * 0.4f);
        }

        static AudioClip Make(string name, float dur, Func<float, float> f)
        {
            const int rate = 44100;
            int n = (int)(rate * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        void Play(AudioClip c, float v = 1f) { if (c != null) src.PlayOneShot(c, v); }
        public void Shot() => Play(shot);
        public void Empty() => Play(empty);
        public void Clank() => Play(clank, 0.8f);
        public void Thud() => Play(thud);
        public void Crush() => Play(crush);
        public void Win() => Play(win);
        public void Lose() => Play(lose);
    }
}
