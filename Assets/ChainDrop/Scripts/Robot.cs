using UnityEngine;

namespace ChainDrop
{
    /// <summary>Chases the player across the arena, steering around landed crates. Crushed by falling crates.</summary>
    public class Robot : MonoBehaviour
    {
        public float Radius { get; private set; }
        public bool Dead { get; private set; }
        public Vector2 Pos => Util.G(rb.position);

        GameManager gm;
        Rigidbody rb;
        Transform visual, beacon;
        Renderer[] parts;
        Material[] baseMats;
        Material flashMat;
        float speed, stunUntil, flashUntil, walkT, sideSign = 1f, sideUntil;
        int hp;
        Crate lastHitBy;
        Color bodyColor;

        public static Robot Spawn(GameManager gm, RobotDef d, Transform parent, Theme th)
        {
            string type = string.IsNullOrEmpty(d.type) ? "walker" : d.type;
            var go = new GameObject("Robot_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.x, 0, d.z);

            var r = go.AddComponent<Robot>();
            r.gm = gm;
            Color body = th.robot;
            float k = 1f;   // size multiplier
            switch (type)
            {
                case "runner": r.speed = 2.1f; r.hp = 1; body = Util.Hex("#e0a020"); k = 0.8f; break;
                case "tank": r.speed = 0.85f; r.hp = 2; body = Util.Hex("#7a5cc8"); k = 1.35f; break;
                default: r.speed = 1.15f; r.hp = 1; break;
            }
            r.bodyColor = body;
            r.Radius = 0.42f * k;

            r.rb = go.AddComponent<Rigidbody>();
            r.rb.useGravity = false;
            r.rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            r.rb.interpolation = RigidbodyInterpolation.Interpolate;
            r.rb.mass = type == "tank" ? 6f : 1f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = r.Radius;
            col.height = 1.4f * k;
            col.center = new Vector3(0, 0.7f * k + 0.06f, 0);

            r.visual = new GameObject("Visual").transform;
            r.visual.SetParent(go.transform, false);
            var v = r.visual;
            var bodyMat = Util.Mat(body, 0.5f, 0.5f);
            var darkMat = Util.Mat(th.metalDark.Mul(0.7f), 0.4f, 0.6f);
            var visorMat = Util.Mat(new Color(0.04f, 0.04f, 0.06f), 0.9f, 0.2f);
            var eyeMat = Util.Glow(Util.Hex("#ff3b30"), 3f);

            // Tracks
            Util.Cube("trackL", v, new Vector3(-0.3f * k, 0.18f * k, 0), new Vector3(0.22f * k, 0.36f * k, 0.8f * k), darkMat);
            Util.Cube("trackR", v, new Vector3(0.3f * k, 0.18f * k, 0), new Vector3(0.22f * k, 0.36f * k, 0.8f * k), darkMat);
            // Body + chest plate
            Util.Cube("body", v, new Vector3(0, 0.68f * k, 0), new Vector3(0.7f * k, 0.6f * k, 0.6f * k), bodyMat);
            Util.Cube("chest", v, new Vector3(0, 0.66f * k, 0.31f * k), new Vector3(0.42f * k, 0.3f * k, 0.04f), Util.Mat(body.Mul(0.7f), 0.5f, 0.5f));
            // Arms
            Util.Cube("armL", v, new Vector3(-0.45f * k, 0.65f * k, 0.1f * k), new Vector3(0.14f * k, 0.45f * k, 0.18f * k), darkMat);
            Util.Cube("armR", v, new Vector3(0.45f * k, 0.65f * k, 0.1f * k), new Vector3(0.14f * k, 0.45f * k, 0.18f * k), darkMat);
            // Head with visor and glowing eye
            Util.Cube("head", v, new Vector3(0, 1.15f * k, 0), new Vector3(0.5f * k, 0.34f * k, 0.46f * k), bodyMat);
            Util.Cube("visor", v, new Vector3(0, 1.16f * k, 0.22f * k), new Vector3(0.4f * k, 0.12f * k, 0.04f), visorMat);
            Util.Prim(PrimitiveType.Sphere, "eye", v, new Vector3(0, 1.16f * k, 0.25f * k), Vector3.one * 0.1f * k, eyeMat);
            // Antenna + blinking beacon
            Util.Prim(PrimitiveType.Cylinder, "antenna", v, new Vector3(0.15f * k, 1.42f * k, 0), new Vector3(0.03f, 0.12f * k, 0.03f), darkMat);
            r.beacon = Util.Prim(PrimitiveType.Sphere, "beacon", v, new Vector3(0.15f * k, 1.58f * k, 0), Vector3.one * 0.08f, eyeMat).transform;
            if (type == "tank")
            {
                Util.Cube("armorL", v, new Vector3(-0.42f * k, 0.95f * k, 0), new Vector3(0.12f, 0.2f * k, 0.6f * k), Util.Mat(th.metal, 0.6f, 0.8f));
                Util.Cube("armorR", v, new Vector3(0.42f * k, 0.95f * k, 0), new Vector3(0.12f, 0.2f * k, 0.6f * k), Util.Mat(th.metal, 0.6f, 0.8f));
            }

            r.parts = v.GetComponentsInChildren<Renderer>();
            r.baseMats = new Material[r.parts.Length];
            for (int i = 0; i < r.parts.Length; i++) r.baseMats[i] = r.parts[i].sharedMaterial;
            r.flashMat = Util.Glow(Color.white, 1.5f);
            return r;
        }

        void FixedUpdate()
        {
            if (Dead) return;
            if (gm.State != GameState.Playing || Time.time < stunUntil)
            {
                rb.SetVel(Vector3.Lerp(rb.GetVel(), Vector3.zero, 0.15f));
                return;
            }

            Vector3 to = Util.W(gm.player.Pos - Pos);
            if (to.sqrMagnitude < 0.0001f) return;
            Vector3 dir = to.normalized;

            // Simple obstacle avoidance: if a landed crate is ahead, slide around it
            bool blocked = false;
            var origin = rb.position + Vector3.up * 0.5f;
            foreach (var hit in Physics.SphereCastAll(origin, Radius * 0.9f, dir, 1.0f))
            {
                if (hit.collider == null || hit.collider.GetComponent<Obstacle>() == null) continue;
                blocked = true;
                if (Time.time > sideUntil)
                {
                    var rel = hit.point - rb.position;
                    float cross = dir.x * rel.z - dir.z * rel.x;
                    sideSign = cross > 0 ? -1f : 1f;
                    sideUntil = Time.time + 0.6f;
                }
                break;
            }
            Vector3 steer = blocked ? (new Vector3(-dir.z, 0, dir.x) * sideSign + dir * 0.25f).normalized : dir;
            rb.SetVel(Vector3.Lerp(rb.GetVel(), steer * speed, 0.3f));
            walkT += Time.fixedDeltaTime * speed * 6f;
        }

        void Update()
        {
            if (Dead) return;
            var vel = rb.GetVel();
            vel.y = 0;
            if (vel.sqrMagnitude > 0.02f)
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(vel.normalized), Time.deltaTime * 10f);
            visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkT)) * 0.05f, 0);
            beacon.gameObject.SetActive(Mathf.Repeat(Time.time * 2f, 1f) < 0.5f);

            bool flash = Time.time < flashUntil;
            for (int i = 0; i < parts.Length; i++) parts[i].sharedMaterial = flash ? flashMat : baseMats[i];
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
            Vector2 away = Pos - crate.Ground;
            away = away.sqrMagnitude < 0.01f ? Vector2.down : away.normalized;
            rb.SetVel(Util.W(away * 7f));
            gm.Shake(0.25f);
            gm.sfx.Thud();
        }

        void Die()
        {
            Dead = true;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            rb.SetVel(Vector3.zero);
            rb.isKinematic = true;
            visual.localScale = new Vector3(1.3f, 0.18f, 1.3f);
            visual.localPosition = Vector3.zero;
            beacon.gameObject.SetActive(false);
            var dead = Util.Mat(bodyColor.Mul(0.35f), 0.3f, 0.4f);
            foreach (var p in parts) p.sharedMaterial = dead;
            gm.OilStain(Pos);
            gm.Debris(rb.position + Vector3.up * 0.6f, bodyColor);
            gm.OnRobotKilled(this);
        }
    }
}
