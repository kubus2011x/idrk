using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    /// <summary>Builds a randomized grid facility out of primitives. Every run differs.</summary>
    public static class LevelBuilder
    {
        public const int W = 7, H = 5;
        public const float CS = 8f, WH = 3.5f, GAP = 3f, DOORH = 2.6f;
        public const int WallLayer = 8;
        public const int WallMask = 1 << WallLayer;

        static readonly string[][] SignText = new string[][]
        {
            /*Start   */ new string[] { "MUNCHCORP\nLOADING BAY" },
            /*Corridor*/ new string[] { "WET FLOOR\n(it is not water)", "SMILE: YOU ARE\nON CAMERA", "EMPLOYEES MUST\nSMILE. ALWAYS.", "SAFETY FIRST*\n*SECOND", "EXIT\n(decorative)" },
            /*Kitchen */ new string[] { "NO RUNNING.\nNO WALKING.\nNO ESCAPING.", "WASH HANDS BEFORE\nRETURNING TO THE VOID", "FRYER OIL\nLAST CHANGED: 1987" },
            /*Storage */ new string[] { "INVENTORY IS LIFE", "DO NOT OPEN\nBOX 7", "EXPIRED IS\nA STATE OF MIND" },
            /*Freezer */ new string[] { "COLD CHAIN\nWARM REGRETS", "KEEP DOOR CLOSED\nIT KNOWS WHEN YOU DON'T" },
            /*Dining  */ new string[] { "ENJOY YOUR MEAL\n(it enjoys you)", "NOW HIRING\nNO EXPERIENCE\nNO FUTURE", "KIDS EAT FREE\nADULTS EAT LATER" },
        };

        public static LevelData Build(Transform root, int seed)
        {
            var rng = new System.Random(seed);
            var d = new LevelData(W, H, CS);
            d.root = root;
            Maze(d, rng);
            AssignTypes(d, rng);

            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    BuildCell(d, x, y, rng);

            // Walls: internal edges once (N and E), plus the outer boundary
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (y < H - 1) Wall(d, x, y, 0, d.open[x, y, 0], rng); else Wall(d, x, y, 0, false, rng);
                    if (x < W - 1) Wall(d, x, y, 1, d.open[x, y, 1], rng); else Wall(d, x, y, 1, false, rng);
                    if (y == 0) Wall(d, x, y, 2, false, rng);
                    if (x == 0) Wall(d, x, y, 3, false, rng);
                }

            BuildStart(d);
            return d;
        }

        // ---------------- layout ----------------
        static void Maze(LevelData d, System.Random rng)
        {
            var visited = new bool[W, H];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(0, 0));
            visited[0, 0] = true;
            var nb = new List<int>();
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                nb.Clear();
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = c.x + LevelData.DX[dir], ny = c.y + LevelData.DY[dir];
                    if (d.InBounds(nx, ny) && !visited[nx, ny]) nb.Add(dir);
                }
                if (nb.Count == 0) { stack.Pop(); continue; }
                int pick = nb[rng.Next(nb.Count)];
                int ax = c.x + LevelData.DX[pick], ay = c.y + LevelData.DY[pick];
                OpenEdge(d, c.x, c.y, pick);
                visited[ax, ay] = true;
                stack.Push(new Vector2Int(ax, ay));
            }
            // extra loops so the Inspector can't be dodged by simply picking one corridor
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    for (int dir = 0; dir < 2; dir++)
                    {
                        int nx = x + LevelData.DX[dir], ny = y + LevelData.DY[dir];
                        if (d.InBounds(nx, ny) && !d.open[x, y, dir] && rng.NextDouble() < 0.28) OpenEdge(d, x, y, dir);
                    }
            OpenEdge(d, 0, 0, 0);
            OpenEdge(d, 0, 0, 1);
        }

        static void OpenEdge(LevelData d, int x, int y, int dir)
        {
            int nx = x + LevelData.DX[dir], ny = y + LevelData.DY[dir];
            if (!d.InBounds(nx, ny)) return;
            d.open[x, y, dir] = true;
            d.open[nx, ny, (dir + 2) % 4] = true;
        }

        static void AssignTypes(LevelData d, System.Random rng)
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (x != 0 || y != 0) cells.Add(new Vector2Int(x, y));
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = cells[i]; cells[i] = cells[j]; cells[j] = t;
            }
            d.type[0, 0] = CellType.Start;
            for (int i = 0; i < cells.Count; i++)
            {
                CellType t = CellType.Corridor;
                if (i < 4) t = CellType.Kitchen;
                else if (i < 12) t = CellType.Storage;
                else if (i < 14) t = CellType.Freezer;
                else if (i < 17) t = CellType.Dining;
                d.type[cells[i].x, cells[i].y] = t;
            }
        }

        // ---------------- geometry helpers ----------------
        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, Vector2 tile, bool collider = true, int layer = WallLayer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (tile != Vector2.zero)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector("_MainTex_ST", new Vector4(tile.x, tile.y, 0f, 0f));
                r.SetPropertyBlock(mpb);
            }
            if (!collider) { var c = g.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
            g.layer = layer;
            return g;
        }

        static Material WallMat(CellType t)
        {
            switch (t)
            {
                case CellType.Kitchen: return Mats.Get("wallK", Color.white, 0.35f, Mats.Tex("wallKT", 0, new Color(0.55f, 0.7f, 0.62f), new Color(0.2f, 0.25f, 0.22f)));
                case CellType.Storage: return Mats.Get("wallS", Color.white, 0.05f, Mats.Tex("wallST", 2, new Color(0.4f, 0.4f, 0.37f), new Color(0.55f, 0.55f, 0.5f)));
                case CellType.Dining: return Mats.Get("wallD", Color.white, 0.2f, Mats.Tex("wallDT", 0, new Color(0.75f, 0.5f, 0.2f), new Color(0.4f, 0.25f, 0.1f)));
                case CellType.Freezer: return Mats.Get("wallF", Color.white, 0.5f, Mats.Tex("wallFT", 0, new Color(0.6f, 0.75f, 0.85f), new Color(0.3f, 0.4f, 0.5f)));
                case CellType.Start: return Mats.Get("wallB", Color.white, 0.2f, Mats.Tex("wallBT", 2, new Color(0.7f, 0.62f, 0.25f), new Color(0.5f, 0.45f, 0.15f)));
                default: return Mats.Get("wallC", Color.white, 0.1f, Mats.Tex("wallCT", 2, new Color(0.35f, 0.45f, 0.4f), new Color(0.2f, 0.28f, 0.25f)));
            }
        }

        static Material FloorMat(CellType t)
        {
            switch (t)
            {
                case CellType.Kitchen: return Mats.Get("floorK", Color.white, 0.5f, Mats.Tex("floorKT", 0, new Color(0.82f, 0.82f, 0.76f), new Color(0.25f, 0.25f, 0.22f)));
                case CellType.Storage: return Mats.Get("floorS", Color.white, 0.1f, Mats.Tex("floorST", 2, new Color(0.22f, 0.22f, 0.22f), new Color(0.38f, 0.38f, 0.36f)));
                case CellType.Dining: return Mats.Get("floorD", Color.white, 0.4f, Mats.Tex("floorDT", 1, new Color(0.65f, 0.1f, 0.08f), new Color(0.85f, 0.8f, 0.68f)));
                case CellType.Freezer: return Mats.Get("floorF", Color.white, 0.7f, Mats.Tex("floorFT", 0, new Color(0.55f, 0.65f, 0.72f), new Color(0.25f, 0.3f, 0.35f)));
                case CellType.Start: return Mats.Get("floorB", Color.white, 0.2f, Mats.Tex("floorBT", 2, new Color(0.3f, 0.3f, 0.3f), new Color(0.42f, 0.42f, 0.4f)));
                default: return Mats.Get("floorC", Color.white, 0.4f, Mats.Tex("floorCT", 0, new Color(0.28f, 0.3f, 0.29f), new Color(0.1f, 0.1f, 0.1f)));
            }
        }

        static Material CeilMat() { return Mats.Get("ceil", Color.white, 0.05f, Mats.Tex("ceilT", 0, new Color(0.3f, 0.3f, 0.28f), new Color(0.12f, 0.12f, 0.12f))); }

        // ---------------- walls ----------------
        static void Wall(LevelData d, int x, int y, int dir, bool isOpen, System.Random rng)
        {
            Vector3 c = d.Center(x, y);
            Vector3 mid, along;
            switch (dir)
            {
                case 0: mid = c + new Vector3(0, 0, CS / 2f); along = Vector3.right; break;
                case 1: mid = c + new Vector3(CS / 2f, 0, 0); along = Vector3.forward; break;
                case 2: mid = c + new Vector3(0, 0, -CS / 2f); along = Vector3.right; break;
                default: mid = c + new Vector3(-CS / 2f, 0, 0); along = Vector3.forward; break;
            }
            bool alongX = (dir == 0 || dir == 2);
            Material mat = WallMat(d.type[x, y]);
            if (!isOpen)
            {
                Seg(d.root, mid + Vector3.up * (WH / 2f), alongX, CS + 0.3f, WH, mat);
                return;
            }
            float flank = (CS - GAP) / 2f;
            float off = GAP / 2f + flank / 2f;
            Seg(d.root, mid + along * off + Vector3.up * (WH / 2f), alongX, flank + 0.15f, WH, mat);
            Seg(d.root, mid - along * off + Vector3.up * (WH / 2f), alongX, flank + 0.15f, WH, mat);
            float lh = WH - DOORH;
            Seg(d.root, mid + Vector3.up * (DOORH + lh / 2f), alongX, GAP, lh, mat);
            // door in roughly a third of doorways (never on the start cell exits)
            bool nearStart = (x == 0 && y == 0) || (dir == 0 && x == 0 && y == 0);
            if (!nearStart && rng.NextDouble() < 0.35)
            {
                Vector3 size = alongX ? new Vector3(GAP, DOORH, 0.14f) : new Vector3(0.14f, DOORH, GAP);
                var door = Box(d.root, "Door", mid + Vector3.up * (DOORH / 2f), size, Mats.Steel, new Vector2(1.5f, 1.3f));
                door.AddComponent<Door>();
            }
        }

        static void Seg(Transform root, Vector3 center, bool alongX, float len, float h, Material m)
        {
            Vector3 size = alongX ? new Vector3(len, h, 0.3f) : new Vector3(0.3f, h, len);
            Box(root, "Wall", center, size, m, new Vector2(len / 2f, h / 2f));
        }

        // ---------------- cells ----------------
        static void BuildCell(LevelData d, int x, int y, System.Random rng)
        {
            var t = d.type[x, y];
            Vector3 c = d.Center(x, y);
            Box(d.root, "Floor", c + new Vector3(0, -0.1f, 0), new Vector3(CS + 0.02f, 0.2f, CS + 0.02f), FloorMat(t), new Vector2(4, 4));
            Box(d.root, "Ceiling", c + new Vector3(0, WH + 0.1f, 0), new Vector3(CS + 0.02f, 0.2f, CS + 0.02f), CeilMat(), new Vector2(4, 4));

            // lights
            bool lit = t == CellType.Start || rng.NextDouble() < 0.65;
            if (lit)
            {
                Box(d.root, "Tube", c + new Vector3(0, WH - 0.04f, 0), new Vector3(1.8f, 0.06f, 0.3f), Mats.Tube, Vector2.zero, false);
                var lg = new GameObject("Light");
                lg.transform.SetParent(d.root, false);
                lg.transform.position = c + new Vector3(0, WH - 0.5f, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 12f;
                l.shadows = LightShadows.None;
                float baseI = 1.0f;
                switch (t)
                {
                    case CellType.Freezer: l.color = new Color(0.55f, 0.75f, 1f); break;
                    case CellType.Kitchen: l.color = new Color(0.85f, 1f, 0.85f); break;
                    case CellType.Dining: l.color = new Color(1f, 0.7f, 0.45f); baseI = 0.9f; break;
                    case CellType.Storage: l.color = new Color(1f, 0.9f, 0.6f); baseI = 0.9f; break;
                    case CellType.Start: l.color = new Color(1f, 0.95f, 0.8f); baseI = 1.4f; break;
                    default: l.color = new Color(0.8f, 1f, 0.9f); break;
                }
                l.intensity = baseI;
                var f = lg.AddComponent<Flicker>();
                f.L = l;
                f.baseIntensity = baseI;
                f.dying = t != CellType.Start && rng.NextDouble() < 0.25;
            }

            // ceiling pipe
            if (rng.NextDouble() < 0.45)
            {
                bool px = rng.NextDouble() < 0.5;
                float off = (float)(rng.NextDouble() * 5.0 - 2.5);
                Vector3 pp = c + (px ? new Vector3(0, 3.15f, off) : new Vector3(off, 3.15f, 0));
                var pipe = Mats.Part(d.root, PrimitiveType.Cylinder, "Pipe", pp, new Vector3(0.16f, CS / 2f, 0.16f), Mats.Pipe, px ? new Vector3(0, 0, 90) : new Vector3(90, 0, 0));
                pipe.transform.position = pp;
            }

            if (t != CellType.Start) Props(d, x, y, c, t, rng);
            WallSign(d, x, y, c, t, rng);
        }

        static void WallSign(LevelData d, int x, int y, Vector3 c, CellType t, System.Random rng)
        {
            var texts = SignText[(int)t];
            int start = rng.Next(4);
            for (int k = 0; k < 4; k++)
            {
                int dir = (start + k) % 4;
                if (d.open[x, y, dir]) continue;
                if (t == CellType.Start && dir != 0 && dir != 1) { }
                Vector3 outward = new Vector3(LevelData.DX[dir], 0, LevelData.DY[dir]);
                Vector3 pos = c + outward * (CS / 2f - 0.2f) + Vector3.up * 2.2f;
                string s = texts[rng.Next(texts.Length)];
                if (t == CellType.Dining && rng.NextDouble() < 0.5) s = "MENU\nBURGER  $5.99\nFRIES  $2.99\nSPECIAL  $0.00\n(ask no questions)";
                int lines = s.Split('\n').Length;
                Mats.Sign(d.root, s, pos, Quaternion.LookRotation(outward), 2.6f, 0.45f * lines + 0.2f, new Color(1f, 0.85f, 0.25f));
                return;
            }
        }

        // ---------------- props (kept out of the center "lanes" so the Inspector can path) ----------------
        static void Props(LevelData d, int x, int y, Vector3 c, CellType t, System.Random rng)
        {
            for (int qx = -1; qx <= 1; qx += 2)
                for (int qz = -1; qz <= 1; qz += 2)
                {
                    Vector3 q = c + new Vector3(qx * 2.9f, 0, qz * 2.9f);
                    double r = rng.NextDouble();
                    switch (t)
                    {
                        case CellType.Kitchen:
                            if (r < 0.7) Counter(d, q, rng); else Fridge(d, q);
                            break;
                        case CellType.Storage:
                            if (r < 0.65) Shelf(d, c, qx, qz, rng, false); else Crates(d, q, rng);
                            break;
                        case CellType.Freezer:
                            if (r < 0.6) Shelf(d, c, qx, qz, rng, true); else Pallet(d, q, rng);
                            break;
                        case CellType.Dining:
                            if (r < 0.85) Table(d, q, rng);
                            break;
                        default:
                            if (r < 0.35) Crates(d, q, rng); else if (r < 0.5) Barrel(d, q);
                            break;
                    }
                }
        }

        static void Counter(LevelData d, Vector3 q, System.Random rng)
        {
            Box(d.root, "Counter", q + new Vector3(0, 0.475f, 0), new Vector3(1.4f, 0.95f, 1.4f), Mats.Steel, new Vector2(1, 1));
            if (rng.NextDouble() < 0.5)
            {
                Mats.Part(d.root, PrimitiveType.Cylinder, "Burner", q + new Vector3(-0.35f, 0.96f, -0.3f), new Vector3(0.4f, 0.02f, 0.4f), Mats.Dark);
            }
            d.slots.Add(q + new Vector3((float)(rng.NextDouble() * 0.5 - 0.1), 0.97f, (float)(rng.NextDouble() * 0.6 - 0.2)));
        }

        static void Fridge(LevelData d, Vector3 q)
        {
            Box(d.root, "Fridge", q + new Vector3(0, 1f, 0), new Vector3(1.2f, 2f, 1.2f), Mats.Steel, new Vector2(1, 1));
            Box(d.root, "Handle", q + new Vector3(0, 1.1f, 0.62f), new Vector3(0.06f, 0.7f, 0.06f), Mats.Dark, Vector2.zero, false);
        }

        static void Shelf(LevelData d, Vector3 c, int qx, int qz, System.Random rng, bool rare)
        {
            Vector3 s = c + new Vector3(qx * 2.9f, 0, qz * 3.5f);
            float[] tiers = { 0.4f, 1.1f, 1.8f };
            Box(d.root, "ShelfSideL", s + new Vector3(-0.78f, 1.1f, 0), new Vector3(0.05f, 2.2f, 0.6f), Mats.Steel, Vector2.zero);
            Box(d.root, "ShelfSideR", s + new Vector3(0.78f, 1.1f, 0), new Vector3(0.05f, 2.2f, 0.6f), Mats.Steel, Vector2.zero);
            for (int i = 0; i < tiers.Length; i++)
            {
                Box(d.root, "Board", s + new Vector3(0, tiers[i], 0), new Vector3(1.6f, 0.06f, 0.6f), Mats.Steel, new Vector2(1, 1));
                var list = rare ? d.rareSlots : d.slots;
                list.Add(s + new Vector3(-0.4f, tiers[i] + 0.04f, 0));
                list.Add(s + new Vector3(0.4f, tiers[i] + 0.04f, 0));
                if (rng.NextDouble() < 0.4)
                    Box(d.root, "Box", s + new Vector3(0.05f, tiers[i] + 0.2f, 0), new Vector3(0.3f, 0.34f, 0.35f), Mats.Cardboard, Vector2.zero, false);
            }
        }

        static void Crates(LevelData d, Vector3 q, System.Random rng)
        {
            for (int i = 0; i < 2; i++)
            {
                var g = Box(d.root, "Crate", q + new Vector3((float)(rng.NextDouble() - 0.5) * 0.6f, 0.35f + i * 0.72f, (float)(rng.NextDouble() - 0.5) * 0.6f), new Vector3(0.7f, 0.7f, 0.7f), Mats.Cardboard, Vector2.zero, true, 0);
                g.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 90f, 0);
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 3f;
                rb.linearDamping = 0.4f;
                rb.angularDamping = 0.8f;
            }
        }

        static void Barrel(LevelData d, Vector3 q)
        {
            var g = Mats.Part(d.root, PrimitiveType.Cylinder, "Barrel", q + new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.5f, 0.6f), Mats.Blue, default(Vector3), true);
            g.transform.position = q + new Vector3(0, 0.5f, 0);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 6f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;
        }

        static void Pallet(LevelData d, Vector3 q, System.Random rng)
        {
            Box(d.root, "Pallet", q + new Vector3(0, 0.075f, 0), new Vector3(1.2f, 0.15f, 1.2f), Mats.Wood, Vector2.zero);
            d.rareSlots.Add(q + new Vector3(-0.2f, 0.17f, 0));
            d.rareSlots.Add(q + new Vector3(0.3f, 0.17f, 0.2f));
        }

        static void Table(LevelData d, Vector3 q, System.Random rng)
        {
            var top = Mats.Part(d.root, PrimitiveType.Cylinder, "TableTop", q + new Vector3(0, 0.75f, 0), new Vector3(1.2f, 0.025f, 1.2f), Mats.Get("tableTop", new Color(0.7f, 0.2f, 0.15f), 0.4f), default(Vector3), true);
            top.transform.position = q + new Vector3(0, 0.75f, 0);
            top.layer = WallLayer;
            Mats.Part(d.root, PrimitiveType.Cylinder, "TableLeg", q + new Vector3(0, 0.37f, 0), new Vector3(0.12f, 0.37f, 0.12f), Mats.Steel);
            d.slots.Add(q + new Vector3(0, 0.78f, 0));
        }

        // ---------------- the loading bay (start cell) ----------------
        static void BuildStart(LevelData d)
        {
            Vector3 c = d.Center(0, 0);
            Transform r = d.root;

            // delivery van (west side, no doorway there)
            Box(r, "Van", c + new Vector3(-2.7f, 1.0f, 0), new Vector3(2.0f, 2.0f, 4.6f), Mats.VanWhite, Vector2.zero);
            Box(r, "VanStripe", c + new Vector3(-2.7f, 0.9f, 0), new Vector3(2.04f, 0.35f, 4.64f), Mats.Red, Vector2.zero, false);
            Box(r, "Windshield", c + new Vector3(-2.7f, 1.45f, 2.31f), new Vector3(1.6f, 0.7f, 0.04f), Mats.Dark, Vector2.zero, false);
            Box(r, "HeadL", c + new Vector3(-3.3f, 0.7f, 2.32f), new Vector3(0.3f, 0.2f, 0.05f), Mats.Headlight, Vector2.zero, false);
            Box(r, "HeadR", c + new Vector3(-2.1f, 0.7f, 2.32f), new Vector3(0.3f, 0.2f, 0.05f), Mats.Headlight, Vector2.zero, false);
            Box(r, "TailL", c + new Vector3(-3.3f, 0.7f, -2.32f), new Vector3(0.3f, 0.2f, 0.05f), Mats.Taillight, Vector2.zero, false);
            Box(r, "TailR", c + new Vector3(-2.1f, 0.7f, -2.32f), new Vector3(0.3f, 0.2f, 0.05f), Mats.Taillight, Vector2.zero, false);
            Mats.Sign(r, "MUNCH\nCORP", c + new Vector3(-1.66f, 1.45f, 0), Quaternion.LookRotation(Vector3.right * -1f), 1.8f, 0.8f, new Color(0.8f, 0.05f, 0.05f));

            // clock-out terminal (south-east)
            var term = Box(r, "Terminal", c + new Vector3(2.6f, 0.55f, -3.3f), new Vector3(1.6f, 1.1f, 0.7f), Mats.Steel, new Vector2(1, 1));
            term.AddComponent<DeliveryTerminal>();
            Box(r, "TerminalScreen", c + new Vector3(2.6f, 1.5f, -3.55f), new Vector3(1.2f, 0.7f, 0.06f), Mats.Screen, Vector2.zero, false);
            Mats.Sign(r, "DELIVERY\nTERMINAL", c + new Vector3(2.6f, 1.5f, -3.5f), Quaternion.LookRotation(Vector3.back), 1.0f, 0.5f, new Color(0.7f, 1f, 0.7f));

            // return chute pad (north-east): cargo left here is auto-delivered
            d.bayCenter = c + new Vector3(2.8f, 0, 2.8f);
            Box(r, "ChutePad", d.bayCenter + new Vector3(0, 0.05f, 0), new Vector3(2.2f, 0.1f, 2.2f), Mats.GlowGreen, Vector2.zero, true);
            Box(r, "HazardN", d.bayCenter + new Vector3(0, 0.07f, 1.15f), new Vector3(2.4f, 0.06f, 0.15f), Mats.Hazard, new Vector2(8, 1), false);
            Box(r, "HazardS", d.bayCenter + new Vector3(0, 0.07f, -1.15f), new Vector3(2.4f, 0.06f, 0.15f), Mats.Hazard, new Vector2(8, 1), false);
            Box(r, "HazardW", d.bayCenter + new Vector3(-1.15f, 0.07f, 0), new Vector3(0.15f, 0.06f, 2.2f), Mats.Hazard, new Vector2(1, 8), false);
            Box(r, "HazardE", d.bayCenter + new Vector3(1.15f, 0.07f, 0), new Vector3(0.15f, 0.06f, 2.2f), Mats.Hazard, new Vector2(1, 8), false);
            var gl = new GameObject("ChuteLight");
            gl.transform.SetParent(r, false);
            gl.transform.position = d.bayCenter + new Vector3(0, 1.6f, 0);
            var l = gl.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.2f, 1f, 0.35f); l.range = 7f; l.intensity = 1.3f; l.shadows = LightShadows.None;
            Mats.Sign(r, "RETURN CHUTE\nDROP FOOD HERE", c + new Vector3(2.8f, 2.4f, 3.7f), Quaternion.LookRotation(Vector3.forward), 2.4f, 0.8f, new Color(0.5f, 1f, 0.55f));

            d.spawnPos = c + new Vector3(0.4f, 0.05f, -0.3f);
            d.spawnYaw = 90f;
        }

        // ---------------- hazards ----------------
        public static void PlaceGrease(LevelData d, Transform parent, System.Random rng, int count)
        {
            int placed = 0, guard = 0;
            while (placed < count && guard++ < 200)
            {
                int x = rng.Next(W), y = rng.Next(H);
                if (d.type[x, y] == CellType.Start) continue;
                var c = d.Center(x, y);
                float ox = (float)(rng.NextDouble() * 2.4 - 1.2), oz = (float)(rng.NextDouble() * 2.4 - 1.2);
                GreaseSpill.Spawn(parent, c + new Vector3(ox, 0, oz), 0.95f, -1f);
                placed++;
            }
        }
    }
}
