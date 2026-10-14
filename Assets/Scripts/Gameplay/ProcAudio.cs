using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    /// <summary>All sounds are synthesized at runtime. No audio files required.</summary>
    public static class ProcAudio
    {
        const int SR = 22050;
        const float TAU = 6.2831853f;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static readonly System.Random rng = new System.Random(77);

        delegate float Gen(float t, float u);

        static float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
        static float S(float f, float t) { return Mathf.Sin(TAU * f * t); }
        static float Saw(float f, float t) { return 2f * (f * t - Mathf.Floor(f * t)) - 1f; }

        public static void ClearCache() { cache.Clear(); }

        static AudioClip Make(string key, float len, Gen g)
        {
            AudioClip c;
            if (cache.TryGetValue(key, out c) && c != null) return c;
            int n = Mathf.CeilToInt(len * SR);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float u = i / (float)n;
                d[i] = Mathf.Clamp(g(t, u), -1f, 1f);
            }
            c = AudioClip.Create(key, n, 1, SR, false);
            c.SetData(d, 0);
            cache[key] = c;
            return c;
        }

        public static AudioClip Hum { get { return Make("hum", 2f, (t, u) => 0.14f * S(50, t) + 0.09f * S(100, t) + 0.05f * S(150, t) + 0.015f * N()); } }
        public static AudioClip Step { get { return Make("step", 0.2f, (t, u) => N() * Mathf.Exp(-t * 28f) * 0.5f + S(70, t) * Mathf.Exp(-t * 22f) * 0.6f); } }
        public static AudioClip Thump { get { return Make("thump", 0.35f, (t, u) => S(45, t) * Mathf.Exp(-t * 11f) * 0.95f + N() * Mathf.Exp(-t * 50f) * 0.3f); } }
        public static AudioClip Pickup { get { return Make("pickup", 0.2f, (t, u) => S(500f + 900f * u, t) * Mathf.Exp(-u * 4f) * 0.35f); } }
        public static AudioClip Drop { get { return Make("drop", 0.3f, (t, u) => S(95, t) * Mathf.Exp(-t * 14f) * 0.8f + N() * Mathf.Exp(-t * 45f) * 0.35f); } }
        public static AudioClip Cash { get { return Make("cash", 0.6f, (t, u) => S(1318, t) * Mathf.Exp(-t * 8f) * 0.3f + (t > 0.08f ? S(1760, t - 0.08f) * Mathf.Exp(-(t - 0.08f) * 7f) * 0.3f : 0f)); } }
        public static AudioClip Alarm { get { return Make("alarm", 1f, (t, u) => Mathf.Sign(S(u < 0.5f ? 800f : 600f, t)) * 0.22f); } }
        public static AudioClip Chase { get { return Make("chase", 1f, (t, u) => 0.22f * (Saw(110, t) + Saw(115, t)) * (0.5f + 0.5f * S(4, t))); } }
        public static AudioClip Chime { get { return Make("chime", 1.4f, (t, u) => (S(659, t) * Mathf.Exp(-t * 4f) + (t > 0.3f ? S(523, t - 0.3f) * Mathf.Exp(-(t - 0.3f) * 4f) : 0f) + (t > 0.6f ? S(392, t - 0.6f) * Mathf.Exp(-(t - 0.6f) * 3.5f) : 0f)) * 0.3f); } }
        public static AudioClip Slip { get { return Make("slip", 0.6f, (t, u) => S(1200f - 1000f * u, t) * (1f - u) * 0.3f + S(8, t) * 0.0f); } }
        public static AudioClip Clang { get { return Make("clang", 1.5f, (t, u) => (S(420, t) + 0.6f * S(835, t) + 0.4f * S(1260, t)) * Mathf.Exp(-t * 3f) * 0.22f + N() * Mathf.Exp(-t * 60f) * 0.3f); } }
        public static AudioClip Door { get { return Make("door", 0.5f, (t, u) => N() * Mathf.Sin(Mathf.PI * u) * 0.15f); } }
        public static AudioClip InspectorHum { get { return Make("inspHum", 3f, (t, u) => 0.22f * S(55, t) * (0.6f + 0.4f * S(1, t)) + 0.12f * S(58, t)); } }
        public static AudioClip Buzzer { get { return Make("buzzer", 0.6f, (t, u) => Mathf.Sign(S(150, t)) * 0.3f * (1f - u)); } }

        public static void PlayAt(AudioClip c, Vector3 pos, float vol = 1f, bool spatial = true, float pitch = 1f)
        {
            if (c == null) return;
            var go = new GameObject("sfx");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.volume = vol;
            s.spatialBlend = spatial ? 1f : 0f;
            s.pitch = pitch;
            s.minDistance = 2f;
            s.maxDistance = 30f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.Play();
            Object.Destroy(go, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public static void Play2D(AudioClip c, float vol = 1f)
        {
            Vector3 p = Vector3.zero;
            if (GameManager.I != null && GameManager.I.Cam != null) p = GameManager.I.Cam.transform.position;
            PlayAt(c, p, vol, false, 1f);
        }

        public static AudioSource Loop(GameObject go, AudioClip c, float vol, bool spatial, float maxDist = 25f)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = vol;
            s.spatialBlend = spatial ? 1f : 0f;
            s.minDistance = 2f;
            s.maxDistance = maxDist;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.playOnAwake = false;
            s.Play();
            return s;
        }
    }
}
