using UnityEngine;

namespace ChainDrop
{
    /// <summary>The bait. Runs freely around the arena; robots always chase it. Turns, aims and fires at chains.</summary>
    public class Player : MonoBehaviour
    {
        public Vector2 Pos => Util.G(rb.position);
        public const float Radius = 0.35f;
        public Vector3 GunTip => muzzle.position;

        GameManager gm;
        Rigidbody rb;
        Transform visual, gun, muzzle, armPivot;
        Vector3 move;
        Vector3 aimDir = Vector3.forward;
        float aimHoldUntil, recoil, walkT;
        bool squashed;

        const float Speed = 4.4f;

        public static Player Spawn(GameManager gm, Vector2 at, Transform parent, Theme th)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = Util.W(at);
            var p = go.AddComponent<Player>();
            p.gm = gm;
            p.rb = go.AddComponent<Rigidbody>();
            p.rb.useGravity = false;
            p.rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            p.rb.interpolation = RigidbodyInterpolation.Interpolate;
            p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            p.rb.mass = 2f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = Radius;
            col.height = 1.6f;
            col.center = new Vector3(0, 0.86f, 0);

            p.visual = new GameObject("Visual").transform;
            p.visual.SetParent(go.transform, false);
            var v = p.visual;
            var uniform = Util.Mat(th.player, 0.25f);
            var dark = Util.Mat(Util.Hex("#2b3a55"), 0.3f);
            var skin = Util.Mat(Util.Hex("#f2c9a0"), 0.2f);
            var helmet = Util.Mat(Util.Hex("#4a5a3a"), 0.4f, 0.2f);
            var gunMat = Util.Mat(Util.Hex("#1d1f24"), 0.6f, 0.8f);

            Util.Prim(PrimitiveType.Capsule, "legL", v, new Vector3(-0.13f, 0.32f, 0), new Vector3(0.2f, 0.32f, 0.2f), dark);
            Util.Prim(PrimitiveType.Capsule, "legR", v, new Vector3(0.13f, 0.32f, 0), new Vector3(0.2f, 0.32f, 0.2f), dark);
            Util.Prim(PrimitiveType.Capsule, "torso", v, new Vector3(0, 0.95f, 0), new Vector3(0.5f, 0.38f, 0.36f), uniform);
            Util.Cube("vest", v, new Vector3(0, 0.95f, 0.05f), new Vector3(0.46f, 0.42f, 0.32f), Util.Mat(th.player.Mul(0.7f), 0.3f));
            Util.Prim(PrimitiveType.Sphere, "head", v, new Vector3(0, 1.42f, 0), Vector3.one * 0.34f, skin);
            Util.Prim(PrimitiveType.Sphere, "helmet", v, new Vector3(0, 1.5f, -0.02f), new Vector3(0.38f, 0.24f, 0.4f), helmet);

            p.armPivot = new GameObject("ArmPivot").transform;
            p.armPivot.SetParent(v, false);
            p.armPivot.localPosition = new Vector3(0.22f, 1.05f, 0.1f);
            p.gun = new GameObject("Gun").transform;
            p.gun.SetParent(p.armPivot, false);
            Util.Cube("body", p.gun, new Vector3(0, 0, 0.25f), new Vector3(0.14f, 0.18f, 0.5f), gunMat);
            Util.Prim(PrimitiveType.Cylinder, "barrel", p.gun, new Vector3(0, 0.03f, 0.62f), new Vector3(0.07f, 0.2f, 0.07f), gunMat)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            Util.Cube("mag", p.gun, new Vector3(0, -0.14f, 0.22f), new Vector3(0.08f, 0.16f, 0.1f), gunMat);
            Util.Prim(PrimitiveType.Sphere, "hand", p.gun, new Vector3(0, -0.05f, 0.05f), Vector3.one * 0.13f, skin);
            p.muzzle = new GameObject("Muzzle").transform;
            p.muzzle.SetParent(p.gun, false);
            p.muzzle.localPosition = new Vector3(0, 0.03f, 0.85f);
            return p;
        }

        public void Move(Vector2 input) => move = Util.W(Vector2.ClampMagnitude(input, 1f));

        public void AimAt(Vector3 target)
        {
            var d = target - armPivot.position;
            if (d.sqrMagnitude > 0.001f) aimDir = d.normalized;
            aimHoldUntil = Time.time + 0.5f;
            recoil = 0.18f;
        }

        void FixedUpdate()
        {
            bool playing = gm.State == GameState.Playing;
            rb.SetVel(playing ? move * Speed : Vector3.zero);
        }

        void Update()
        {
            if (squashed) return;
            bool moving = move.sqrMagnitude > 0.01f && gm.State == GameState.Playing;
            bool aiming = Time.time < aimHoldUntil;

            // Body faces where it runs, or toward the target while shooting
            Vector3 face = aiming ? new Vector3(aimDir.x, 0, aimDir.z) : moving ? move : visual.forward;
            if (face.sqrMagnitude > 0.001f)
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(face.normalized), Time.deltaTime * 14f);

            // Gun pitches up toward the chain while aiming
            Quaternion gunRot = aiming ? Quaternion.LookRotation(aimDir) : visual.rotation * Quaternion.Euler(10, 0, 0);
            armPivot.rotation = Quaternion.Slerp(armPivot.rotation, gunRot, Time.deltaTime * 20f);
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.2f);
            gun.localPosition = new Vector3(0, 0, -recoil);

            if (moving) walkT += Time.deltaTime * 14f;
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.06f, 0);
        }

        public void Squash()
        {
            squashed = true;
            visual.localScale = new Vector3(1.4f, 0.2f, 1.4f);
        }
    }
}
