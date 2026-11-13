using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChainDrop
{
    public enum GameState { Loading, Playing, Won, Lost }

    /// <summary>
    /// Boots itself in any scene (no setup needed: open the project, press Play).
    /// Real 3D arena with lights and shadows. Levels come from Resources/ChainDrop/levels.json.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public static bool AutoBoot = true;

        public const float MinX = -5f, MaxX = 5f, MinZ = -7f, MaxZ = 6f;
        const float Pitch = 58f;

        public GameState State { get; private set; } = GameState.Loading;
        public Player player;
        public Sfx sfx;

        Camera cam;
        Light sun;
        LevelPack pack;
        int levelIndex;
        LevelDef L;
        Theme theme;
        Transform levelRoot;
        readonly List<Robot> robots = new List<Robot>();
        readonly List<Crate> crates = new List<Crate>();
        readonly List<Barrel> barrels = new List<Barrel>();
        public IReadOnlyList<Crate> Crates => crates;
        int railIndex;
        public int NextRailIndex() => railIndex++;
        int kills, shots, shotsUsed, stars;
        string loseReason = "";
        float shakeAmt, levelStartTime, defaultFixedDt, lastAspect = -1f;
        Vector3 camBase;
        Rect restartRect;

        bool joyActive;
        Vector2 joyStart, joyNow;
        float JoyMax => Screen.height * 0.07f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!AutoBoot || I != null) return;
            new GameObject("ChainDrop_Game").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            defaultFixedDt = 0.02f;
            Time.fixedDeltaTime = defaultFixedDt;
            Physics.gravity = new Vector3(0, -20f, 0);

            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
                cg.AddComponent<AudioListener>();
            }
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;

            sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.color = new Color(1f, 0.95f, 0.88f);

            sfx = gameObject.AddComponent<Sfx>();
            var ta = Resources.Load<TextAsset>("ChainDrop/levels");
            pack = JsonUtility.FromJson<LevelPack>(ta.text);
            // In the editor always start from level 1; on a phone continue where the player left off
            levelIndex = Application.isEditor ? 0 : Mathf.Clamp(PlayerPrefs.GetInt("cd3_level", 0), 0, pack.levels.Length - 1);
        }

        void Start() => LoadLevel(levelIndex);

        // ───────────────────────── Level building ─────────────────────────

        public void LoadLevel(int index)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDt;
            if (levelRoot != null) { levelRoot.gameObject.SetActive(false); Destroy(levelRoot.gameObject); }
            robots.Clear();
            crates.Clear();
            barrels.Clear();
            railIndex = 0;
            kills = 0; shotsUsed = 0; stars = 0; shakeAmt = 0; joyActive = false;

            levelIndex = Mathf.Clamp(index, 0, pack.levels.Length - 1);
            L = pack.levels[levelIndex];
            theme = Theme.Get(L.theme);
            shots = L.shots;

            levelRoot = new GameObject("Level_" + (levelIndex + 1)).transform;
            lastAspect = -1f;
            FitCamera();
            BuildArena();
            foreach (var c in L.crates) crates.Add(Crate.Build(this, c, levelRoot, theme));
            if (L.barrels != null) foreach (var b in L.barrels) barrels.Add(Barrel.Spawn(b, levelRoot, theme));
            player = Player.Spawn(this, new Vector2(L.px, L.pz), levelRoot, theme);

            levelStartTime = Time.time;
            State = GameState.Playing;
            StartCoroutine(SpawnRobots());
        }

        IEnumerator SpawnRobots()
        {
            float t0 = Time.time;
            foreach (var d in L.robots.OrderBy(r => r.delay))
            {
                while (Time.time - t0 < d.delay) yield return null;
                if (State != GameState.Playing) yield break;
                robots.Add(Robot.Spawn(this, d, levelRoot, theme));
                Sparks(new Vector3(d.x, 1f, d.z), theme.accent, 8);
            }
        }

        /// <summary>Moves the camera back until the whole arena fits the screen (below the HUD).</summary>
        void FitCamera()
        {
            if (cam == null || Mathf.Approximately(lastAspect, cam.aspect)) return;
            lastAspect = cam.aspect;
            var rot = Quaternion.Euler(Pitch, 0, 0);
            var target = new Vector3(0, 0, -0.8f);
            var pts = new[]
            {
                new Vector3(MinX - 0.7f, 0, MinZ - 0.7f), new Vector3(MaxX + 0.7f, 0, MinZ - 0.7f),
                new Vector3(MinX - 0.7f, 3.5f, MaxZ + 0.7f), new Vector3(MaxX + 0.7f, 3.5f, MaxZ + 0.7f),
            };
            Vector3 pos = target;
            for (float d = 8f; d < 90f; d += 0.25f)
            {
                pos = target - rot * Vector3.forward * d;
                cam.transform.SetPositionAndRotation(pos, rot);
                bool ok = true;
                foreach (var p in pts)
                {
                    var v = cam.WorldToViewportPoint(p);
                    if (v.x < 0.02f || v.x > 0.98f || v.y < 0.04f || v.y > 0.84f) { ok = false; break; }
                }
                if (ok) break;
            }
            camBase = pos;
        }

        void BuildArena()
        {
            cam.backgroundColor = theme.bg;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(theme.bg, new Color(0.55f, 0.58f, 0.65f), 0.55f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = theme.bg;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 55f;

            var env = new GameObject("Arena").transform;
            env.SetParent(levelRoot, false);
            float w = MaxX - MinX, dpt = MaxZ - MinZ, cx = (MinX + MaxX) / 2f, cz = (MinZ + MaxZ) / 2f;

            var wallMat = Util.Mat(theme.metalDark, 0.35f, 0.6f);
            var wallTrim = Util.Mat(theme.metal, 0.5f, 0.7f);
            var accentMat = Util.Mat(theme.accent, 0.4f, 0.1f);
            var blackMat = Util.Mat(new Color(0.06f, 0.06f, 0.07f), 0.3f);

            // Surroundings + steel plate floor
            Util.Cube("outside", env, new Vector3(0, -0.3f, 0), new Vector3(60, 0.4f, 60), Util.Mat(theme.bg.Mul(0.8f), 0.1f));
            var plate = Util.SteelPlate(theme.ground.Mul(1.15f), theme.ground);
            var floor = Util.Cube("floor", env, new Vector3(cx, -0.1f, cz), new Vector3(w, 0.2f, dpt), Util.Textured(plate, new Vector2(w / 2f, dpt / 2f), Color.white));
            var fc = floor.AddComponent<BoxCollider>();
            fc.size = Vector3.one;

            // Hazard stripes along the edges
            for (float x = MinX + 0.2f; x < MaxX - 0.2f; x += 0.8f)
            {
                Util.Cube("hzN", env, new Vector3(x + 0.2f, 0.005f, MaxZ - 0.25f), new Vector3(0.4f, 0.01f, 0.3f), accentMat);
                Util.Cube("hzS", env, new Vector3(x + 0.2f, 0.005f, MinZ + 0.25f), new Vector3(0.4f, 0.01f, 0.3f), accentMat);
            }
            for (float z = MinZ + 0.6f; z < MaxZ - 0.6f; z += 0.8f)
            {
                Util.Cube("hzW", env, new Vector3(MinX + 0.25f, 0.005f, z + 0.2f), new Vector3(0.3f, 0.01f, 0.4f), accentMat);
                Util.Cube("hzE", env, new Vector3(MaxX - 0.25f, 0.005f, z + 0.2f), new Vector3(0.3f, 0.01f, 0.4f), accentMat);
            }

            // Walls: tall back wall with pipes and lamps, low side/front walls so the view stays open
            Wall(env, new Vector3(cx, 2.2f, MaxZ + 0.35f), new Vector3(w + 1.4f, 4.4f, 0.7f), wallMat);
            Wall(env, new Vector3(cx, 0.35f, MinZ - 0.35f), new Vector3(w + 1.4f, 0.7f, 0.7f), wallMat);
            Wall(env, new Vector3(MinX - 0.35f, 0.6f, cz), new Vector3(0.7f, 1.2f, dpt), wallMat);
            Wall(env, new Vector3(MaxX + 0.35f, 0.6f, cz), new Vector3(0.7f, 1.2f, dpt), wallMat);
            Util.Cube("trimS", env, new Vector3(cx, 0.72f, MinZ - 0.35f), new Vector3(w + 1.4f, 0.06f, 0.74f), wallTrim);
            Util.Cube("trimW", env, new Vector3(MinX - 0.35f, 1.22f, cz), new Vector3(0.74f, 0.06f, dpt), wallTrim);
            Util.Cube("trimE", env, new Vector3(MaxX + 0.35f, 1.22f, cz), new Vector3(0.74f, 0.06f, dpt), wallTrim);

            var pipeMat = Util.Mat(theme.metal.Mul(0.8f), 0.6f, 0.9f);
            foreach (var y in new[] { 2.4f, 2.9f })
                Util.Prim(PrimitiveType.Cylinder, "pipe", env, new Vector3(cx, y, MaxZ - 0.05f), new Vector3(0.22f, (w + 1.4f) / 2f, 0.22f), pipeMat)
                    .transform.localRotation = Quaternion.Euler(0, 0, 90);
            for (int i = 0; i < 4; i++)
            {
                float x = MinX + 1.25f + i * (w - 2.5f) / 3f;
                Util.Cube("panel", env, new Vector3(x, 1.2f, MaxZ - 0.02f), new Vector3(1.2f, 0.8f, 0.05f), wallTrim);
                Util.Cube("panelStripe", env, new Vector3(x, 1.2f, MaxZ - 0.06f), new Vector3(1.0f, 0.12f, 0.03f), accentMat);
                Util.Prim(PrimitiveType.Sphere, "lamp", env, new Vector3(x, 3.6f, MaxZ - 0.1f), Vector3.one * 0.3f, Util.Glow(theme.accent, 4f));
                if (i == 0 || i == 3)
                {
                    var pl = new GameObject("lampLight").AddComponent<Light>();
                    pl.transform.SetParent(env, false);
                    pl.transform.position = new Vector3(x, 3.4f, MaxZ - 1f);
                    pl.type = LightType.Point;
                    pl.color = theme.accent;
                    pl.range = 9f;
                    pl.intensity = 2.5f;
                }
            }
            // Pillars on the side walls
            for (float z = MinZ + 1f; z <= MaxZ; z += 3.25f)
            {
                Util.Cube("pillarW", env, new Vector3(MinX - 0.4f, 1.0f, z), new Vector3(0.8f, 2f, 0.5f), wallTrim);
                Util.Cube("pillarE", env, new Vector3(MaxX + 0.4f, 1.0f, z), new Vector3(0.8f, 2f, 0.5f), wallTrim);
            }
            // Props outside the arena for depth
            var barrel = Util.Mat(Util.Hex("#8a3b2a"), 0.4f, 0.5f);
            foreach (var p in new[] { new Vector3(MinX - 1.6f, 0.6f, MinZ + 1.5f), new Vector3(MaxX + 1.6f, 0.6f, MinZ + 3f), new Vector3(MaxX + 1.7f, 0.6f, MaxZ - 2f), new Vector3(MinX - 1.7f, 0.6f, MaxZ - 3.5f) })
            {
                Util.Prim(PrimitiveType.Cylinder, "barrel", env, p, new Vector3(0.8f, 0.6f, 0.8f), barrel);
                Util.Prim(PrimitiveType.Cylinder, "barrelRing", env, p + Vector3.up * 0.25f, new Vector3(0.84f, 0.04f, 0.84f), blackMat);
            }

            // Floor grime
            var rnd = new System.Random(levelIndex * 31 + 7);
            var grime = Util.Mat(new Color(0.05f, 0.05f, 0.06f), 0.8f);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3((float)(rnd.NextDouble() * 8 - 4), 0.004f, (float)(rnd.NextDouble() * 11 - 6));
                Util.Prim(PrimitiveType.Cylinder, "grime", env, p, new Vector3(1.1f, 0.002f, 0.7f), grime);
            }

            foreach (var d in L.robots.Select(r => new Vector2(r.x, r.z)).Distinct())
                BuildDoor(env, d, blackMat);
        }

        void Wall(Transform env, Vector3 pos, Vector3 size, Material m)
        {
            var g = Util.Cube("wall", env, pos, size, m);
            g.AddComponent<BoxCollider>().size = Vector3.one;
        }

        void BuildDoor(Transform env, Vector2 spawn, Material dark)
        {
            float dN = Mathf.Abs(spawn.y - MaxZ), dS = Mathf.Abs(spawn.y - MinZ), dW = Mathf.Abs(spawn.x - MinX), dE = Mathf.Abs(spawn.x - MaxX);
            float m = Mathf.Min(dN, dS, dW, dE);
            var glow = Util.Glow(theme.accent, 3f);
            if (m == dN)
            {
                Util.Cube("door", env, new Vector3(spawn.x, 0.9f, MaxZ + 0.02f), new Vector3(1.5f, 1.8f, 0.1f), dark);
                Util.Cube("doorLight", env, new Vector3(spawn.x, 1.9f, MaxZ - 0.02f), new Vector3(1.5f, 0.1f, 0.06f), glow);
            }
            else if (m == dS)
            {
                Util.Cube("door", env, new Vector3(spawn.x, 0.36f, MinZ - 0.02f), new Vector3(1.5f, 0.74f, 0.76f), dark);
                Util.Cube("doorLight", env, new Vector3(spawn.x, 0.74f, MinZ), new Vector3(1.5f, 0.06f, 0.06f), glow);
            }
            else
            {
                float x = m == dW ? MinX - 0.02f : MaxX + 0.02f;
                Util.Cube("door", env, new Vector3(x, 0.6f, spawn.y), new Vector3(0.76f, 1.24f, 1.5f), dark);
                Util.Cube("doorLight", env, new Vector3(x, 1.25f, spawn.y), new Vector3(0.78f, 0.06f, 1.5f), glow);
            }
        }

        // ───────────────────────── Input & shooting ─────────────────────────

        void Update()
        {
            FitCamera();
            if (State != GameState.Playing) { if (player != null) player.Move(Vector2.zero); return; }

            if (GameInput.PressedThisFrame(out var sp))
            {
                var guiPt = new Vector2(sp.x, Screen.height - sp.y);
                if (!restartRect.Contains(guiPt))
                {
                    var railCrate = ButtonAt(sp);
                    var crate = railCrate == null ? CrateAt(sp) : null;
                    if (railCrate != null) ShootButton(railCrate);
                    else if (crate != null) Shoot(crate);
                    else { joyActive = true; joyStart = joyNow = sp; }
                }
            }

            Vector2 input = GameInput.Keyboard2D();
            if (GameInput.Held(out var hp))
            {
                if (joyActive)
                {
                    joyNow = hp;
                    Vector2 d = joyNow - joyStart;
                    if (d.magnitude > JoyMax) joyStart += d.normalized * (d.magnitude - JoyMax);   // floating stick follows the finger
                    Vector2 stick = (joyNow - joyStart) / JoyMax;
                    if (stick.magnitude > 0.12f) input = stick;
                }
            }
            else joyActive = false;
            player.Move(input);

            foreach (var r in robots)
                if (!r.Dead && Vector2.Distance(r.Pos, player.Pos) < r.Radius + Player.Radius + 0.1f)
                {
                    Lose("Robot seni yakaladı!");
                    break;
                }
        }

        /// <summary>Finds the hanging crate (or chain) under a screen point.</summary>
        Crate CrateAt(Vector2 sp)
        {
            Crate best = null;
            float bestD = float.MaxValue;
            float chainTol = Screen.height * 0.03f;
            foreach (var c in crates)
            {
                if (c.Released) continue;
                Vector3 cs = cam.WorldToScreenPoint(c.Center);
                if (cs.z < 0) continue;
                float rad = ((Vector2)cam.WorldToScreenPoint(c.Center + cam.transform.right * c.Size * 0.5f) - (Vector2)cs).magnitude;
                float d = Vector2.Distance(sp, cs);
                Vector2 a = cam.WorldToScreenPoint(c.ChainPoint), b = cam.WorldToScreenPoint(c.ChainPoint + Vector3.up * 4f);
                float dc = DistToSegment(sp, a, b);
                bool hit = d < rad * 1.4f + Screen.height * 0.01f || dc < chainTol;
                float score = Mathf.Min(d, dc);
                if (hit && score < bestD) { bestD = score; best = c; }
            }
            return best;
        }

        /// <summary>Finds a rail crate whose wall button is under a screen point.</summary>
        Crate ButtonAt(Vector2 sp)
        {
            float tol = Screen.height * 0.04f;
            foreach (var c in crates)
            {
                if (!c.HasRail || c.Released) continue;
                Vector3 bs = cam.WorldToScreenPoint(c.ButtonPos);
                if (bs.z > 0 && Vector2.Distance(sp, bs) < tol) return c;
            }
            return null;
        }

        void ShootButton(Crate c)
        {
            if (shots <= 0) { sfx.Empty(); return; }
            shots--;
            shotsUsed++;
            player.AimAt(c.ButtonPos);
            StartCoroutine(Tracer(player.GunTip, c.ButtonPos));
            c.ToggleRail();
            sfx.Shot();
            sfx.Clank();
            Sparks(c.ButtonPos, new Color(1f, 0.85f, 0.3f), 8);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        void Shoot(Crate crate)
        {
            if (shots <= 0) { sfx.Empty(); return; }
            shots--;
            shotsUsed++;
            Vector3 target = crate.ChainPoint;
            player.AimAt(target);
            StartCoroutine(Tracer(player.GunTip, target));
            crate.Release();
            sfx.Shot();
            sfx.Clank();
            Sparks(target, new Color(1f, 0.85f, 0.3f), 10);
            Shake(0.08f);
        }

        // ───────────────────────── Game flow ─────────────────────────

        /// <summary>Falling crate near the floor: crush everything inside its footprint.</summary>
        public void CrushCheck(Crate c)
        {
            float half = c.Size * 0.5f;
            foreach (var r in robots)
            {
                if (r.Dead) continue;
                var d = r.Pos - c.Ground;
                if (Mathf.Abs(d.x) < half + r.Radius && Mathf.Abs(d.y) < half + r.Radius) r.Hit(c, c.Ground);
            }
            foreach (var b in barrels)
            {
                if (b.Exploded) continue;
                var d = b.Ground - c.Ground;
                if (Mathf.Abs(d.x) < half + Barrel.Radius && Mathf.Abs(d.y) < half + Barrel.Radius) Explode(b);
            }
            if (State == GameState.Playing)
            {
                var pd = player.Pos - c.Ground;
                if (Mathf.Abs(pd.x) < half + Player.Radius * 0.5f && Mathf.Abs(pd.y) < half + Player.Radius * 0.5f)
                {
                    player.Squash();
                    Lose("Kasa senin üstüne düştü!");
                }
            }
        }

        /// <summary>Barrel explosion: kills robots nearby, snaps nearby chains, sets off other barrels.</summary>
        void Explode(Barrel b)
        {
            if (b.Exploded) return;
            b.MarkExploded();
            Vector2 at = b.Ground;
            var token = new object();
            foreach (var r in robots)
                if (!r.Dead && Vector2.Distance(r.Pos, at) < Barrel.BlastRadius + r.Radius) r.Hit(token, at);
            foreach (var c in crates)
                if (!c.Released && Vector2.Distance(c.Ground, at) < 1.6f) c.Release();
            if (State == GameState.Playing && Vector2.Distance(player.Pos, at) < Barrel.BlastRadius - 0.6f)
            {
                player.Squash();
                Lose("Patlamaya çok yakındın!");
            }
            StartCoroutine(Blast(Util.W(at, 0.6f)));
            sfx.Crush();
            sfx.Thud();
            Shake(0.55f);
            foreach (var other in barrels)
                if (!other.Exploded && Vector2.Distance(other.Ground, at) < Barrel.BlastRadius)
                    StartCoroutine(ExplodeLater(other, 0.18f));
        }

        IEnumerator ExplodeLater(Barrel b, float delay)
        {
            yield return new WaitForSeconds(delay);
            Explode(b);
        }

        IEnumerator Blast(Vector3 at)
        {
            var fire = Util.Glow(new Color(1f, 0.55f, 0.15f), 5f);
            var core = Util.Glow(new Color(1f, 0.9f, 0.5f), 8f);
            var ball = Util.Prim(PrimitiveType.Sphere, "blast", levelRoot, at, Vector3.one * 0.5f, fire);
            var inner = Util.Prim(PrimitiveType.Sphere, "blastCore", levelRoot, at, Vector3.one * 0.3f, core);
            var l = ball.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.2f);
            l.range = 10f;
            l.intensity = 8f;
            Debris(at, new Color(0.75f, 0.2f, 0.15f));
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                if (ball == null) yield break;
                float k = t / 0.35f;
                ball.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, Barrel.BlastRadius * 2f, Mathf.Sqrt(k));
                inner.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, Barrel.BlastRadius, k);
                l.intensity = 8f * (1f - k);
                yield return null;
            }
            if (ball != null) Destroy(ball);
            if (inner != null) Destroy(inner);
            if (L != null) OilStain(Util.G(at));
        }

        public void OnCrateLanded(Crate c)
        {
            Shake(Mathf.Clamp(0.12f + c.Size * 0.06f, 0.15f, 0.35f));
            sfx.Thud();
            Dust(Util.W(c.Ground, 0.1f), c.Size);
        }

        public void OnRobotKilled(Robot r)
        {
            kills++;
            sfx.Crush();
            Shake(0.4f);
            StartCoroutine(SlowMo(0.25f, 0.45f));
            if (kills >= L.robots.Length && State == GameState.Playing)
                StartCoroutine(WinAfter(1.0f));
        }

        IEnumerator WinAfter(float realSeconds)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            if (State != GameState.Playing) yield break;
            State = GameState.Won;
            stars = shotsUsed <= L.par ? 3 : shotsUsed <= L.par + 1 ? 2 : 1;
            sfx.Win();
            int best = PlayerPrefs.GetInt("cd3_stars_" + levelIndex, 0);
            if (stars > best) PlayerPrefs.SetInt("cd3_stars_" + levelIndex, stars);
            PlayerPrefs.SetInt("cd3_level", Mathf.Min(levelIndex + 1, pack.levels.Length - 1));
            PlayerPrefs.Save();
        }

        public void Lose(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            loseReason = reason;
            joyActive = false;
            sfx.Lose();
            Shake(0.45f);
            StartCoroutine(SlowMo(0.3f, 0.5f));
        }

        IEnumerator SlowMo(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = defaultFixedDt * scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDt;
        }

        // ───────────────────────── Juice ─────────────────────────

        public void Shake(float amount) => shakeAmt = Mathf.Max(shakeAmt, amount);

        void LateUpdate()
        {
            if (cam == null) return;
            cam.transform.position = camBase + Random.insideUnitSphere * shakeAmt;
            shakeAmt = Mathf.MoveTowards(shakeAmt, 0f, Time.unscaledDeltaTime * 1.6f);
        }

        IEnumerator Tracer(Vector3 a, Vector3 b)
        {
            var glow = Util.Glow(new Color(1f, 0.85f, 0.4f), 6f);
            var line = Util.Cube("tracer", levelRoot, (a + b) / 2f, new Vector3(0.05f, 0.05f, Vector3.Distance(a, b)), glow);
            line.transform.rotation = Quaternion.LookRotation(b - a);
            var flash = Util.Prim(PrimitiveType.Sphere, "flash", levelRoot, a, Vector3.one * 0.35f, glow);
            var fl = flash.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = new Color(1f, 0.8f, 0.4f);
            fl.range = 4f;
            fl.intensity = 5f;
            yield return new WaitForSecondsRealtime(0.07f);
            if (line != null) Destroy(line);
            if (flash != null) Destroy(flash);
        }

        void Sparks(Vector3 at, Color c, int count)
        {
            var m = Util.Glow(c, 3f);
            for (int i = 0; i < count; i++)
            {
                var g = Util.Cube("spark", levelRoot, at, Vector3.one * 0.07f, m);
                var rb = g.AddComponent<Rigidbody>();
                rb.SetVel(Random.onUnitSphere * Random.Range(2f, 6f));
                Destroy(g, 0.4f);
            }
        }

        void Dust(Vector3 at, float size)
        {
            var m = Util.Mat(new Color(0.6f, 0.6f, 0.6f), 0.1f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var g = Util.Cube("dust", levelRoot, at + dir * size * 0.55f, Vector3.one * 0.18f, m);
                var rb = g.AddComponent<Rigidbody>();
                rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = 4f;
#else
                rb.drag = 4f;
#endif
                rb.SetVel(dir * Random.Range(3f, 5f) + Vector3.up * 0.5f);
                Destroy(g, 0.45f);
            }
        }

        public void Debris(Vector3 at, Color c)
        {
            var m = Util.Mat(c, 0.4f, 0.5f);
            var dark = Util.Mat(c.Mul(0.4f), 0.4f, 0.5f);
            for (int i = 0; i < 12; i++)
            {
                var g = Util.Cube("debris", levelRoot, at + Random.insideUnitSphere * 0.3f, Vector3.one * Random.Range(0.1f, 0.22f), i % 2 == 0 ? m : dark);
                g.AddComponent<BoxCollider>();
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.SetVel(new Vector3(Random.Range(-4f, 4f), Random.Range(4f, 8f), Random.Range(-4f, 4f)));
                rb.angularVelocity = Random.insideUnitSphere * 15f;
                Destroy(g, 1.6f);
            }
            Sparks(at, new Color(1f, 0.7f, 0.2f), 10);
        }

        public void OilStain(Vector2 at) =>
            Util.Prim(PrimitiveType.Cylinder, "oil", levelRoot, Util.W(at, 0.012f), new Vector3(1.5f, 0.002f, 1.2f), Util.Mat(new Color(0.02f, 0.02f, 0.03f), 0.95f));

        // ───────────────────────── UI (IMGUI, prototype only) ─────────────────────────

        GUIStyle sTitle, sText, sSmall, sButton, sSmallButton;

        void EnsureStyles(float u)
        {
            if (sTitle == null)
            {
                sTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                sText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                sSmall = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                sButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                sSmallButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                sTitle.normal.textColor = sText.normal.textColor = sSmall.normal.textColor = Color.white;
            }
            sTitle.fontSize = Mathf.RoundToInt(8f * u);
            sText.fontSize = Mathf.RoundToInt(4.2f * u);
            sSmall.fontSize = Mathf.RoundToInt(4.5f * u);
            sButton.fontSize = Mathf.RoundToInt(5f * u);
            sSmallButton.fontSize = Mathf.RoundToInt(3.4f * u);
        }

        void OnGUI()
        {
            if (L == null) return;
            float u = Mathf.Min(Screen.width, Screen.height * 0.6f) / 100f;
            EnsureStyles(u);
            float W = Screen.width, H = Screen.height;

            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, W, 12f * u), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(3f * u, 1.5f * u, 50f * u, 9f * u), $"{theme.name}  ·  BÖLÜM {levelIndex + 1}", sSmall);

            float bw = 2.2f * u, bh = 5.5f * u;
            float ax = W - 24f * u - L.shots * (bw + 1.2f * u);
            for (int i = 0; i < L.shots; i++)
            {
                GUI.color = i < shots ? theme.accent : new Color(1, 1, 1, 0.2f);
                GUI.DrawTexture(new Rect(ax + i * (bw + 1.2f * u), 3.2f * u, bw, bh), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            restartRect = new Rect(W - 21f * u, 1.5f * u, 19f * u, 9f * u);
            if (GUI.Button(restartRect, "YENİDEN", sSmallButton) && State == GameState.Playing) LoadLevel(levelIndex);

            if (!string.IsNullOrEmpty(L.hint) && State == GameState.Playing)
            {
                float a = Time.time - levelStartTime < 6f ? 1f : 0.4f;
                GUI.color = new Color(0, 0, 0, 0.35f * a);
                GUI.DrawTexture(new Rect(0, 12f * u, W, 12f * u), Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(5f * u, 12f * u, W - 10f * u, 12f * u), L.hint, sText);
                GUI.color = Color.white;
            }

            if (joyActive && State == GameState.Playing)
            {
                float R = JoyMax;
                var c = new Vector2(joyStart.x, H - joyStart.y);
                var k = new Vector2(joyNow.x, H - joyNow.y);
                GUI.color = new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(c.x - R, c.y - R, 2 * R, 2 * R), Util.CircleTex);
                GUI.color = new Color(1, 1, 1, 0.45f);
                GUI.DrawTexture(new Rect(k.x - R * 0.42f, k.y - R * 0.42f, R * 0.84f, R * 0.84f), Util.CircleTex);
                GUI.color = Color.white;
            }

            if (State == GameState.Won) DrawWin(u, W, H);
            if (State == GameState.Lost) DrawLose(u, W, H);

            if (Application.isEditor || Debug.isDebugBuild)
            {
                if (GUI.Button(new Rect(2 * u, H - 9 * u, 9 * u, 7 * u), "<")) LoadLevel(levelIndex - 1);
                if (GUI.Button(new Rect(12 * u, H - 9 * u, 9 * u, 7 * u), ">")) LoadLevel(levelIndex + 1);
            }
        }

        void Panel(float u, float W, float H, out Rect box)
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            box = new Rect(W / 2f - 40f * u, H / 2f - 35f * u, 80f * u, 70f * u);
            GUI.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawWin(float u, float W, float H)
        {
            Panel(u, W, H, out var box);
            bool last = levelIndex >= pack.levels.Length - 1;
            GUI.Label(new Rect(box.x, box.y + 3f * u, box.width, 12f * u), last ? "DÜNYA BİTTİ!" : "TEMİZ İŞ!", sTitle);
            float s = 13f * u, gap = 3f * u, sx = box.center.x - (3 * s + 2 * gap) / 2f;
            for (int i = 0; i < 3; i++)
            {
                GUI.color = i < stars ? theme.accent : new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(sx + i * (s + gap), box.y + 18f * u, s, s), Util.CircleTex);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x, box.y + 34f * u, box.width, 8f * u), $"Kullanılan mermi: {shotsUsed}  (hedef {L.par})", sText);
            if (GUI.Button(new Rect(box.x + 8f * u, box.y + 46f * u, box.width - 16f * u, 14f * u), last ? "BAŞA DÖN" : "SONRAKİ", sButton))
                LoadLevel(last ? 0 : levelIndex + 1);
        }

        void DrawLose(float u, float W, float H)
        {
            Panel(u, W, H, out var box);
            GUI.Label(new Rect(box.x, box.y + 5f * u, box.width, 12f * u), "OLMADI!", sTitle);
            GUI.Label(new Rect(box.x + 4f * u, box.y + 20f * u, box.width - 8f * u, 16f * u), loseReason, sText);
            if (GUI.Button(new Rect(box.x + 8f * u, box.y + 46f * u, box.width - 16f * u, 14f * u), "TEKRAR", sButton))
                LoadLevel(levelIndex);
        }
    }
}
