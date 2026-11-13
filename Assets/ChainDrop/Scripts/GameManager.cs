using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChainDrop
{
    public enum GameState { Loading, Playing, Won, Lost }

    /// <summary>
    /// Boots itself in any scene (no setup needed: open the project, press Play).
    /// Top-down 3/4 view arena. Levels come from Resources/ChainDrop/levels.json.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public static bool AutoBoot = true;

        /// <summary>How far up the screen one unit of height is drawn (fake 3D, 3/4 view).</summary>
        public const float Oblique = 0.55f;
        public const float MinX = -5f, MaxX = 5f, MinY = -7f, MaxY = 6f;

        public GameState State { get; private set; } = GameState.Loading;
        public Player player;
        public Sfx sfx;

        Camera cam;
        LevelPack pack;
        int levelIndex;
        LevelDef L;
        Theme theme;
        Transform levelRoot;
        readonly List<Robot> robots = new List<Robot>();
        readonly List<Crate> crates = new List<Crate>();
        int kills, shots, shotsUsed, stars;
        string loseReason = "";
        float shakeAmt, levelStartTime, defaultFixedDt, camTopY;
        Vector3 camBase;
        Rect restartRect;

        // Floating joystick
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
            Physics2D.gravity = Vector2.zero;

            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
                cg.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            sfx = gameObject.AddComponent<Sfx>();

            var ta = Resources.Load<TextAsset>("ChainDrop/levels");
            pack = JsonUtility.FromJson<LevelPack>(ta.text);
            levelIndex = Mathf.Clamp(PlayerPrefs.GetInt("cd_level", 0), 0, pack.levels.Length - 1);
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
            kills = 0; shotsUsed = 0; stars = 0; shakeAmt = 0; joyActive = false;

            levelIndex = Mathf.Clamp(index, 0, pack.levels.Length - 1);
            L = pack.levels[levelIndex];
            theme = Theme.Get(L.theme);
            shots = L.shots;

            levelRoot = new GameObject("Level_" + (levelIndex + 1)).transform;
            FitCamera();
            BuildArena();
            foreach (var c in L.crates) crates.Add(Crate.Build(this, c, levelRoot, theme, camTopY + 2f));
            player = Player.Spawn(this, new Vector2(L.px, L.py), levelRoot, theme);

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
                Burst(new Vector2(d.x, d.y + 0.4f), theme.accent, 6, 0.12f, 3f, 0.4f, false);
            }
        }

        void FitCamera()
        {
            if (cam == null) return;
            const float contentW = 11.8f, contentBottom = -8.0f, contentTop = 9.2f;
            const float hud = 0.12f;  // top part of the screen used by the HUD
            float widthBased = (contentW / 2f) / Mathf.Max(0.1f, cam.aspect);
            float heightBased = (contentTop - contentBottom) / (1f - hud) / 2f;
            float halfH = Mathf.Max(widthBased, heightBased);
            cam.orthographicSize = halfH;
            float playMid = (contentTop + contentBottom) / 2f;
            float camY = playMid + halfH * hud;   // shift down a little so the HUD does not cover the arena
            camBase = new Vector3(0, camY, -10f);
            camTopY = camY + halfH;
        }

        void BuildArena()
        {
            cam.backgroundColor = theme.bg;
            var env = new GameObject("Arena").transform;
            env.SetParent(levelRoot, false);
            Color f1 = theme.ground, f2 = theme.ground.Mul(1.08f);

            // Background wall texture behind the arena (far wall)
            Util.Box("farWall", env, new Vector2(0, MaxY + 6f), new Vector2(30f, 12f), theme.bgFar, -300);
            for (int i = -3; i <= 3; i++)
            {
                Util.Box("pillar", env, new Vector2(i * 3.3f, MaxY + 6f), new Vector2(0.6f, 12f), theme.bg.Mul(0.85f), -299);
                Util.Blob("lampGlow", env, new Vector2(i * 3.3f + 1.65f, MaxY + 2.6f), new Vector2(3f, 2f), theme.accent.A(0.12f), -298);
                Util.Dot("lamp", env, new Vector2(i * 3.3f + 1.65f, MaxY + 2.6f), 0.22f, theme.accent, -297);
            }
            Util.Box("pipe1", env, new Vector2(0, MaxY + 1.7f), new Vector2(30f, 0.18f), theme.metalDark, -296);
            Util.Box("pipe2", env, new Vector2(0, MaxY + 1.35f), new Vector2(30f, 0.1f), theme.metalDark.Mul(0.8f), -296);

            // Checkered floor
            for (int x = (int)MinX; x < (int)MaxX; x++)
                for (int y = (int)MinY; y < (int)MaxY; y++)
                    Util.Box("tile", env, new Vector2(x + 0.5f, y + 0.5f), new Vector2(1.01f, 1.01f), ((x + y) & 1) == 0 ? f1 : f2, -200);

            // Floor light pools and markings
            for (int i = 0; i < 4; i++)
                Util.Blob("pool", env, new Vector2(i % 2 == 0 ? -2.5f : 2.5f, i < 2 ? 2.5f : -3.5f), new Vector2(5f, 4f), theme.accent.A(0.06f), -190);
            for (float x = MinX + 0.2f; x < MaxX; x += 0.8f)
            {
                Util.Box("hzT", env, new Vector2(x + 0.2f, MaxY - 0.18f), new Vector2(0.4f, 0.18f), theme.accent.A(0.85f), -180);
                Util.Box("hzB", env, new Vector2(x + 0.2f, MinY + 0.18f), new Vector2(0.4f, 0.18f), theme.accent.A(0.85f), -180);
            }
            Util.Box("lineL", env, new Vector2(MinX + 0.15f, (MinY + MaxY) / 2f), new Vector2(0.08f, MaxY - MinY), theme.accent.A(0.5f), -180);
            Util.Box("lineR", env, new Vector2(MaxX - 0.15f, (MinY + MaxY) / 2f), new Vector2(0.08f, MaxY - MinY), theme.accent.A(0.5f), -180);
            var rnd = new System.Random(levelIndex * 31 + 7);
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector2((float)(rnd.NextDouble() * 8 - 4), (float)(rnd.NextDouble() * 11 - 6));
                Util.Blob("stain", env, p, new Vector2(1.2f, 0.7f), new Color(0, 0, 0, 0.18f), -170);
            }

            // Walls (3/4 view): far wall shows its front face, side walls show their top
            Color wallTop = theme.metalDark, wallFront = theme.metalDark.Mul(0.65f);
            Util.Box("wallTopFront", env, new Vector2(0, MaxY + 0.35f), new Vector2(MaxX - MinX + 1.4f, 0.7f), wallFront, -150);
            Util.Box("wallTopLip", env, new Vector2(0, MaxY + 0.78f), new Vector2(MaxX - MinX + 1.4f, 0.16f), wallTop, -149);
            Util.Box("wallL", env, new Vector2(MinX - 0.35f, (MinY + MaxY) / 2f + 0.2f), new Vector2(0.7f, MaxY - MinY + 1.4f), wallTop, -148);
            Util.Box("wallR", env, new Vector2(MaxX + 0.35f, (MinY + MaxY) / 2f + 0.2f), new Vector2(0.7f, MaxY - MinY + 1.4f), wallTop, -148);
            Util.Box("wallBottom", env, new Vector2(0, MinY - 0.35f), new Vector2(MaxX - MinX + 1.4f, 0.7f), wallTop, 9000);
            Util.Box("wallBottomEdge", env, new Vector2(0, MinY - 0.02f), new Vector2(MaxX - MinX + 1.4f, 0.06f), wallTop.Mul(1.3f), 9001);

            // Doors where robots come from
            foreach (var d in L.robots.Select(r => new Vector2(r.x, r.y)).Distinct())
                BuildDoor(env, d);

            // Colliders
            AddWall(env, new Vector2(0, MaxY + 0.5f), new Vector2(20f, 1f));
            AddWall(env, new Vector2(0, MinY - 0.5f), new Vector2(20f, 1f));
            AddWall(env, new Vector2(MinX - 0.5f, 0), new Vector2(1f, 20f));
            AddWall(env, new Vector2(MaxX + 0.5f, 0), new Vector2(1f, 20f));
        }

        void BuildDoor(Transform env, Vector2 spawn)
        {
            float dTop = Mathf.Abs(spawn.y - MaxY), dBot = Mathf.Abs(spawn.y - MinY);
            float dL = Mathf.Abs(spawn.x - MinX), dR = Mathf.Abs(spawn.x - MaxX);
            float m = Mathf.Min(dTop, dBot, dL, dR);
            Color dark = new Color(0.04f, 0.04f, 0.06f);
            if (m == dTop)
            {
                Util.Box("door", env, new Vector2(spawn.x, MaxY + 0.3f), new Vector2(1.3f, 0.62f), dark, -147);
                Util.Box("doorLight", env, new Vector2(spawn.x, MaxY + 0.7f), new Vector2(1.3f, 0.08f), theme.accent, -146);
            }
            else if (m == dBot)
            {
                Util.Box("door", env, new Vector2(spawn.x, MinY - 0.35f), new Vector2(1.3f, 0.7f), dark, 9002);
                Util.Box("doorLight", env, new Vector2(spawn.x, MinY - 0.02f), new Vector2(1.3f, 0.08f), theme.accent, 9003);
            }
            else
            {
                float x = m == dL ? MinX - 0.35f : MaxX + 0.35f;
                Util.Box("door", env, new Vector2(x, spawn.y), new Vector2(0.7f, 1.3f), dark, -147);
                Util.Box("doorLight", env, new Vector2(m == dL ? MinX - 0.02f : MaxX + 0.02f, spawn.y), new Vector2(0.08f, 1.3f), theme.accent, -146);
            }
        }

        static void AddWall(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("WallCollider");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>().size = size;
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
                    Vector2 wp = cam.ScreenToWorldPoint(sp);
                    var crate = CrateAt(wp);
                    if (crate != null) Shoot(crate);
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
                    d = joyNow - joyStart;
                    Vector2 stick = d / JoyMax;
                    if (stick.magnitude > 0.12f) input = stick;
                }
            }
            else joyActive = false;
            player.Move(input);

            // Robots that reach the player catch it
            foreach (var r in robots)
                if (!r.Dead && Vector2.Distance(r.Pos, player.Pos) < r.Radius + Player.Radius + 0.06f)
                {
                    Lose("Robot seni yakaladı!");
                    break;
                }
        }

        Crate CrateAt(Vector2 wp)
        {
            Crate best = null;
            float bestD = float.MaxValue;
            foreach (var c in crates)
            {
                if (!c.HitTest(wp)) continue;
                float d = Vector2.Distance(wp, c.Visual);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void Shoot(Crate crate)
        {
            if (shots <= 0) { sfx.Empty(); return; }
            shots--;
            shotsUsed++;
            player.AimAt(crate.ChainBottom);
            Vector2 target = crate.ChainBottom + new Vector2(0, 0.25f);
            StartCoroutine(Tracer(player.GunTip, target));
            crate.Release();
            sfx.Shot();
            sfx.Clank();
            Burst(target, new Color(1f, 0.85f, 0.3f), 8, 0.09f, 5f, 0.35f, false);
            Shake(0.07f);
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
                if (Mathf.Abs(d.x) < half + r.Radius * 0.6f && Mathf.Abs(d.y) < half * 0.85f + r.Radius * 0.6f) r.Hit(c);
            }
            if (State == GameState.Playing)
            {
                var pd = player.Pos - c.Ground;
                if (Mathf.Abs(pd.x) < half + Player.Radius * 0.5f && Mathf.Abs(pd.y) < half * 0.85f + Player.Radius * 0.5f)
                {
                    player.Squash();
                    Lose("Kasa senin üstüne düştü!");
                }
            }
        }

        public void OnCrateLanded(Crate c)
        {
            Shake(Mathf.Clamp(0.12f + c.Size * 0.06f, 0.15f, 0.35f));
            sfx.Thud();
            StartCoroutine(DustRing(c.Ground, c.Size));
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
            int best = PlayerPrefs.GetInt("cd_stars_" + levelIndex, 0);
            if (stars > best) PlayerPrefs.SetInt("cd_stars_" + levelIndex, stars);
            PlayerPrefs.SetInt("cd_level", Mathf.Min(levelIndex + 1, pack.levels.Length - 1));
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
            cam.transform.position = camBase + (Vector3)(Random.insideUnitCircle * shakeAmt);
            shakeAmt = Mathf.MoveTowards(shakeAmt, 0f, Time.unscaledDeltaTime * 1.6f);
        }

        IEnumerator Tracer(Vector2 a, Vector2 b)
        {
            var dir = b - a;
            var sr = Util.Box("tracer", levelRoot, (a + b) / 2f, new Vector2(dir.magnitude, 0.07f), new Color(1f, 0.92f, 0.5f), 7000);
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var flash = Util.Blob("flash", levelRoot, a, new Vector2(0.9f, 0.9f), new Color(1f, 0.85f, 0.3f), 7001);
            for (float t = 0; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                if (sr == null) yield break;
                float k = 1f - t / 0.12f;
                sr.color = new Color(1f, 0.92f, 0.5f, k);
                flash.color = new Color(1f, 0.85f, 0.3f, k);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
            if (flash != null) Destroy(flash.gameObject);
        }

        IEnumerator DustRing(Vector2 at, float size)
        {
            var ring = Util.Blob("dust", levelRoot, at, Vector2.one * size, new Color(0.85f, 0.85f, 0.85f, 0.5f), 4000);
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                if (ring == null) yield break;
                float k = t / 0.45f;
                ring.transform.localScale = new Vector3(size * (1.2f + k * 1.6f), size * (0.9f + k * 1.1f), 1);
                ring.color = new Color(0.85f, 0.85f, 0.85f, 0.5f * (1 - k));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        void Burst(Vector2 pos, Color c, int count, float size, float speed, float life, bool dots)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = dots ? Util.Dot("p", levelRoot, pos, size, c, 6500) : Util.Box("p", levelRoot, pos, new Vector2(size, size), c, 6500);
                var rb = sr.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.linearDampingCompat(3f);
                rb.SetVel(Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed));
                rb.angularVelocity = Random.Range(-720f, 720f);
                Destroy(sr.gameObject, life);
            }
        }

        public void Debris(Vector2 pos, Color c) => Burst(pos, c, 14, 0.18f, 7f, 0.9f, false);

        public void OilStain(Vector2 pos) =>
            Util.Blob("oil", levelRoot, pos, new Vector2(1.6f, 0.9f), new Color(0.03f, 0.03f, 0.05f, 0.55f), -160);

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

            // Top bar
            GUI.color = new Color(0, 0, 0, 0.4f);
            GUI.DrawTexture(new Rect(0, 0, W, 12f * u), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(3f * u, 1.5f * u, 45f * u, 9f * u), $"{theme.name}  {levelIndex / 10 + 1}-{levelIndex % 10 + 1}", sSmall);

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

            // Floating joystick
            if (joyActive && State == GameState.Playing)
            {
                float R = JoyMax;
                var c = new Vector2(joyStart.x, H - joyStart.y);
                var k = new Vector2(joyNow.x, H - joyNow.y);
                GUI.color = new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(c.x - R, c.y - R, 2 * R, 2 * R), Util.Circle.texture);
                GUI.color = new Color(1, 1, 1, 0.45f);
                GUI.DrawTexture(new Rect(k.x - R * 0.42f, k.y - R * 0.42f, R * 0.84f, R * 0.84f), Util.Circle.texture);
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
                GUI.DrawTexture(new Rect(sx + i * (s + gap), box.y + 18f * u, s, s), Util.Circle.texture);
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

    static class RbCompat
    {
        public static void linearDampingCompat(this Rigidbody2D rb, float v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = v;
#else
            rb.drag = v;
#endif
        }
    }
}
