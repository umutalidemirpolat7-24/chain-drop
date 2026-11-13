using UnityEngine;

namespace ChainDrop
{
    /// <summary>Marks things robots should steer around (landed crates).</summary>
    public class Obstacle : MonoBehaviour { }

    /// <summary>
    /// A steel crate hanging above the arena on a chain. Its shadow on the floor shows where it will land.
    /// Tap the crate (or its chain) to shoot the chain; it falls and crushes whatever is under the shadow.
    /// </summary>
    public class Crate : MonoBehaviour
    {
        public float Size { get; private set; }
        public float Height { get; private set; }
        public bool Released { get; private set; }
        public bool Landed { get; private set; }
        public Vector2 Ground => pos;                       // floor position (shadow center)
        public Vector2 Visual => pos + new Vector2(0, (Height + Size) * GameManager.Oblique);   // top face center on screen

        GameManager gm;
        Vector2 basePos, pos, vel, swingAxis;
        float swingAmp, swingT, vz, chainTopY;
        Vector2 chainCut;

        SpriteRenderer shadow, front, top, strapA, strapB, rim, chain;
        Transform body;

        const float SwingW = 2.2f;
        const float Gravity = 26f;
        public const float KillHeight = 1.3f;

        public static Crate Build(GameManager gm, CrateDef d, Transform parent, Theme th, float chainTopY)
        {
            var go = new GameObject("Crate");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Crate>();
            c.gm = gm;
            c.Size = d.size > 0.1f ? d.size : 1.6f;
            c.Height = d.h > 0.1f ? d.h : 5f;
            c.basePos = c.pos = new Vector2(d.x, d.y);
            c.swingAmp = d.swing;
            c.swingAxis = d.axis == "y" ? Vector2.up : Vector2.right;
            c.chainTopY = chainTopY;
            c.swingT = Random.Range(0f, 6f);

            float s = c.Size;
            c.shadow = Util.Blob("shadow", go.transform, Vector2.zero, new Vector2(s * 1.3f, s * 1.0f), new Color(0, 0, 0, 0.4f), 10);

            c.body = new GameObject("Body").transform;
            c.body.SetParent(go.transform, false);
            c.front = Util.Box("front", c.body, Vector2.zero, Vector2.one, th.metalDark, 0);
            c.top = Util.RBox("top", c.body, Vector2.zero, Vector2.one, th.metal, 0);
            c.rim = Util.RBox("rim", c.body, Vector2.zero, Vector2.one, th.metal.Mul(0.8f), 0);
            c.strapA = Util.Box("strapA", c.body, Vector2.zero, Vector2.one, th.metal.Mul(1.15f), 0);
            c.strapB = Util.Box("strapB", c.body, Vector2.zero, Vector2.one, th.metal.Mul(1.15f), 0);

            var chainGo = new GameObject("Chain");
            chainGo.transform.SetParent(go.transform, false);
            c.chain = chainGo.AddComponent<SpriteRenderer>();
            c.chain.sprite = Util.Chain;
            c.chain.drawMode = SpriteDrawMode.Tiled;
            c.chain.color = th.chain;

            c.UpdateVisuals();
            return c;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Released)
            {
                swingT += dt;
                pos = basePos + swingAxis * swingAmp * Mathf.Sin(swingT * SwingW);
            }
            else if (!Landed)
            {
                vz -= Gravity * dt;
                Height = Mathf.Max(0f, Height + vz * dt);
                pos += vel * dt;
                if (Height < KillHeight) gm.CrushCheck(this);
                if (Height <= 0f) Land();
            }
            UpdateVisuals();
        }

        public bool HitTest(Vector2 world)
        {
            if (Released) return false;
            var v = Visual;
            if (Mathf.Abs(world.x - v.x) < Size * 0.7f && world.y > v.y - Size * 0.4f - Size * GameManager.Oblique - 0.2f && world.y < v.y + Size * 0.5f)
                return true;
            // the chain above the crate also counts
            return Mathf.Abs(world.x - ChainBottom.x) < 0.45f && world.y > ChainBottom.y && world.y < chainTopY;
        }

        public Vector2 ChainBottom => Visual + new Vector2(0, Size * 0.35f);

        public void Release()
        {
            if (Released) return;
            Released = true;
            vel = swingAxis * swingAmp * SwingW * Mathf.Cos(swingT * SwingW);   // keeps the swing momentum
            vz = 0f;
            chainCut = ChainBottom;
        }

        void Land()
        {
            Landed = true;
            var ob = new GameObject("Obstacle");
            ob.transform.SetParent(transform, false);
            ob.transform.localPosition = pos;
            var col = ob.AddComponent<BoxCollider2D>();
            col.size = new Vector2(Size * 0.92f, Size * 0.8f);
            ob.AddComponent<Obstacle>();
            gm.OnCrateLanded(this);
        }

        void UpdateVisuals()
        {
            float s = Size;
            Vector2 local = pos;   // this object always sits at the world origin; children use world-space coords

            // Shadow: grows sharper and darker as the crate gets closer to the floor
            float k = Mathf.Clamp01(Height / 8f);
            shadow.transform.localPosition = local;
            shadow.transform.localScale = new Vector3(s * (1.35f - 0.35f * (1 - k)), s * (1.05f - 0.25f * (1 - k)), 1);
            shadow.color = new Color(0, 0, 0, Mathf.Lerp(0.6f, 0.22f, k));

            // Cube in 3/4 view: front face + top face
            float ob = GameManager.Oblique;
            float topY = local.y + (Height + s) * ob;
            float topH = s * 0.82f;
            float frontH = s * ob;
            body.localPosition = new Vector3(local.x, 0, 0);
            top.transform.localPosition = new Vector3(0, topY, 0);
            top.transform.localScale = new Vector3(s, topH, 1);
            rim.transform.localPosition = new Vector3(0, topY, 0);
            rim.transform.localScale = new Vector3(s * 0.74f, topH * 0.7f, 1);
            strapA.transform.localPosition = new Vector3(0, topY, 0);
            strapA.transform.localScale = new Vector3(s * 0.12f, topH * 0.7f, 1);
            strapB.transform.localPosition = new Vector3(0, topY, 0);
            strapB.transform.localScale = new Vector3(s * 0.74f, topH * 0.1f, 1);
            front.transform.localPosition = new Vector3(0, topY - topH / 2f - frontH / 2f + 0.02f, 0);
            front.transform.localScale = new Vector3(s, frontH, 1);

            int order = Landed ? Util.DepthOrder(local.y - s * 0.4f) : Released ? 6000 : 5000 + Util.DepthOrder(local.y) / 10;
            front.sortingOrder = order;
            top.sortingOrder = order + 1;
            rim.sortingOrder = order + 2;
            strapA.sortingOrder = order + 3;
            strapB.sortingOrder = order + 3;
            chain.sortingOrder = Released ? 5000 : order - 1;

            // Chain from the crate (or the cut point) up to the ceiling pivot, off the top of the screen
            Vector2 bottom = Released ? chainCut : ChainBottom;
            Vector2 anchor = new Vector2(basePos.x, chainTopY);
            Vector2 dir = anchor - bottom;
            chain.transform.localPosition = bottom;
            chain.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            chain.size = new Vector2(0.25f, Mathf.Max(0.1f, dir.magnitude));
        }
    }
}
