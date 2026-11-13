using System.Collections.Generic;
using UnityEngine;

namespace ChainDrop
{
    /// <summary>One chain link. Tapping near it breaks the joint above it, so the crate falls.</summary>
    public class ChainLink : MonoBehaviour
    {
        public HangingCrate crate;
        public HingeJoint2D joint;
        public bool Broken => joint == null;

        public void Break()
        {
            if (joint != null) Destroy(joint);
            joint = null;
            crate.Release();
        }
    }

    /// <summary>A steel crate hanging from the ceiling on a physics chain.</summary>
    public class HangingCrate : MonoBehaviour
    {
        public Rigidbody2D body;
        public bool released;
        public float size;
        public readonly List<ChainLink> links = new List<ChainLink>();
        GameManager gm;

        const float LinkLen = 0.42f;
        const float Gravity = 1.6f;

        /// <summary>Deadly only while it is actually falling, so a crate lying on the floor is harmless.</summary>
        public bool IsDeadly => released && body != null && body.GetVel().y < -1.5f;

        public static HangingCrate Build(GameManager gm, CrateDef d, float ceilingY, Transform parent, Theme th)
        {
            var root = new GameObject("CrateRig").transform;
            root.SetParent(parent, false);

            // Ceiling anchor
            var anchorGo = new GameObject("Anchor");
            anchorGo.transform.SetParent(root, false);
            anchorGo.transform.position = new Vector3(d.x, ceilingY, 0);
            var anchorRb = anchorGo.AddComponent<Rigidbody2D>();
            anchorRb.bodyType = RigidbodyType2D.Static;
            Util.Box("bracket", anchorGo.transform, new Vector2(0, 0.1f), new Vector2(0.7f, 0.3f), th.metalDark, 5);
            Util.Box("bolt", anchorGo.transform, new Vector2(0, -0.1f), new Vector2(0.18f, 0.25f), th.metal, 6);

            // Crate body (created first so links can reference it)
            int n = Mathf.Max(1, Mathf.RoundToInt(d.chain / LinkLen));
            float size = d.size > 0.1f ? d.size : 1.4f;
            var crateGo = new GameObject("Crate");
            crateGo.transform.SetParent(root, false);
            crateGo.transform.position = new Vector3(d.x, ceilingY - n * LinkLen - size / 2f, 0);
            var crate = crateGo.AddComponent<HangingCrate>();
            crate.gm = gm;
            crate.size = size;
            var rb = crateGo.AddComponent<Rigidbody2D>();
            rb.mass = 2f;
            rb.gravityScale = Gravity;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = crateGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(size, size);
            crate.body = rb;

            // Crate visuals: steel frame + darker inside + straps
            Util.Box("frame", crateGo.transform, Vector2.zero, new Vector2(size, size), th.metal, 10);
            Util.Box("inner", crateGo.transform, Vector2.zero, new Vector2(size * 0.78f, size * 0.78f), th.metalDark, 11);
            Util.Box("strapV", crateGo.transform, Vector2.zero, new Vector2(size * 0.14f, size * 0.78f), th.metal, 12);
            Util.Box("strapH", crateGo.transform, Vector2.zero, new Vector2(size * 0.78f, size * 0.14f), th.metal, 12);
            Util.Box("ring", crateGo.transform, new Vector2(0, size / 2f + 0.05f), new Vector2(0.3f, 0.12f), th.metalDark, 9);

            // Chain links with hinge joints
            Rigidbody2D prev = anchorRb;
            for (int i = 0; i < n; i++)
            {
                var lg = new GameObject("Link" + i);
                lg.transform.SetParent(root, false);
                lg.transform.position = new Vector3(d.x, ceilingY - LinkLen * (i + 0.5f), 0);
                var lrb = lg.AddComponent<Rigidbody2D>();
                lrb.mass = 0.3f;
                lrb.gravityScale = Gravity;
                lrb.interpolation = RigidbodyInterpolation2D.Interpolate;
                var lc = lg.AddComponent<BoxCollider2D>();
                lc.isTrigger = true;                       // tap target only, no physical contact
                lc.size = new Vector2(0.45f, LinkLen);
                var hj = lg.AddComponent<HingeJoint2D>();
                hj.connectedBody = prev;
                hj.autoConfigureConnectedAnchor = false;
                hj.anchor = new Vector2(0, LinkLen / 2f);
                hj.connectedAnchor = i == 0 ? Vector2.zero : new Vector2(0, -LinkLen / 2f);
                bool alt = i % 2 == 0;
                Util.Box("v", lg.transform, Vector2.zero, alt ? new Vector2(0.16f, LinkLen * 1.05f) : new Vector2(0.09f, LinkLen * 1.05f), th.chain, 7);
                var link = lg.AddComponent<ChainLink>();
                link.crate = crate;
                link.joint = hj;
                crate.links.Add(link);
                prev = lrb;
            }

            var cj = crateGo.AddComponent<HingeJoint2D>();
            cj.connectedBody = prev;
            cj.autoConfigureConnectedAnchor = false;
            cj.anchor = new Vector2(0, size / 2f);
            cj.connectedAnchor = new Vector2(0, -LinkLen / 2f);

            if (Mathf.Abs(d.swing) > 0.01f)
            {
                rb.SetVel(new Vector2(d.swing, 0));
                foreach (var l in crate.links) l.GetComponent<Rigidbody2D>().SetVel(new Vector2(d.swing * 0.5f, 0));
            }
            return crate;
        }

        public void Release()
        {
            if (released) return;
            released = true;
            body.SetVel(body.GetVel() + new Vector2(0, -0.5f));
        }

        void OnCollisionEnter2D(Collision2D c)
        {
            if (gm == null) return;
            float v = c.relativeVelocity.magnitude;
            if (released && v > 4f)
            {
                gm.Shake(Mathf.Clamp(v * 0.025f, 0.08f, 0.3f));
                gm.sfx.Thud();
                gm.Dust(c.GetContact(0).point);
            }
        }
    }
}
