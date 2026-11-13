using UnityEngine;

namespace ChainDrop
{
    /// <summary>Walks toward the player along the floor. Dies when a falling crate lands on it.</summary>
    public class Robot : MonoBehaviour
    {
        public float speed;
        public int hp;
        public bool Dead { get; private set; }

        GameManager gm;
        Rigidbody2D rb;
        Transform visual;
        SpriteRenderer[] parts;
        Color[] baseColors;
        float stunUntil, flashUntil, walkT;
        HangingCrate lastHitBy;

        public const float Height = 1.3f;
        public const float CatchDistance = 0.75f;

        public static Robot Spawn(GameManager gm, RobotDef d, Transform parent, Theme th)
        {
            string type = string.IsNullOrEmpty(d.type) ? "walker" : d.type;
            float x = d.side == "R" ? 6.8f : -6.8f;
            var go = new GameObject("Robot_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, Height / 2f, 0);

            var r = go.AddComponent<Robot>();
            r.gm = gm;
            r.rb = go.AddComponent<Rigidbody2D>();
            r.rb.bodyType = RigidbodyType2D.Kinematic;
            r.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            Color body = th.robot, eye = Util.Hex("#ff3b30");
            float w = 0.8f, h = Height;
            switch (type)
            {
                case "runner": r.speed = 2.4f; r.hp = 1; body = Util.Hex("#e0a020"); w = 0.65f; h = 1.1f; break;
                case "tank": r.speed = 0.8f; r.hp = 2; body = Util.Hex("#7a5cc8"); w = 1.1f; h = 1.45f; break;
                default: r.speed = 1.1f; r.hp = 1; break;
            }
            col.size = new Vector2(w, h);
            go.transform.position = new Vector3(x, h / 2f, 0);

            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            r.visual = vis;
            Util.Box("legL", vis, new Vector2(-w * 0.22f, -h * 0.38f), new Vector2(w * 0.22f, h * 0.25f), th.metalDark, 20);
            Util.Box("legR", vis, new Vector2(w * 0.22f, -h * 0.38f), new Vector2(w * 0.22f, h * 0.25f), th.metalDark, 20);
            Util.Box("body", vis, new Vector2(0, -h * 0.02f), new Vector2(w, h * 0.55f), body, 21);
            Util.Box("head", vis, new Vector2(0, h * 0.36f), new Vector2(w * 0.7f, h * 0.28f), body * 0.85f + Color.black * 0.15f, 21);
            Util.Box("visor", vis, new Vector2(w * 0.1f, h * 0.37f), new Vector2(w * 0.45f, h * 0.1f), Color.black, 22);
            Util.Dot("eye", vis, new Vector2(w * 0.22f, h * 0.37f), h * 0.1f, eye, 23);
            Util.Box("antenna", vis, new Vector2(-w * 0.15f, h * 0.55f), new Vector2(0.05f, h * 0.12f), th.metalDark, 20);
            if (type == "tank")
                Util.Box("armor", vis, new Vector2(0, h * 0.05f), new Vector2(w * 1.1f, h * 0.18f), th.metal, 22);

            r.parts = vis.GetComponentsInChildren<SpriteRenderer>();
            r.baseColors = new Color[r.parts.Length];
            for (int i = 0; i < r.parts.Length; i++) r.baseColors[i] = r.parts[i].color;
            return r;
        }

        void FixedUpdate()
        {
            if (Dead || gm.State != GameState.Playing || Time.time < stunUntil) return;
            float target = gm.player.X;
            float dx = target - rb.position.x;
            if (Mathf.Abs(dx) < 0.01f) return;
            float dir = Mathf.Sign(dx);
            var p = rb.position;
            p.x += dir * speed * Time.fixedDeltaTime;
            rb.MovePosition(p);
            visual.localScale = new Vector3(dir, 1, 1);
            walkT += Time.fixedDeltaTime * speed * 6f;
        }

        void Update()
        {
            if (Dead) return;
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.06f, 0);
            bool flash = Time.time < flashUntil;
            for (int i = 0; i < parts.Length; i++) parts[i].color = flash ? Color.white : baseColors[i];

            if (gm.State == GameState.Playing && Mathf.Abs(transform.position.x - gm.player.X) < CatchDistance)
                gm.Lose("Robot seni yakaladı!");
        }

        void OnTriggerEnter2D(Collider2D o) => CheckHit(o);
        void OnTriggerStay2D(Collider2D o) => CheckHit(o);

        void CheckHit(Collider2D o)
        {
            if (Dead) return;
            var crate = o.GetComponent<HangingCrate>();
            if (crate == null || !crate.IsDeadly || crate == lastHitBy) return;
            lastHitBy = crate;
            hp--;
            if (hp <= 0) { Die(); return; }

            // Tank survives the first hit: knock back + stun
            stunUntil = Time.time + 0.9f;
            flashUntil = Time.time + 0.15f;
            float away = Mathf.Sign(transform.position.x - crate.transform.position.x);
            rb.position += new Vector2(away * 0.6f, 0);
            gm.Shake(0.2f);
            gm.sfx.Thud();
        }

        void Die()
        {
            Dead = true;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            visual.localScale = new Vector3(visual.localScale.x * 1.25f, 0.25f, 1);
            visual.localPosition = new Vector3(0, -Height * 0.38f, 0);
            for (int i = 0; i < parts.Length; i++) parts[i].color = baseColors[i] * 0.55f + Color.black * 0.45f;
            gm.Debris(transform.position, baseColors[2]);
            gm.OnRobotKilled(this);
        }
    }
}
