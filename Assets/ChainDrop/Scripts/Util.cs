using System.Collections.Generic;
using UnityEngine;

namespace ChainDrop
{
    /// <summary>3D helpers: primitives, URP materials and procedural textures, so the prototype needs no art files.</summary>
    public static class Util
    {
        static Material baseMat;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material BaseMat
        {
            get
            {
                if (baseMat == null)
                {
                    // Steal the render pipeline's default lit material from a primitive (works in URP and built-in).
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMat = g.GetComponent<Renderer>().sharedMaterial;
                    Object.Destroy(g);
                }
                return baseMat;
            }
        }

        public static Material Mat(Color c, float smooth = 0.35f, float metal = 0f)
        {
            string key = $"{c}-{smooth}-{metal}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseMat) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        public static Material Glow(Color c, float intensity = 2.5f)
        {
            string key = $"glow-{c}-{intensity}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseMat) { color = c };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cache[key] = m;
            return m;
        }

        public static Material Textured(Texture2D tex, Vector2 tiling, Color tint, float smooth = 0.4f, float metal = 0.2f)
        {
            var m = new Material(BaseMat) { color = tint, mainTexture = tex, mainTextureScale = tiling };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>Creates a primitive without its collider (colliders are added explicitly where needed).</summary>
        public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
            => Prim(PrimitiveType.Cube, name, parent, pos, scale, m);

        /// <summary>Riveted steel floor plate, tileable.</summary>
        public static Texture2D SteelPlate(Color a, Color b)
        {
            const int n = 128;
            var t = new Texture2D(n, n) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rnd = new System.Random(3);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool checker = ((x / 64) + (y / 64)) % 2 == 0;
                    Color c = checker ? a : b;
                    // brushed noise
                    float noise = (float)rnd.NextDouble() * 0.06f - 0.03f + Mathf.Sin(y * 0.9f) * 0.01f;
                    c = new Color(c.r + noise, c.g + noise, c.b + noise, 1);
                    // seams between plates
                    int lx = x % 64, ly = y % 64;
                    if (lx < 2 || ly < 2) c *= 0.55f;
                    else if (lx < 4 || ly < 4) c *= 1.15f;
                    // rivets in plate corners
                    foreach (var r in new[] { new Vector2(8, 8), new Vector2(56, 8), new Vector2(8, 56), new Vector2(56, 56) })
                    {
                        float d = Vector2.Distance(new Vector2(lx, ly), r);
                        if (d < 3f) c = Color.Lerp(c * 1.35f, c * 0.7f, d / 3f);
                    }
                    c.a = 1;
                    px[y * n + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D _circle;
        public static Texture2D CircleTex
        {
            get
            {
                if (_circle == null)
                {
                    const int n = 64;
                    _circle = new Texture2D(n, n);
                    var px = new Color[n * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                            px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(32f - d));
                        }
                    _circle.SetPixels(px);
                    _circle.Apply();
                }
                return _circle;
            }
        }

        public static Vector2 G(Vector3 v) => new Vector2(v.x, v.z);           // world → ground plane
        public static Vector3 W(Vector2 g, float y = 0f) => new Vector3(g.x, y, g.y);

        public static Vector3 GetVel(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
