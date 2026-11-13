using UnityEngine;

namespace ChainDrop
{
    /// <summary>Marks things robots should steer around (landed crates).</summary>
    public class Obstacle : MonoBehaviour { }

    /// <summary>
    /// A steel crate hanging from the ceiling on a real 3D chain (a pendulum).
    /// A red marker on the floor shows exactly where it will land. Tap it to shoot the chain.
    /// </summary>
    public class Crate : MonoBehaviour
    {
        public float Size { get; private set; }
        public bool Released { get; private set; }
        public bool Landed { get; private set; }
        public float Bottom => body.position.y - Size / 2f;       // height of the crate's underside
        public Vector3 Center => body.position;
        public Vector2 Ground => Util.G(body.position);
        public Vector3 ChainPoint => body.position + Vector3.up * (Size / 2f + 0.25f);
        public Vector3 PivotPoint => pivot.position;

        GameManager gm;
        Transform pivot, body, marker;
        Vector3 vel, prevPos, swingAxis, spin;
        float swingAngle, swingT, length;

        public const float PivotY = 15f;
        public const float KillHeight = 1.25f;
        const float SwingW = 2.0f;
        const float Gravity = 26f;
        const float LinkSpacing = 0.2f;

        public static Crate Build(GameManager gm, CrateDef d, Transform parent, Theme th)
        {
            var go = new GameObject("Crate");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Crate>();
            c.gm = gm;
            c.Size = d.size > 0.1f ? d.size : 2f;
            float h = d.h > 0.1f ? d.h : 4.5f;
            float s = c.Size;

            c.pivot = new GameObject("Pivot").transform;
            c.pivot.SetParent(go.transform, false);
            c.pivot.position = new Vector3(d.x, PivotY, d.z);
            c.length = PivotY - (h + s);

            // Chain: alternating links from the ceiling down to the crate
            var linkMat = Util.Mat(th.chain, 0.7f, 0.9f);
            int n = Mathf.CeilToInt(c.length / LinkSpacing);
            for (int i = 0; i < n; i++)
            {
                bool alt = i % 2 == 0;
                Util.Cube("link", c.pivot, new Vector3(0, -i * LinkSpacing - LinkSpacing / 2f, 0),
                          alt ? new Vector3(0.14f, 0.24f, 0.04f) : new Vector3(0.04f, 0.24f, 0.14f), linkMat);
            }
            Util.Cube("hook", c.pivot, new Vector3(0, -c.length + 0.1f, 0), new Vector3(0.3f, 0.12f, 0.3f), Util.Mat(th.metalDark, 0.5f, 0.8f));

            // Crate body (steel box with straps)
            c.body = new GameObject("Body").transform;
            c.body.SetParent(c.pivot, false);
            c.body.localPosition = new Vector3(0, -c.length - s / 2f, 0);
            var steel = Util.Mat(th.metal, 0.55f, 0.75f);
            var dark = Util.Mat(th.metalDark, 0.4f, 0.6f);
            var accent = Util.Mat(th.accent, 0.4f, 0.1f);
            Util.Cube("box", c.body, Vector3.zero, Vector3.one * s, steel);
            Util.Cube("strapX", c.body, Vector3.zero, new Vector3(s * 1.02f, s * 0.14f, s * 1.02f), dark);
            Util.Cube("strapY", c.body, Vector3.zero, new Vector3(s * 0.14f, s * 1.02f, s * 1.02f), dark);
            Util.Cube("stripe", c.body, new Vector3(0, -s * 0.38f, 0), new Vector3(s * 1.03f, s * 0.08f, s * 1.03f), accent);
            Util.Cube("lid", c.body, new Vector3(0, s * 0.5f, 0), new Vector3(s * 0.7f, 0.06f, s * 0.7f), dark);

            // Landing marker on the floor (red outline + dark fill)
            c.marker = new GameObject("Marker").transform;
            c.marker.SetParent(go.transform, false);
            var red = Util.Glow(Util.Hex("#ff3b30"), 1.4f);
            float m = s * 0.5f, t = 0.08f;
            Util.Cube("mN", c.marker, new Vector3(0, 0, m), new Vector3(s, 0.02f, t), red);
            Util.Cube("mS", c.marker, new Vector3(0, 0, -m), new Vector3(s, 0.02f, t), red);
            Util.Cube("mE", c.marker, new Vector3(m, 0, 0), new Vector3(t, 0.02f, s), red);
            Util.Cube("mW", c.marker, new Vector3(-m, 0, 0), new Vector3(t, 0.02f, s), red);
            Util.Cube("mFill", c.marker, Vector3.zero, new Vector3(s * 0.92f, 0.01f, s * 0.92f), Util.Mat(Util.Hex("#3a1414"), 0.1f));

            if (Mathf.Abs(d.swing) > 0.01f)
            {
                c.swingAngle = Mathf.Asin(Mathf.Clamp(d.swing / Mathf.Max(1f, c.length + s / 2f), -0.9f, 0.9f));
                c.swingAxis = d.axis == "z" ? Vector3.right : Vector3.forward;   // rotating about Z swings along X
                c.swingT = Random.Range(0f, 6f);
            }
            c.prevPos = c.body.position;
            c.UpdateMarker();
            return c;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // The chain keeps swinging (and slowly settles once the crate is gone)
            if (swingAngle != 0f)
            {
                swingT += dt;
                if (Released) swingAngle *= 1f - dt * 0.8f;
                pivot.localRotation = Quaternion.AngleAxis(swingAngle * Mathf.Sin(swingT * SwingW) * Mathf.Rad2Deg, swingAxis);
            }

            if (!Released)
            {
                vel = (body.position - prevPos) / dt;
                prevPos = body.position;
            }
            else if (!Landed)
            {
                vel.y -= Gravity * dt;
                body.position += vel * dt;
                body.Rotate(spin * dt, Space.World);
                if (Bottom < KillHeight) gm.CrushCheck(this);
                if (Bottom <= 0f) Land();
            }
            UpdateMarker();
        }

        void UpdateMarker()
        {
            if (Landed) { marker.gameObject.SetActive(false); return; }
            var p = body.position;
            marker.position = new Vector3(p.x, 0.03f, p.z);
            marker.rotation = Quaternion.identity;
        }

        public void Release()
        {
            if (Released) return;
            Released = true;
            vel.y = Mathf.Min(vel.y, 0f) - 0.5f;
            spin = new Vector3(Random.Range(-30f, 30f), Random.Range(-40f, 40f), Random.Range(-30f, 30f));
            body.SetParent(transform, true);    // detach from the chain, keep world position
        }

        void Land()
        {
            Landed = true;
            var p = body.position;
            body.position = new Vector3(p.x, Size / 2f, p.z);
            body.rotation = Quaternion.Euler(0, body.eulerAngles.y, 0);
            var col = body.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(Size * 0.95f, Size, Size * 0.95f);
            body.gameObject.AddComponent<Obstacle>();
            gm.OnCrateLanded(this);
        }
    }
}
