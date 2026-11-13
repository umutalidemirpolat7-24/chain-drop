using UnityEngine;

namespace ChainDrop
{
    /// <summary>The bait. Dragged left/right along the floor; robots always walk toward it.</summary>
    public class Player : MonoBehaviour
    {
        public float X => transform.position.x;
        public Vector2 GunTip => (Vector2)transform.position + new Vector2(facing * 0.45f, 0.35f);

        GameManager gm;
        Rigidbody2D rb;
        Transform visual;
        float targetX, facing = 1f, walkT;

        const float Speed = 8f;
        const float MinX = -5.1f, MaxX = 5.1f;

        public static Player Spawn(GameManager gm, float x, Transform parent, Theme th)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0.65f, 0);
            var p = go.AddComponent<Player>();
            p.gm = gm;
            p.targetX = x;
            p.rb = go.AddComponent<Rigidbody2D>();
            p.rb.bodyType = RigidbodyType2D.Kinematic;
            p.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.6f, 1.25f);

            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            p.visual = vis;
            Util.Box("legs", vis, new Vector2(0, -0.45f), new Vector2(0.45f, 0.35f), Util.Hex("#2b3a55"), 30);
            Util.Box("torso", vis, new Vector2(0, 0.0f), new Vector2(0.6f, 0.6f), th.player, 31);
            Util.Dot("head", vis, new Vector2(0, 0.48f), 0.42f, Util.Hex("#f2c9a0"), 32);
            Util.Box("hair", vis, new Vector2(-0.03f, 0.62f), new Vector2(0.4f, 0.14f), Util.Hex("#3b2a1e"), 33);
            Util.Box("gun", vis, new Vector2(0.38f, 0.32f), new Vector2(0.5f, 0.14f), Util.Hex("#222222"), 34);
            Util.Box("arm", vis, new Vector2(0.2f, 0.2f), new Vector2(0.3f, 0.12f), th.player, 33);
            return p;
        }

        public void SetTarget(float worldX) => targetX = Mathf.Clamp(worldX, MinX, MaxX);
        public void Face(float dir) { if (Mathf.Abs(dir) > 0.01f) facing = Mathf.Sign(dir); }

        void FixedUpdate()
        {
            if (gm.State != GameState.Playing) return;
            var pos = rb.position;
            float nx = Mathf.MoveTowards(pos.x, targetX, Speed * Time.fixedDeltaTime);
            if (Mathf.Abs(nx - pos.x) > 0.001f) { Face(nx - pos.x); walkT += Time.fixedDeltaTime * 14f; }
            rb.MovePosition(new Vector2(nx, pos.y));
        }

        bool squashed;

        void Update()
        {
            if (squashed) return;
            visual.localScale = new Vector3(facing, 1, 1);
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.05f, 0);
        }

        void OnTriggerEnter2D(Collider2D o) => CheckCrate(o);
        void OnTriggerStay2D(Collider2D o) => CheckCrate(o);

        void CheckCrate(Collider2D o)
        {
            if (gm.State != GameState.Playing) return;
            var crate = o.GetComponent<HangingCrate>();
            if (crate != null && crate.IsDeadly)
            {
                squashed = true;
                visual.localScale = new Vector3(facing * 1.3f, 0.25f, 1);
                visual.localPosition = new Vector3(0, -0.45f, 0);
                gm.Lose("Kasa senin üstüne düştü!");
            }
        }
    }
}
