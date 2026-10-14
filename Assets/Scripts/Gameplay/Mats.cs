using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    /// <summary>Procedural materials, textures and primitive helpers. No external assets needed.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
        static Shader sh;
        static Font font;

        public static void ClearCache() { mats.Clear(); texs.Clear(); sh = null; font = null; }

        static Shader GetShader()
        {
            if (sh != null) return sh;
            sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return sh;
        }

        public static Material Get(string key, Color c, float smooth = 0.15f, Texture tex = null, float metal = 0f, Color? emit = null)
        {
            Material m;
            if (mats.TryGetValue(key, out m) && m != null) return m;
            m = new Material(GetShader());
            m.name = key;
            m.color = c;
            if (tex != null) m.mainTexture = tex;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emit.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emit.Value);
            }
            mats[key] = m;
            return m;
        }

        // style: 0 tiles+grout, 1 checker, 2 concrete, 3 hazard stripes, 4 brushed steel
        public static Texture2D Tex(string key, int style, Color a, Color b)
        {
            Texture2D t;
            if (texs.TryGetValue(key, out t) && t != null) return t;
            const int N = 64;
            t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.name = key;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.15f + 3.1f, y * 0.15f + 7.7f);
                    float g = 0.68f + 0.32f * n;
                    Color c;
                    switch (style)
                    {
                        case 0: c = ((x % 32) < 2 || (y % 32) < 2) ? b : a; break;
                        case 1: c = (((x / 16) + (y / 16)) % 2 == 0) ? a : b; break;
                        case 2: c = Color.Lerp(a, b, n); break;
                        case 3: c = (((x + y) / 8) % 2 == 0) ? a : b; g = 1f; break;
                        default: c = Color.Lerp(a, b, Mathf.PerlinNoise(x * 0.03f, y * 0.7f)); g = 0.85f + 0.15f * n; break;
                    }
                    c.r *= g; c.g *= g; c.b *= g; c.a = 1f;
                    px[y * N + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(true);
            texs[key] = t;
            return t;
        }

        // ---- named materials ----
        public static Material Steel { get { return Get("steel", new Color(0.7f, 0.72f, 0.75f), 0.55f, Tex("steelT", 4, new Color(0.75f, 0.77f, 0.8f), new Color(0.45f, 0.47f, 0.5f)), 0.5f); } }
        public static Material Cardboard { get { return Get("cardboard", new Color(0.78f, 0.6f, 0.36f), 0.05f); } }
        public static Material Red { get { return Get("red", new Color(0.8f, 0.1f, 0.08f), 0.3f); } }
        public static Material Yellow { get { return Get("yellow", new Color(1f, 0.8f, 0.1f), 0.3f); } }
        public static Material Cream { get { return Get("cream", new Color(0.92f, 0.86f, 0.7f), 0.2f); } }
        public static Material White { get { return Get("white", new Color(0.9f, 0.9f, 0.88f), 0.3f); } }
        public static Material Dark { get { return Get("dark", new Color(0.08f, 0.08f, 0.09f), 0.6f); } }
        public static Material Wood { get { return Get("wood", new Color(0.45f, 0.3f, 0.17f), 0.1f); } }
        public static Material Bun { get { return Get("bun", new Color(0.85f, 0.55f, 0.2f), 0.3f); } }
        public static Material Patty { get { return Get("patty", new Color(0.32f, 0.16f, 0.08f), 0.2f); } }
        public static Material Cheese { get { return Get("cheese", new Color(1f, 0.75f, 0.05f), 0.4f); } }
        public static Material Fry { get { return Get("fry", new Color(1f, 0.82f, 0.25f), 0.2f); } }
        public static Material SauceCrate { get { return Get("sauceCrate", new Color(0.25f, 0.7f, 0.2f), 0.5f, null, 0f, new Color(0.15f, 0.7f, 0.05f)); } }
        public static Material Pale { get { return Get("pale", new Color(0.82f, 0.8f, 0.7f), 0.2f); } }
        public static Material Suit { get { return Get("suit", new Color(0.55f, 0.52f, 0.42f), 0.1f); } }
        public static Material Pants { get { return Get("pants", new Color(0.18f, 0.18f, 0.2f), 0.1f); } }
        public static Material EyeGlow { get { return Get("eye", new Color(1f, 0.1f, 0.05f), 0.5f, null, 0f, new Color(2f, 0.15f, 0.05f)); } }
        public static Material Tube { get { return Get("tube", new Color(0.95f, 1f, 0.95f), 0.5f, null, 0f, new Color(1.6f, 1.7f, 1.5f)); } }
        public static Material GlowGreen { get { return Get("glowGreen", new Color(0.1f, 0.8f, 0.2f), 0.5f, null, 0f, new Color(0.1f, 1.2f, 0.25f)); } }
        public static Material Screen { get { return Get("screen", new Color(0.05f, 0.3f, 0.08f), 0.6f, null, 0f, new Color(0.15f, 1.0f, 0.3f)); } }
        public static Material Grease { get { return Get("grease", new Color(0.35f, 0.25f, 0.04f), 0.95f); } }
        public static Material Hazard { get { return Get("hazard", Color.white, 0.2f, Tex("hazardT", 3, new Color(1f, 0.8f, 0.05f), new Color(0.06f, 0.06f, 0.06f))); } }
        public static Material VanWhite { get { return Get("van", new Color(0.88f, 0.88f, 0.84f), 0.4f); } }
        public static Material Blue { get { return Get("blue", new Color(0.12f, 0.25f, 0.6f), 0.3f); } }
        public static Material Pipe { get { return Get("pipe", new Color(0.35f, 0.22f, 0.18f), 0.3f, null, 0.4f); } }
        public static Material Headlight { get { return Get("headlight", Color.yellow, 0.5f, null, 0f, new Color(1.5f, 1.4f, 0.6f)); } }
        public static Material Taillight { get { return Get("taillight", Color.red, 0.5f, null, 0f, new Color(1.5f, 0.1f, 0.05f)); } }

        public static GameObject Part(Transform parent, PrimitiveType t, string name, Vector3 lp, Vector3 ls, Material m, Vector3 euler = default(Vector3), bool collider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = ls;
            var r = g.GetComponent<Renderer>();
            if (m != null) r.sharedMaterial = m;
            if (!collider)
            {
                var c = g.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return g;
        }

        /// <summary>Fits a text sign inside a width x height box in world units.</summary>
        public static void Sign(Transform parent, string text, Vector3 pos, Quaternion rot, float width, float height, Color col)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            var go = new GameObject("Sign");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = col;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            Bounds b = mr.bounds;
            if (b.size.x > 0.01f && b.size.y > 0.01f)
            {
                // world-space bounds are axis-aligned; use the larger horizontal extent
                float w = Mathf.Max(b.size.x, b.size.z);
                float k = Mathf.Min(width / w, height / b.size.y);
                go.transform.localScale = Vector3.one * k;
            }
        }
    }
}
