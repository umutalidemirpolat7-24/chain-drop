using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChainDrop
{
    public enum GameState { Loading, Playing, Won, Lost }

    /// <summary>
    /// Boots itself in any scene (no setup needed: open the project, press Play).
    /// Loads levels from Resources/ChainDrop/levels.json and builds everything from code.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public static bool AutoBoot = true;

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
        int kills, shots, shotsUsed, stars;
        string loseReason = "";
        float shakeAmt, levelStartTime, defaultFixedDt;
        Vector3 camBase;
        bool dragging;
        Rect restartRect;

        public const float CeilingY = 11f;
        const float ViewWidth = 11.6f;
        const float DragZone = 0.2f;     // bottom 20% of the screen = drag area (under the floor)
        const float TapRadius = 0.55f;   // how close a tap must be to a chain link

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
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 6;
            defaultFixedDt = 0.02f;
            Time.fixedDeltaTime = defaultFixedDt;

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
            kills = 0; shotsUsed = 0; stars = 0; dragging = false; shakeAmt = 0;

            levelIndex = Mathf.Clamp(index, 0, pack.levels.Length - 1);
            L = pack.levels[levelIndex];
            theme = Theme.Get(L.theme);
            shots = L.shots;

            levelRoot = new GameObject("Level_" + (levelIndex + 1)).transform;
            FitCamera();
            BuildEnvironment();
            foreach (var c in L.crates) HangingCrate.Build(this, c, CeilingY, levelRoot, theme);
            player = Player.Spawn(this, L.playerX, levelRoot, theme);

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
            }
        }

        void BuildEnvironment()
        {
            cam.backgroundColor = theme.bg;
            var env = new GameObject("Environment").transform;
            env.SetParent(levelRoot, false);

            // Background: pillars, girders, lamps
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 3.2f;
                Util.Box("pillar", env, new Vector2(x, 6f), new Vector2(0.7f, 14f), theme.bgFar, -20);
                Util.Box("pillarEdge", env, new Vector2(x + 0.3f, 6f), new Vector2(0.1f, 14f), theme.bgFar * 0.8f + Color.black * 0.2f, -19);
            }
            Util.Box("girder", env, new Vector2(0, 8.2f), new Vector2(30f, 0.35f), theme.bgFar, -18);
            Util.Box("girder2", env, new Vector2(0, 4.6f), new Vector2(30f, 0.2f), theme.bgFar, -18);
            for (int i = -2; i <= 2; i++)
                Util.Dot("lamp", env, new Vector2(i * 3.2f + 1.6f, 9.4f), 0.22f, theme.accent * 0.9f, -17);

            // Ceiling
            Util.Box("ceiling", env, new Vector2(0, CeilingY + 2f), new Vector2(40f, 4f), theme.metalDark * 0.7f + Color.black * 0.3f, 0);
            Util.Box("ceilingRail", env, new Vector2(0, CeilingY + 0.1f), new Vector2(40f, 0.25f), theme.metalDark, 1);

            // Floor with collider
            var ground = new GameObject("Ground");
            ground.transform.SetParent(env, false);
            ground.transform.position = new Vector3(0, -4f, 0);
            var gc = ground.AddComponent<BoxCollider2D>();
            gc.size = new Vector2(40f, 8f);
            Util.Box("dirt", ground.transform, Vector2.zero, new Vector2(40f, 8f), theme.ground, 2);
            Util.Box("top", ground.transform, new Vector2(0, 3.92f), new Vector2(40f, 0.16f), theme.groundTop, 3);
            for (int i = -10; i <= 10; i++)
                Util.Box("hazard", ground.transform, new Vector2(i * 0.8f, 3.55f), new Vector2(0.4f, 0.28f), theme.accent, 3);
        }

        void FitCamera()
        {
            if (cam == null) return;
            float widthBased = (ViewWidth / 2f) / Mathf.Max(0.1f, cam.aspect);
            float heightBased = (CeilingY + 1.2f) / (1f - DragZone) / 2f;   // keep the ceiling on screen in landscape too
            float halfH = Mathf.Max(widthBased, heightBased);
            cam.orthographicSize = halfH;
            camBase = new Vector3(0, halfH * (1f - 2f * DragZone), -10f);    // floor sits at the DragZone line
        }

        // ───────────────────────── Input & shooting ─────────────────────────

        void Update()
        {
            FitCamera();
            if (State != GameState.Playing) return;

            if (GameInput.PressedThisFrame(out var sp))
            {
                var guiPt = new Vector2(sp.x, Screen.height - sp.y);
                if (!restartRect.Contains(guiPt))
                {
                    if (sp.y < Screen.height * DragZone) dragging = true;
                    else Shoot(cam.ScreenToWorldPoint(sp));
                }
            }
            if (GameInput.Held(out var hp))
            {
                if (dragging) player.SetTarget(cam.ScreenToWorldPoint(hp).x);
            }
            else dragging = false;
        }

        void Shoot(Vector2 wp)
        {
            if (shots <= 0) { sfx.Empty(); return; }
            shots--;
            shotsUsed++;
            sfx.Shot();
            player.Face(wp.x - player.X);

            ChainLink best = null;
            float bestD = float.MaxValue;
            foreach (var h in Physics2D.OverlapCircleAll(wp, TapRadius))
            {
                var link = h.GetComponent<ChainLink>();
                if (link == null || link.Broken) continue;
                float d = Vector2.Distance(wp, h.transform.position);
                if (d < bestD) { bestD = d; best = link; }
            }

            Vector2 end = best != null ? (Vector2)best.transform.position : wp;
            StartCoroutine(Tracer(player.GunTip, end));
            if (best != null)
            {
                best.Break();
                sfx.Clank();
                Sparks(end);
                Shake(0.06f);
            }
        }

        // ───────────────────────── Game flow ─────────────────────────

        public void OnRobotKilled(Robot r)
        {
            kills++;
            sfx.Crush();
            Shake(0.35f);
            StartCoroutine(SlowMo(0.25f, 0.45f));
            if (kills >= L.robots.Length && State == GameState.Playing)
                StartCoroutine(WinAfter(0.9f));
        }

        IEnumerator WinAfter(float realSeconds)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            if (State != GameState.Playing) yield break;
            State = GameState.Won;
            stars = shotsUsed <= L.par ? 3 : shotsUsed <= L.par + 1 ? 2 : 1;
            sfx.Win();
            int key = PlayerPrefs.GetInt("cd_stars_" + levelIndex, 0);
            if (stars > key) PlayerPrefs.SetInt("cd_stars_" + levelIndex, stars);
            PlayerPrefs.SetInt("cd_level", Mathf.Min(levelIndex + 1, pack.levels.Length - 1));
            PlayerPrefs.Save();
        }

        public void Lose(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            loseReason = reason;
            sfx.Lose();
            Shake(0.4f);
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
            shakeAmt = Mathf.MoveTowards(shakeAmt, 0f, Time.unscaledDeltaTime * 1.5f);
        }

        IEnumerator Tracer(Vector2 a, Vector2 b)
        {
            var dir = b - a;
            var sr = Util.Box("tracer", levelRoot, (a + b) / 2f, new Vector2(dir.magnitude, 0.07f), new Color(1f, 0.9f, 0.4f, 1f), 50);
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var flash = Util.Dot("flash", levelRoot, a, 0.45f, new Color(1f, 0.85f, 0.3f, 1f), 51);
            for (float t = 0; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / 0.12f;
                if (sr == null) yield break;
                sr.color = new Color(1f, 0.9f, 0.4f, k);
                flash.color = new Color(1f, 0.85f, 0.3f, k);
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
            if (flash != null) Destroy(flash.gameObject);
        }

        void Burst(Vector2 pos, Color c, int count, float size, float speed, float gravity, float life, bool dots)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = dots ? Util.Dot("p", levelRoot, pos, size, c, 40) : Util.Box("p", levelRoot, pos, new Vector2(size, size), c, 40);
                var rb = sr.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = gravity;
                rb.SetVel(Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed) + Vector2.up * speed * 0.3f);
                rb.angularVelocity = Random.Range(-720f, 720f);
                Destroy(sr.gameObject, life);
            }
        }

        public void Debris(Vector2 pos, Color c) => Burst(pos + Vector2.up * 0.4f, c, 12, 0.2f, 7f, 2f, 1.5f, false);
        public void Dust(Vector2 pos) => Burst(pos, new Color(0.8f, 0.8f, 0.8f, 0.6f), 6, 0.35f, 2.5f, 0.2f, 0.5f, true);
        void Sparks(Vector2 pos) => Burst(pos, new Color(1f, 0.85f, 0.3f), 7, 0.08f, 5f, 1f, 0.35f, false);

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
            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, W, 12f * u), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(3f * u, 1.5f * u, 45f * u, 9f * u), $"{theme.name}  {levelIndex / 10 + 1}-{levelIndex % 10 + 1}", sSmall);

            // Ammo (bullets)
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

            // Hint
            if (!string.IsNullOrEmpty(L.hint) && State == GameState.Playing)
            {
                float a = Time.time - levelStartTime < 6f ? 1f : 0.45f;
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(6f * u, 13f * u, W - 12f * u, 14f * u), L.hint, sText);
                GUI.color = Color.white;
            }

            // Drag zone label
            if (State == GameState.Playing)
            {
                GUI.color = new Color(1, 1, 1, 0.35f);
                GUI.Label(new Rect(0, H * (1f - DragZone * 0.55f), W, 8f * u), "‹   SÜRÜKLE   ›", sText);
                GUI.color = Color.white;
            }

            if (State == GameState.Won) DrawWin(u, W, H);
            if (State == GameState.Lost) DrawLose(u, W, H);

            // Dev tools: skip levels (editor / development builds only)
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
}
