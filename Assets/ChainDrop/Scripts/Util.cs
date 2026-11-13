using UnityEngine;

namespace ChainDrop
{
    /// <summary>Runtime-generated sprites and small helpers, so the prototype needs no art assets.</summary>
    public static class Util
    {
        static Sprite _square, _circle;

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var t = new Texture2D(4, 4) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    var px = new Color[16];
                    for (int i = 0; i < 16; i++) px[i] = Color.white;
                    t.SetPixels(px);
                    t.Apply();
                    _square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                }
                return _square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    const int n = 64;
                    var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    var px = new Color[n * n];
                    float r = n / 2f;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                        }
                    t.SetPixels(px);
                    t.Apply();
                    _circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return _circle;
            }
        }

        public static SpriteRenderer Box(string name, Transform parent, Vector2 localPos, Vector2 size, Color c, int order)
        {
            return Make(Square, name, parent, localPos, size, c, order);
        }

        public static SpriteRenderer Dot(string name, Transform parent, Vector2 localPos, float diameter, Color c, int order)
        {
            return Make(Circle, name, parent, localPos, new Vector2(diameter, diameter), c, order);
        }

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
    }
}
