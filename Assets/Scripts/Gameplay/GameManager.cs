using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    public enum GState { Menu, Briefing, Playing, Paused, Results }

    public class Contract
    {
        public string name, blurb;
        public int quota;
        public float time, valueMult, fog, bonusSpeed;
        public int inspectors;
    }

    /// <summary>
    /// Owns the whole game loop: menu -> briefing -> shift -> results -> next shift.
    /// Builds the world at runtime and draws all UI with IMGUI (no UI packages needed).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public GState State = GState.Menu;
        public bool Playing { get { return State == GState.Playing; } }
        public Camera Cam;
        public Light Flashlight;
        public PlayerController Player;
        public LevelData Level;
        public Transform worldRoot;
        public Contract Current;
        public int Shift = 1, Bank = 0;
        public int Delivered, DeliveredCount, Fines, Citations;
        public float TimeLeft;
        public bool QuotaMet { get { return Current != null && Delivered >= Current.quota; } }

        // menu stage
        GameObject stage;
        Transform stageBurger;
        static readonly Vector3 StagePos = new Vector3(0, -300, 0);

        AudioSource humSrc, alarmSrc, chaseSrc;
        Contract[] contracts;
        bool oneMinSaid;
        float nextClang, nextPA, flash, paTimer;
        string paText = "";
        bool lastSuccess, lastFired, lastClockOut;
        int lastNet, lastBonus;
        string resultLine = "";

        class ToastMsg { public string text; public float t; }
        readonly List<ToastMsg> toasts = new List<ToastMsg>();

        static readonly string[] PALines =
        {
            "Attention valued associates: morale is mandatory.",
            "Reminder: the Health Inspector is not a rumor. He is a lifestyle.",
            "Employees found sleeping on shift will be reassigned to the fryer.",
            "MunchCorp thanks you for your sacrifice. Your sacrifice thanks no one.",
            "Please keep screaming to a minimum. The walls are listening.",
            "Today's special is you. Please remain on schedule.",
            "Your safety is important to us. Your quota is more important.",
            "Stay hydrated. Hydration is not provided.",
        };

        // =================== lifecycle ===================
        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;

            // neutralize any stray default camera/light in whatever scene we landed in
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);

            MakeCamera();
            MakeStage();
            humSrc = ProcAudio.Loop(gameObject, ProcAudio.Hum, 0.3f, false);
            alarmSrc = ProcAudio.Loop(gameObject, ProcAudio.Alarm, 0.12f, false); alarmSrc.Stop();
            chaseSrc = ProcAudio.Loop(gameObject, ProcAudio.Chase, 0f, false);
            ShowMenu();
        }

        void MakeCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            Cam.fieldOfView = 75f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 90f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.02f, 0.03f, 0.03f);

            var fl = new GameObject("Flashlight");
            fl.transform.SetParent(go.transform, false);
            Flashlight = fl.AddComponent<Light>();
            Flashlight.type = LightType.Spot;
            Flashlight.range = 24f;
            Flashlight.spotAngle = 58f;
            Flashlight.intensity = 2.6f;
            Flashlight.color = new Color(1f, 0.96f, 0.85f);
            Flashlight.shadows = LightShadows.None;
            Flashlight.enabled = false;
        }

        void MakeStage()
        {
            stage = new GameObject("MenuStage");
            stage.transform.position = StagePos;
            Mats.Part(stage.transform, PrimitiveType.Cube, "Floor", new Vector3(0, -0.1f, 0), new Vector3(40, 0.2f, 40),
                Mats.Get("floorD", Color.white, 0.4f, Mats.Tex("floorDT", 1, new Color(0.65f, 0.1f, 0.08f), new Color(0.85f, 0.8f, 0.68f))));
            var burger = CargoFactory.Visual(CargoKind.Burger);
            burger.transform.SetParent(stage.transform, false);
            burger.transform.localPosition = new Vector3(0, 0.1f, 0);
            burger.transform.localScale = Vector3.one * 6f;
            stageBurger = burger.transform;

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(stage.transform, false);
            spot.transform.localPosition = new Vector3(0, 9, -3);
            spot.transform.localRotation = Quaternion.Euler(80, 0, 0);
            spot.type = LightType.Spot; spot.range = 25; spot.spotAngle = 60; spot.intensity = 5f; spot.color = new Color(1f, 0.9f, 0.7f);
            spot.shadows = LightShadows.None;
            var back = new GameObject("Back").AddComponent<Light>();
            back.transform.SetParent(stage.transform, false);
            back.transform.localPosition = new Vector3(3, 3, 6);
            back.type = LightType.Point; back.range = 18; back.intensity = 2f; back.color = new Color(1f, 0.15f, 0.1f);
            back.shadows = LightShadows.None;
        }

        void PlaceCamOnStage()
        {
            Cam.transform.SetParent(null);
            Cam.transform.position = StagePos + new Vector3(0, 2.2f, -11f);
            Cam.transform.rotation = Quaternion.Euler(4, 0, 0);
            Cam.fieldOfView = 60f;
            Flashlight.enabled = false;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.04f, 0.02f, 0.02f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.09f, 0.09f);
            Cam.backgroundColor = new Color(0.04f, 0.02f, 0.02f);
            stage.SetActive(true);
        }

        // =================== state transitions ===================
        public void ShowMenu()
        {
            DestroyWorld();
            Time.timeScale = 1f;
            Shift = 1; Bank = 0;
            State = GState.Menu;
            PlaceCamOnStage();
            StopLoops();
        }

        public void ShowBriefing()
        {
            DestroyWorld();
            Time.timeScale = 1f;
            State = GState.Briefing;
            contracts = MakeContracts(Shift);
            PlaceCamOnStage();
            StopLoops();
        }

        void StopLoops()
        {
            if (alarmSrc != null) alarmSrc.Stop();
            if (chaseSrc != null) chaseSrc.volume = 0f;
        }

        Contract[] MakeContracts(int s)
        {
            float q = 1f + 0.3f * (s - 1);
            return new Contract[]
            {
                new Contract { name = "STANDARD SHIFT", blurb = "Normal hours. Normal danger. Normal lies.", quota = Round10(450 * q), time = 240f, valueMult = 1f, inspectors = 1, fog = 0.035f, bonusSpeed = 0f },
                new Contract { name = "RUSH HOUR", blurb = "Hungry customers. Cargo pays 40% more, but the clock is shorter and the Inspector is in a hurry.", quota = Round10(700 * q), time = 180f, valueMult = 1.4f, inspectors = 1, fog = 0.04f, bonusSpeed = 0.9f },
                new Contract { name = "GRAVEYARD SHIFT", blurb = "Cargo pays nearly double. TWO Inspectors. Thick fog. Nobody will hear you.", quota = Round10(1000 * q), time = 270f, valueMult = 1.8f, inspectors = 2, fog = 0.06f, bonusSpeed = 0.3f },
            };
        }

        static int Round10(float v) { return Mathf.RoundToInt(v / 10f) * 10; }

        void DestroyWorld()
        {
            if (Cam != null) Cam.transform.SetParent(null);
            if (worldRoot != null)
            {
                worldRoot.gameObject.SetActive(false); // fires OnDisable immediately so static lists stay clean
                Destroy(worldRoot.gameObject);
                worldRoot = null;
            }
            Player = null;
            Level = null;
        }

        public void BeginShift(Contract c)
        {
            DestroyWorld();
            Time.timeScale = 1f;
            stage.SetActive(false);
            Current = c;
            Delivered = 0; DeliveredCount = 0; Fines = 0; Citations = 0;
            TimeLeft = c.time;
            oneMinSaid = false;
            quotaAnnounced = false;
            toasts.Clear();
            flash = 0f;

            int seed = Random.Range(1, int.MaxValue);
            var rng = new System.Random(seed);
            worldRoot = new GameObject("ShiftWorld").transform;
            Level = LevelBuilder.Build(worldRoot, seed);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = c.fog;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.03f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.08f);
            Cam.backgroundColor = new Color(0.02f, 0.03f, 0.03f);
            Cam.fieldOfView = 75f;

            CargoFactory.Populate(Level, worldRoot, c.valueMult, rng, 24);
            LevelBuilder.PlaceGrease(Level, worldRoot, rng, 9);

            // player
            var pgo = new GameObject("Player");
            pgo.transform.SetParent(worldRoot, false);
            pgo.transform.position = Level.spawnPos;
            var cc = pgo.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.35f; cc.slopeLimit = 50f;
            Player = pgo.AddComponent<PlayerController>();
            Player.Setup(Cam, Level.spawnYaw);

            // inspectors
            float bonus = c.bonusSpeed + 0.25f * (Shift - 1);
            for (int i = 0; i < c.inspectors; i++)
            {
                var cell = Level.RandomCell(rng, 4);
                Inspector.Create(worldRoot, Level, Level.Center(cell.x, cell.y) + new Vector3(i * 1.5f, 0, 0), bonus);
            }

            State = GState.Playing;
            nextClang = Time.time + 8f;
            nextPA = Time.time + 50f;
            alarmSrc.Stop();
            chaseSrc.volume = 0f;
            Announce("Welcome to your shift. Quota: $" + c.quota + ". Please try to survive. Not required.");
        }

        public void TogglePause()
        {
            if (State == GState.Playing) { State = GState.Paused; Time.timeScale = 0f; }
            else if (State == GState.Paused) { State = GState.Playing; Time.timeScale = 1f; }
        }

        public void ClockOut()
        {
            if (!Playing || !QuotaMet) return;
            EndShift(false, true);
        }

        void EndShift(bool fired, bool clockOut)
        {
            if (State != GState.Playing) return;
            State = GState.Results;
            lastFired = fired;
            lastClockOut = clockOut;
            lastBonus = clockOut ? Mathf.RoundToInt(TimeLeft) * 2 : 0;
            lastSuccess = !fired && Delivered >= Current.quota;
            lastNet = Delivered - Fines + lastBonus;
            if (lastSuccess) Bank += Mathf.Max(lastNet, 0);
            alarmSrc.Stop();
            chaseSrc.volume = 0f;
            Flashlight.enabled = false;
            if (fired) resultLine = "FIRED. Three citations. The Inspector sends his regards.";
            else if (lastSuccess) resultLine = "MunchCorp thanks you for your sacrifice. Your paycheck is on its way. Deductions apply.";
            else resultLine = "Quota not met. Your remains will be billed for cleanup.";
            ProcAudio.Play2D(lastSuccess ? ProcAudio.Chime : ProcAudio.Buzzer, 0.8f);
        }

        // =================== gameplay hooks ===================
        public bool InBay(Vector3 p)
        {
            if (Level == null) return false;
            return Mathf.Abs(p.x - Level.bayCenter.x) < Level.bayHalf && Mathf.Abs(p.z - Level.bayCenter.z) < Level.bayHalf && p.y < 1.8f;
        }

        public void DeliverOne(Cargo c)
        {
            if (c == null) return;
            Delivered += c.value;
            DeliveredCount++;
            Toast("+$" + c.value + "  " + c.label);
            ProcAudio.PlayAt(ProcAudio.Cash, c.transform.position, 0.9f, false);
            Destroy(c.gameObject);
            CheckQuota();
        }

        public void DeliverMany(List<Cargo> list)
        {
            int sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                sum += list[i].value;
                DeliveredCount++;
                Destroy(list[i].gameObject);
            }
            Delivered += sum;
            Toast("+$" + sum + "  delivered (" + list.Count + " items)");
            ProcAudio.Play2D(ProcAudio.Cash, 0.9f);
            CheckQuota();
        }

        bool quotaAnnounced;
        void CheckQuota()
        {
            if (!quotaAnnounced && QuotaMet)
            {
                quotaAnnounced = true;
                Announce("Quota achieved. Management is mildly pleased. Clock out at the terminal, or push your luck.");
            }
        }

        public void Citation(Inspector who)
        {
            if (Player == null || Player.Invuln > 0f || !Playing) return;
            Citations++;
            Fines += 100;
            flash = 1f;
            Player.Confiscate();
            Player.Teleport(Level.spawnPos, Level.spawnYaw);
            Player.Invuln = 3.5f;
            var cell = Level.RandomCell(new System.Random(), 5);
            who.SendAway(Level.Center(cell.x, cell.y));
            ProcAudio.Play2D(ProcAudio.Buzzer, 1f);
            Toast("CITATION #" + Citations + "! -$100 and all carried cargo confiscated.");
            if (Citations >= 3) EndShift(true, false);
        }

        public void MakeNoise(Vector3 pos, float radius)
        {
            for (int i = 0; i < Inspector.All.Count; i++) Inspector.All[i].Hear(pos, radius);
        }

        public void Toast(string s)
        {
            toasts.Add(new ToastMsg { text = s, t = 4.5f });
            if (toasts.Count > 5) toasts.RemoveAt(0);
        }

        void Announce(string s)
        {
            paText = s;
            paTimer = 6f;
            ProcAudio.Play2D(ProcAudio.Chime, 0.5f);
        }

        // =================== per-frame ===================
        void Update()
        {
            if ((State == GState.Playing || State == GState.Paused) && Input.GetKeyDown(KeyCode.Escape)) TogglePause();

            Cursor.lockState = State == GState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = State != GState.Playing;

            if (State == GState.Menu || State == GState.Briefing)
            {
                if (stageBurger != null) stageBurger.Rotate(0, 25f * Time.deltaTime, 0);
            }

            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].t -= Time.unscaledDeltaTime;
                if (toasts[i].t <= 0f) toasts.RemoveAt(i);
            }
            if (paTimer > 0f) paTimer -= Time.unscaledDeltaTime;
            if (flash > 0f) flash -= Time.unscaledDeltaTime * 0.8f;

            if (State != GState.Playing) return;

            float dt = Time.deltaTime;
            TimeLeft -= dt;
            if (TimeLeft <= 0f) { TimeLeft = 0f; EndShift(false, false); return; }

            if (TimeLeft < 30f && !alarmSrc.isPlaying) alarmSrc.Play();
            if (TimeLeft < 60f && !oneMinSaid) { oneMinSaid = true; Announce("One minute remaining. Please do not die in the loading bay. It is bad for the brand."); }

            // ambient horror: distant metallic noises, corporate PA
            if (Time.time > nextClang && Player != null)
            {
                nextClang = Time.time + Random.Range(12f, 25f);
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(14f, 24f);
                ProcAudio.PlayAt(ProcAudio.Clang, Player.transform.position + new Vector3(r.x, 1f, r.y), 0.8f);
            }
            if (Time.time > nextPA)
            {
                nextPA = Time.time + Random.Range(55f, 85f);
                Announce(PALines[Random.Range(0, PALines.Length)]);
            }

            bool anyChase = false;
            for (int i = 0; i < Inspector.All.Count; i++) if (Inspector.All[i].IsChasing) anyChase = true;
            chaseSrc.volume = Mathf.MoveTowards(chaseSrc.volume, anyChase ? 0.35f : 0f, dt * 0.8f);
        }

        // =================== IMGUI ===================
        GUIStyle sTitle, sBig, sBody, sSmall, sBtn, sTimer;
        Texture2D tBtn, tBtnH;

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (sBody != null) return;
            tBtn = Solid(new Color(0.75f, 0.1f, 0.05f, 1f));
            tBtnH = Solid(new Color(1f, 0.72f, 0.05f, 1f));
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, wordWrap = false };
            sBig = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, wordWrap = true };
            sBody = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            sTimer = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold };
            sBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBtn.normal.background = tBtn; sBtn.normal.textColor = Color.white;
            sBtn.hover.background = tBtnH; sBtn.hover.textColor = Color.black;
            sBtn.active.background = tBtnH; sBtn.active.textColor = Color.black;
            sBtn.focused.background = tBtn; sBtn.focused.textColor = Color.white;
        }

        void Box(Rect r, Color c)
        {
            var o = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = o;
        }

        void Label(Rect r, string t, GUIStyle st, Color c, TextAnchor a = TextAnchor.UpperLeft)
        {
            st.alignment = a;
            st.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, st);
            st.normal.textColor = c;
            GUI.Label(r, t, st);
        }

        static string Fmt(float t)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(t));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        void OnGUI()
        {
            InitStyles();
            float s = Screen.height / 720f;
            float vw = Screen.width / s;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            switch (State)
            {
                case GState.Menu: DrawMenu(vw); break;
                case GState.Briefing: DrawBriefing(vw); break;
                case GState.Playing: DrawHUD(vw); break;
                case GState.Paused: DrawHUD(vw); DrawPause(vw); break;
                case GState.Results: DrawHUD(vw); DrawResults(vw); break;
            }
            GUI.matrix = old;
        }

        void DrawMenu(float vw)
        {
            Box(new Rect(0, 0, 640, 720), new Color(0, 0, 0, 0.65f));
            Label(new Rect(40, 50, 600, 80), "SHIFT MEAL", sTitle, new Color(1f, 0.8f, 0.1f));
            Label(new Rect(44, 125, 600, 50), "NIGHTMARE DELIVERY", sBig, new Color(0.9f, 0.15f, 0.1f));
            Label(new Rect(44, 175, 560, 40), "Fast food. Slow death.", sBody, new Color(0.8f, 0.8f, 0.8f));

            if (GUI.Button(new Rect(44, 260, 360, 64), "CLOCK IN", sBtn)) { ShowBriefing(); }
            if (GUI.Button(new Rect(44, 340, 360, 64), "QUIT", sBtn))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            Label(new Rect(44, 450, 570, 250),
                "WASD move   MOUSE look   SHIFT sprint   SPACE jump\n" +
                "E pick up / deliver   Q drop (Shift+Q all)   LMB throw\n" +
                "F flashlight   R clock out (at terminal)   ESC pause\n\n" +
                "Grab food. Don't make noise. Don't get cited.", sSmall, Color.white);
        }

        void DrawBriefing(float vw)
        {
            Box(new Rect(0, 0, vw, 720), new Color(0, 0, 0, 0.7f));
            Label(new Rect(0, 30, vw, 70), "SHIFT #" + Shift + " BRIEFING", sBig, new Color(1f, 0.8f, 0.1f), TextAnchor.UpperCenter);
            Label(new Rect(0, 80, vw, 40), "Career earnings: $" + Bank + "   |   Pick your contract. MunchCorp is not liable.", sBody, Color.white, TextAnchor.UpperCenter);

            float cw = 360f, gap = 24f;
            float x0 = (vw - (cw * 3 + gap * 2)) / 2f;
            for (int i = 0; i < contracts.Length; i++)
            {
                var c = contracts[i];
                float x = x0 + i * (cw + gap);
                Box(new Rect(x, 150, cw, 440), new Color(0.12f, 0.12f, 0.12f, 0.95f));
                Box(new Rect(x, 150, cw, 6), new Color(1f, 0.7f, 0.05f));
                Label(new Rect(x + 16, 168, cw - 32, 40), c.name, sBig, new Color(1f, 0.8f, 0.1f));
                Label(new Rect(x + 16, 250, cw - 32, 110), c.blurb, sSmall, new Color(0.85f, 0.85f, 0.85f));
                Label(new Rect(x + 16, 360, cw - 32, 170),
                    "Quota:  $" + c.quota + "\nTime:  " + Fmt(c.time) + "\nCargo value:  x" + c.valueMult.ToString("0.0") + "\nInspectors:  " + c.inspectors,
                    sBody, Color.white);
                if (GUI.Button(new Rect(x + 16, 528, cw - 32, 46), "ACCEPT", sBtn)) BeginShift(c);
            }
            if (GUI.Button(new Rect(40, 640, 200, 50), "BACK", sBtn)) ShowMenu();
        }

        void DrawHUD(float vw)
        {
            if (Current == null) return;
            var p = Player;

            // chase vignette
            bool chase = false;
            for (int i = 0; i < Inspector.All.Count; i++) if (Inspector.All[i].IsChasing) chase = true;
            if (chase && State == GState.Playing)
            {
                float a = 0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 10f);
                Color rc = new Color(0.8f, 0f, 0f, a);
                Box(new Rect(0, 0, vw, 60), rc); Box(new Rect(0, 660, vw, 60), rc);
                Box(new Rect(0, 0, 60, 720), rc); Box(new Rect(vw - 60, 0, 60, 720), rc);
                Label(new Rect(0, 130, vw, 40), "INSPECTOR IN PURSUIT - RUN", sBody, new Color(1f, 0.3f, 0.2f), TextAnchor.UpperCenter);
            }
            if (flash > 0f) Box(new Rect(0, 0, vw, 720), new Color(1f, 0f, 0f, flash * 0.5f));

            // crosshair
            Box(new Rect(vw / 2 - 2, 358, 4, 4), new Color(1, 1, 1, 0.85f));

            // timer + quota
            Color tc = (TimeLeft < 30f && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f) ? Color.red : Color.white;
            Label(new Rect(vw / 2 - 150, 8, 300, 56), Fmt(TimeLeft), sTimer, tc, TextAnchor.UpperCenter);
            float frac = Mathf.Clamp01(Delivered / (float)Current.quota);
            Box(new Rect(vw / 2 - 200, 66, 400, 22), new Color(0, 0, 0, 0.7f));
            Box(new Rect(vw / 2 - 198, 68, 396 * frac, 18), QuotaMet ? new Color(0.2f, 0.85f, 0.3f) : new Color(1f, 0.75f, 0.1f));
            Label(new Rect(vw / 2 - 200, 64, 400, 26), "$" + Delivered + " / $" + Current.quota, sSmall, Color.white, TextAnchor.UpperCenter);

            // top-left
            Label(new Rect(20, 14, 420, 30), "SHIFT " + Shift + " - " + Current.name, sSmall, Color.white);
            Label(new Rect(20, 38, 420, 30), "CITATIONS " + Citations + "/3   FINES -$" + Fines, sSmall, Citations > 0 ? new Color(1f, 0.4f, 0.3f) : Color.white);

            if (p != null)
            {
                // stamina
                Box(new Rect(20, 690, 200, 12), new Color(0, 0, 0, 0.6f));
                Box(new Rect(21, 691, 198 * p.Stamina, 10), p.Stamina < 0.25f ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.8f, 1f));

                // carried
                Label(new Rect(20, 560, 400, 28), "CARRYING " + p.TotalWeight.ToString("0.#") + " / " + PlayerController.Capacity + " kg", sSmall, Color.white);
                string list = "";
                for (int i = Mathf.Max(0, p.Carried.Count - 5); i < p.Carried.Count; i++) list += p.Carried[i].label + "  $" + p.Carried[i].value + "\n";
                if (p.Carried.Count > 5) list += "(+" + (p.Carried.Count - 5) + " more)\n";
                if (list.Length > 0) Label(new Rect(20, 584, 400, 100), list, sSmall, new Color(1f, 0.85f, 0.4f));

                if (!string.IsNullOrEmpty(p.Prompt) && State == GState.Playing)
                    Label(new Rect(vw / 2 - 300, 385, 600, 80), p.Prompt, sBody, Color.white, TextAnchor.UpperCenter);
            }

            // toasts
            float ty = 120f;
            for (int i = 0; i < toasts.Count; i++)
            {
                float a = Mathf.Clamp01(toasts[i].t);
                Label(new Rect(vw - 440, ty, 420, 60), toasts[i].text, sSmall, new Color(1f, 0.95f, 0.6f, a), TextAnchor.UpperRight);
                ty += 46f;
            }

            // corporate PA subtitle
            if (paTimer > 0f)
            {
                Box(new Rect(vw / 2 - 420, 600, 840, 64), new Color(0, 0, 0, 0.6f));
                Label(new Rect(vw / 2 - 410, 604, 820, 60), "[MUNCHCORP PA]  " + paText, sSmall, new Color(0.7f, 1f, 0.7f));
            }
        }

        void DrawPause(float vw)
        {
            Box(new Rect(0, 0, vw, 720), new Color(0, 0, 0, 0.7f));
            Label(new Rect(0, 150, vw, 60), "ON BREAK", sBig, new Color(1f, 0.8f, 0.1f), TextAnchor.UpperCenter);
            if (GUI.Button(new Rect(vw / 2 - 180, 250, 360, 60), "RESUME", sBtn)) TogglePause();
            if (GUI.Button(new Rect(vw / 2 - 180, 330, 360, 60), "ABANDON SHIFT", sBtn)) ShowBriefing();
            if (GUI.Button(new Rect(vw / 2 - 180, 410, 360, 60), "MAIN MENU", sBtn)) ShowMenu();
        }

        void DrawResults(float vw)
        {
            Box(new Rect(0, 0, vw, 720), new Color(0, 0, 0, 0.78f));
            float x = vw / 2 - 380;
            Label(new Rect(x, 70, 760, 60), lastSuccess ? "SHIFT COMPLETE" : (lastFired ? "YOU'RE FIRED" : "SHIFT FAILED"), sBig,
                lastSuccess ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.2f), TextAnchor.UpperCenter);
            string body =
                "Delivered:  $" + Delivered + "  (" + DeliveredCount + " items)\n" +
                "Fines:  -$" + Fines + "  (" + Citations + " citations)\n" +
                (lastClockOut ? "Clock-out bonus:  +$" + lastBonus + "\n" : "") +
                "Quota:  $" + Current.quota + "  -  " + (Delivered >= Current.quota ? "MET" : "NOT MET") + "\n" +
                "Paycheck:  " + (lastSuccess ? "$" + Mathf.Max(lastNet, 0) : "$0 (no pay for failure)") + "\n" +
                "Career earnings:  $" + Bank;
            Label(new Rect(x + 40, 150, 700, 260), body, sBody, Color.white);
            Label(new Rect(x + 40, 400, 700, 80), "\"" + resultLine + "\"", sSmall, new Color(0.7f, 1f, 0.7f));

            float by = 500f;
            if (lastSuccess)
            {
                if (GUI.Button(new Rect(x, by, 360, 60), "NEXT SHIFT", sBtn)) { Shift++; ShowBriefing(); }
            }
            else
            {
                if (GUI.Button(new Rect(x, by, 360, 60), "RETRY SHIFT", sBtn)) ShowBriefing();
            }
            if (GUI.Button(new Rect(x + 400, by, 360, 60), "MAIN MENU", sBtn)) ShowMenu();
        }
    }
}
