using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 仮の効果音。音声ファイルを使わず、短い電子音をその場で作って鳴らす。
    /// 本制作では外注の効果音に差し替える。
    /// </summary>
    public static class Sfx
    {
        const int SampleRate = 44100;
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            Cache.Clear();
        }

        public static void Init(AudioSource audioSource) { source = audioSource; }

        public static void Pick() { Play("pick", 0.5f, 0.06f, 880f); }
        public static void Put() { Play("put", 0.5f, 0.06f, 620f); }
        public static void Drop() { Play("drop", 0.4f, 0.07f, 330f); }
        public static void Dash() { Play("dash", 0.3f, 0.05f, 500f, 700f); }
        public static void Complete() { Play("complete", 0.5f, 0.08f, 784f, 1047f); }
        public static void Deliver() { Play("deliver", 0.6f, 0.09f, 784f, 988f, 1319f); }
        public static void Fail() { Play("fail", 0.5f, 0.14f, 220f, 165f); }
        public static void Warn() { Play("warn", 0.5f, 0.11f, 660f, 520f, 660f, 520f); }
        public static void Cheer() { Play("cheer", 0.6f, 0.09f, 659f, 784f, 988f, 1319f); }
        public static void Tick() { Play("tick", 0.4f, 0.05f, 1000f); }
        public static void Start() { Play("start", 0.6f, 0.12f, 784f, 1175f); }
        public static void Finish() { Play("finish", 0.6f, 0.16f, 1047f, 784f, 523f); }

        static void Play(string key, float volume, float noteSeconds, params float[] notes)
        {
            if (source == null) return;
            if (!Cache.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                clip = Build(key, noteSeconds, notes);
                Cache[key] = clip;
            }
            source.PlayOneShot(clip, volume);
        }

        static AudioClip Build(string name, float noteSeconds, float[] notes)
        {
            int perNote = Mathf.Max(1, Mathf.RoundToInt(noteSeconds * SampleRate));
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                float freq = notes[n];
                for (int i = 0; i < perNote; i++)
                {
                    float time = (float)i / SampleRate;
                    float progress = (float)i / perNote;
                    float attack = Mathf.Clamp01(time / 0.004f);
                    float decay = Mathf.Pow(1f - progress, 1.5f);
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * time) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * time);
                    data[n * perNote + i] = wave * attack * decay * 0.5f;
                }
            }
            AudioClip clip = AudioClip.Create("Sfx_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
