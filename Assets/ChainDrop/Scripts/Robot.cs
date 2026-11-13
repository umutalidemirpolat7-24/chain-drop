using UnityEngine;
using UnityEngine.Rendering;

namespace ChainDrop
{
    /// <summary>Chases the player across the arena, steering around landed crates. Crushed by falling crates.</summary>
    public class Robot : MonoBehaviour
    {
        public float Radius { get; private set; }
        public bool Dead { get; private set; }
        public Vector2 Pos => rb.position;

        GameManager gm;
        Rigidbody2D rb;
        SortingGroup sg;
        Transform visual, eye;
        SpriteRenderer blink;
        SpriteRenderer[] parts;
        Color[] baseColors;
        float speed, stunUntil, flashUntil, walkT, sideSign = 1f, sideUntil;
        int hp;
        Crate lastHitBy;

        public static Robot Spawn(GameManager gm, RobotDef d, Transform parent, Theme th)
        {
            string type = string.IsNullOrEmpty(d.type) ? "walker" : d.type;
            var go = new GameObject("Robot_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector2(d.x, d.y);

            var r = go.AddComponent<Robot>();
            r.gm = gm;
            Color body = th.robot;
            float w = 0.72f, h = 0.9f;
            switch (type)
            {
                case "runner": r.speed = 3.3f; r.hp = 1; body = Util.Hex("#e0a020"); w = 0.6f; h = 0.75f; r.Radius = 0.3f; break;
                case "tank": r.speed = 1.15f; r.hp = 2; body = Util.Hex("#7a5cc8"); w = 1.05f; h = 1.1f; r.Radius = 0.52f; break;
                default: r.speed = 1.7f; r.hp = 1; r.Radius = 0.36f; break;
            }

            r.rb = go.AddComponent<Rigidbody2D>();
            r.rb.gravityScale = 0f;
            r.rb.freezeRotation = true;
            r.rb.mass = type == "tank" ? 4f : 1f;
            r.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = r.Radius;
            r.sg = go.AddComponent<SortingGroup>();

            Util.Blob("shadow", go.transform, new Vector2(0, -0.02f), new Vector2(w * 1.5f, w * 0.75f), new Color(0, 0, 0, 0.45f), 0);
            r.visual = new GameObject("Visual").transform;
            r.visual.SetParent(go.transform, false);
            var v = r.visual;
            Util.RBox("treadL", v, new Vector2(-w * 0.36f, 0.1f), new Vector2(w * 0.28f, h * 0.28f), th.metalDark.Mul(0.7f), 1);
            Util.RBox("treadR", v, new Vector2(w * 0.36f, 0.1f), new Vector2(w * 0.28f, h * 0.28f), th.metalDark.Mul(0.7f), 1);
            Util.RBox("body", v, new Vector2(0, h * 0.45f), new Vector2(w, h * 0.6f), body, 2);
            Util.RBox("chest", v, new Vector2(0, h * 0.4f), new Vector2(w * 0.6f, h * 0.22f), body.Mul(0.75f), 3);
            Util.RBox("head", v, new Vector2(0, h * 0.92f), new Vector2(w * 0.78f, h * 0.36f), body.Mul(0.88f), 4);
            Util.RBox("visor", v, new Vector2(0, h * 0.92f), new Vector2(w * 0.6f, h * 0.16f), new Color(0.05f, 0.05f, 0.07f), 5);
            r.eye = Util.Dot("eye", v, new Vector2(0, h * 0.92f), h * 0.13f, Util.Hex("#ff3b30"), 6).transform;
            Util.Box("antenna", v, new Vector2(w * 0.2f, h * 1.2f), new Vector2(0.05f, h * 0.2f), th.metalDark, 3);
            r.blink = Util.Dot("blink", v, new Vector2(w * 0.2f, h * 1.32f), 0.1f, Util.Hex("#ff3b30"), 4);
            if (type == "tank")
                Util.RBox("armor", v, new Vector2(0, h * 0.5f), new Vector2(w * 1.12f, h * 0.18f), th.metal, 6);

            r.parts = v.GetComponentsInChildren<SpriteRenderer>();
            r.baseColors = new Color[r.parts.Length];
            for (int i = 0; i < r.parts.Length; i++) r.baseColors[i] = r.parts[i].color;
            return r;
        }

        void FixedUpdate()
        {
            if (Dead) return;
            if (gm.State != GameState.Playing || Time.time < stunUntil)
            {
                rb.SetVel(Vector2.Lerp(rb.GetVel(), Vector2.zero, 0.2f));
                return;
            }

            Vector2 to = gm.player.Pos - rb.position;
            if (to.sqrMagnitude < 0.0001f) return;
            Vector2 dir = to.normalized;

            // Simple obstacle avoidance: if a landed crate is ahead, slide around it
            bool blocked = false;
            foreach (var hit in Physics2D.CircleCastAll(rb.position, Radius * 0.9f, dir, 0.9f))
            {
                if (hit.collider == null || hit.collider.GetComponent<Obstacle>() == null) continue;
                blocked = true;
                if (Time.time > sideUntil)
                {
                    float cross = dir.x * (hit.point.y - rb.position.y) - dir.y * (hit.point.x - rb.position.x);
                    sideSign = cross > 0 ? -1f : 1f;
                    sideUntil = Time.time + 0.6f;
                }
                break;
            }
            Vector2 steer = blocked ? (new Vector2(-dir.y, dir.x) * sideSign + dir * 0.25f).normalized : dir;
            rb.SetVel(Vector2.Lerp(rb.GetVel(), steer * speed, 0.35f));
            walkT += Time.fixedDeltaTime * speed * 5f;
        }

        void Update()
        {
            sg.sortingOrder = Util.DepthOrder(transform.position.y);
            if (Dead) return;

            var vel = rb.GetVel();
            if (vel.sqrMagnitude > 0.01f)
            {
                Vector2 d = vel.normalized;
                eye.localPosition = new Vector3(d.x * 0.16f, eye.localPosition.y, 0);
            }
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.05f, 0);
            blink.enabled = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;

            bool flash = Time.time < flashUntil;
            for (int i = 0; i < parts.Length; i++) parts[i].color = flash ? Color.white : baseColors[i];
        }

        /// <summary>Called by a falling crate whose footprint overlaps this robot.</summary>
        public void Hit(Crate crate)
        {
            if (Dead || crate == lastHitBy) return;
            lastHitBy = crate;
            hp--;
            if (hp <= 0) { Die(); return; }

            // Tank survives the first hit: stunned and knocked away from the crate
            stunUntil = Time.time + 1.1f;
            flashUntil = Time.time + 0.18f;
            Vector2 away = rb.position - crate.Ground;
            away = away.sqrMagnitude < 0.01f ? Vector2.down : away.normalized;
            rb.SetVel(away * 7f);
            gm.Shake(0.25f);
            gm.sfx.Thud();
        }

        void Die()
        {
            Dead = true;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            rb.SetVel(Vector2.zero);
            rb.bodyType = RigidbodyType2D.Static;
            visual.localScale = new Vector3(1.35f, 0.28f, 1);
            visual.localPosition = Vector3.zero;
            blink.enabled = false;
            for (int i = 0; i < parts.Length; i++) parts[i].color = baseColors[i].Mul(0.45f);
            gm.OilStain(rb.position);
            gm.Debris(rb.position + Vector2.up * 0.5f, baseColors[2]);
            gm.OnRobotKilled(this);
        }
    }
}
