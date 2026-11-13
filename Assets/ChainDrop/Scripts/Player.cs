using UnityEngine;
using UnityEngine.Rendering;

namespace ChainDrop
{
    /// <summary>The bait. Runs freely around the arena; robots always chase it. Aims and fires at crates.</summary>
    public class Player : MonoBehaviour
    {
        public Vector2 Pos => rb.position;
        public const float Radius = 0.32f;

        GameManager gm;
        Rigidbody2D rb;
        SortingGroup sg;
        Transform visual, gunPivot, body;
        SpriteRenderer shadow;
        Vector2 move, aim = Vector2.up;
        float aimHoldUntil, recoil, walkT;
        bool squashed;

        const float Speed = 4.6f;

        public Vector2 GunTip => (Vector2)gunPivot.position + aim * 0.62f;

        public static Player Spawn(GameManager gm, Vector2 at, Transform parent, Theme th)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var p = go.AddComponent<Player>();
            p.gm = gm;
            p.rb = go.AddComponent<Rigidbody2D>();
            p.rb.gravityScale = 0f;
            p.rb.freezeRotation = true;
            p.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius;
            p.sg = go.AddComponent<SortingGroup>();

            p.shadow = Util.Blob("shadow", go.transform, new Vector2(0, -0.02f), new Vector2(0.95f, 0.5f), new Color(0, 0, 0, 0.45f), 0);
            p.visual = new GameObject("Visual").transform;
            p.visual.SetParent(go.transform, false);
            p.body = new GameObject("Body").transform;
            p.body.SetParent(p.visual, false);
            Util.RBox("legs", p.body, new Vector2(0, 0.12f), new Vector2(0.42f, 0.3f), Util.Hex("#2b3a55"), 1);
            Util.RBox("torso", p.body, new Vector2(0, 0.45f), new Vector2(0.58f, 0.5f), th.player, 2);
            Util.RBox("vest", p.body, new Vector2(0, 0.43f), new Vector2(0.4f, 0.32f), th.player.Mul(0.75f), 3);
            Util.Dot("head", p.body, new Vector2(0, 0.86f), 0.4f, Util.Hex("#f2c9a0"), 4);
            Util.RBox("helmet", p.body, new Vector2(0, 0.98f), new Vector2(0.44f, 0.2f), Util.Hex("#4a5a3a"), 5);

            p.gunPivot = new GameObject("GunPivot").transform;
            p.gunPivot.SetParent(p.visual, false);
            p.gunPivot.localPosition = new Vector3(0, 0.5f, 0);
            Util.Box("barrel", p.gunPivot, new Vector2(0.38f, 0), new Vector2(0.55f, 0.12f), Util.Hex("#1d1f24"), 8);
            Util.Box("stock", p.gunPivot, new Vector2(0.1f, -0.03f), new Vector2(0.22f, 0.16f), Util.Hex("#3a2c22"), 7);
            Util.Dot("hand", p.gunPivot, new Vector2(0.18f, -0.02f), 0.14f, Util.Hex("#f2c9a0"), 9);
            return p;
        }

        public void Move(Vector2 input) => move = Vector2.ClampMagnitude(input, 1f);

        public void AimAt(Vector2 target)
        {
            var d = target - (Vector2)gunPivot.position;
            if (d.sqrMagnitude > 0.001f) aim = d.normalized;
            aimHoldUntil = Time.time + 0.45f;
            recoil = 0.16f;
        }

        void FixedUpdate()
        {
            bool playing = gm.State == GameState.Playing;
            rb.SetVel(playing ? move * Speed : Vector2.zero);
        }

        void Update()
        {
            sg.sortingOrder = Util.DepthOrder(transform.position.y);
            if (squashed) return;

            bool moving = move.sqrMagnitude > 0.01f && gm.State == GameState.Playing;
            if (moving && Time.time > aimHoldUntil) aim = Vector2.Lerp(aim, move.normalized, Time.deltaTime * 14f).normalized;
            if (moving) walkT += Time.deltaTime * 16f;

            float face = aim.x >= 0 ? 1f : -1f;
            body.localScale = new Vector3(face, 1, 1);
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.05f, 0);

            // Gun rotates toward aim; flip so it never renders upside down; recoil kicks it back
            float ang = Mathf.Atan2(aim.y * 0.8f, aim.x) * Mathf.Rad2Deg;
            gunPivot.localRotation = Quaternion.Euler(0, 0, ang);
            gunPivot.localScale = new Vector3(1, face, 1);
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.2f);
            gunPivot.localPosition = new Vector3(0, 0.5f, 0) - (Vector3)(aim * recoil);
        }

        public void Squash()
        {
            squashed = true;
            visual.localScale = new Vector3(1.4f, 0.25f, 1);
        }
    }
}
