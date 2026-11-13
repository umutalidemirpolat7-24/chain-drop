using UnityEngine;

namespace ChainDrop
{
    /// <summary>Runtime-generated sprites and small helpers, so the prototype needs no art assets.</summary>
    public static class Util
    {
        static Sprite _square, _circle, _soft, _rounded, _chain;

        static Texture2D Tex(int w, int h, System.Func<int, int, Color> f, FilterMode fm = FilterMode.Bilinear, TextureWrapMode wm = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(w, h) { filterMode = fm, wrapMode = wm };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        public static Sprite Square => _square ??= Sprite.Create(Tex(4, 4, (x, y) => Color.white, FilterMode.Point), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        public static Sprite Circle => _circle ??= Sprite.Create(Tex(64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
            return new Color(1, 1, 1, Mathf.Clamp01(32f - d));
        }), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);

        /// <summary>Soft radial blob, used for shadows and lamp glows.</summary>
        public static Sprite Soft => _soft ??= Sprite.Create(Tex(64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1, 1, 1, a * a * (3f - 2f * a));
        }), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);

        /// <summary>Rounded rectangle (looks less "programmer art" than hard squares).</summary>
        public static Sprite Rounded => _rounded ??= Sprite.Create(Tex(32, 32, (x, y) =>
        {
            const float r = 7f;
            float cx = Mathf.Clamp(x + 0.5f, r, 32 - r), cy = Mathf.Clamp(y + 0.5f, r, 32 - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
        }), new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);

        /// <summary>Tileable chain-link pattern; pivot at the bottom so it can be stretched upward.</summary>
        public static Sprite Chain => _chain ??= Sprite.Create(Tex(8, 16, (x, y) =>
        {
            bool wide = y < 8;
            bool on = wide ? (x == 1 || x == 6 || y == 0 || y == 7) && x >= 1 && x <= 6
                           : (x >= 3 && x <= 4);
            return on ? Color.white : new Color(1, 1, 1, 0);
        }, FilterMode.Point, TextureWrapMode.Repeat), new Rect(0, 0, 8, 16), new Vector2(0.5f, 0f), 32f, 0, SpriteMeshType.FullRect);

        public static SpriteRenderer Box(string name, Transform parent, Vector2 localPos, Vector2 size, Color c, int order)
            => Make(Square, name, parent, localPos, size, c, order);

        public static SpriteRenderer RBox(string name, Transform parent, Vector2 localPos, Vector2 size, Color c, int order)
            => Make(Rounded, name, parent, localPos, size, c, order);

        public static SpriteRenderer Dot(string name, Transform parent, Vector2 localPos, float diameter, Color c, int order)
            => Make(Circle, name, parent, localPos, new Vector2(diameter, diameter), c, order);

        public static SpriteRenderer Blob(string name, Transform parent, Vector2 localPos, Vector2 size, Color c, int order)
            => Make(Soft, name, parent, localPos, size, c, order);

        static SpriteRenderer Make(Sprite s, string name, Transform parent, Vector2 localPos, Vector2 size, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        public static Vector2 GetVel(this Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody2D rb, Vector2 v)
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

        public static Color A(this Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Sorting order for things standing on the floor: lower on screen = drawn in front.</summary>
        public static int DepthOrder(float y) => 2000 - Mathf.RoundToInt(y * 40f);
    }
}
