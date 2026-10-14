using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    public enum CellType { Start, Corridor, Kitchen, Storage, Freezer, Dining }

    /// <summary>Grid layout of the facility + BFS pathfinding used by the Inspector.</summary>
    public class LevelData
    {
        public int W, H;
        public float CS;
        public bool[,,] open;          // [x,y,dir] dir: 0=N(+z) 1=E(+x) 2=S 3=W
        public CellType[,] type;
        public Transform root;
        public List<Vector3> slots = new List<Vector3>();
        public List<Vector3> rareSlots = new List<Vector3>();
        public Vector3 spawnPos, bayCenter;
        public float spawnYaw = 90f;
        public float bayHalf = 1.1f;

        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { 1, 0, -1, 0 };

        public LevelData(int w, int h, float cs)
        {
            W = w; H = h; CS = cs;
            open = new bool[w, h, 4];
            type = new CellType[w, h];
        }

        public Vector3 Center(int x, int y) { return new Vector3(x * CS, 0f, y * CS); }

        public Vector2Int CellOf(Vector3 p)
        {
            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(p.x / CS), 0, W - 1), Mathf.Clamp(Mathf.RoundToInt(p.z / CS), 0, H - 1));
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }

        public List<Vector2Int> Path(Vector2Int a, Vector2Int b)
        {
            var prev = new Vector2Int[W, H];
            var seen = new bool[W, H];
            var q = new Queue<Vector2Int>();
            q.Enqueue(a);
            seen[a.x, a.y] = true;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == b) break;
                for (int d = 0; d < 4; d++)
                {
                    if (!open[c.x, c.y, d]) continue;
                    int nx = c.x + DX[d], ny = c.y + DY[d];
                    if (!InBounds(nx, ny) || seen[nx, ny]) continue;
                    seen[nx, ny] = true;
                    prev[nx, ny] = c;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
            var path = new List<Vector2Int>();
            if (!seen[b.x, b.y]) { path.Add(a); return path; }
            var cur = b;
            while (cur != a) { path.Add(cur); cur = prev[cur.x, cur.y]; }
            path.Add(a);
            path.Reverse();
            return path;
        }

        public Vector2Int RandomCell(System.Random rng, int minDistFromStart)
        {
            var start = new Vector2Int(0, 0);
            var list = new List<Vector2Int>();
            Vector2Int farthest = start; int best = -1;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    int d = Path(start, new Vector2Int(x, y)).Count - 1;
                    if (d >= minDistFromStart) list.Add(new Vector2Int(x, y));
                    if (d > best) { best = d; farthest = new Vector2Int(x, y); }
                }
            if (list.Count == 0) return farthest;
            return list[rng.Next(list.Count)];
        }
    }

    /// <summary>Sliding door: opens when the player or an inspector is near.</summary>
    public class Door : MonoBehaviour
    {
        Vector3 closedPos;
        float openAmt;
        bool wasWant;

        void Awake() { closedPos = transform.position; }

        void Update()
        {
            bool want = false;
            var gm = GameManager.I;
            if (gm != null && gm.Player != null) want = Near(gm.Player.transform.position, 3.2f);
            if (!want)
                for (int i = 0; i < Inspector.All.Count; i++)
                    if (Near(Inspector.All[i].transform.position, 3.6f)) { want = true; break; }
            if (want != wasWant)
            {
                wasWant = want;
                ProcAudio.PlayAt(ProcAudio.Door, closedPos, 0.6f);
            }
            openAmt = Mathf.MoveTowards(openAmt, want ? 1f : 0f, Time.deltaTime * 2.6f);
            transform.position = closedPos + Vector3.up * (openAmt * 2.5f);
        }

        bool Near(Vector3 p, float r)
        {
            float dx = p.x - closedPos.x, dz = p.z - closedPos.z;
            return dx * dx + dz * dz < r * r;
        }
    }

    /// <summary>Flickering fluorescent light.</summary>
    public class Flicker : MonoBehaviour
    {
        public Light L;
        public float baseIntensity = 1f;
        public bool dying;
        float burst;

        void Update()
        {
            if (L == null) return;
            if (burst > 0f)
            {
                burst -= Time.deltaTime;
                L.intensity = Random.value < 0.5f ? 0.05f : baseIntensity * 0.6f;
                return;
            }
            L.intensity = baseIntensity;
            if (Random.value < (dying ? 0.03f : 0.0015f)) burst = Random.Range(0.1f, 0.5f);
        }
    }

    /// <summary>Marker for the clock-out / delivery terminal.</summary>
    public class DeliveryTerminal : MonoBehaviour { }

    /// <summary>Slippery grease puddle. Players and Inspectors that step in it slide and stumble.</summary>
    public class GreaseSpill : MonoBehaviour
    {
        public static readonly List<GreaseSpill> All = new List<GreaseSpill>();
        public float radius = 0.9f;
        public float life = -1f;
        float age;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static GreaseSpill Spawn(Transform parent, Vector3 pos, float radius, float life)
        {
            var g = Mats.Part(parent, PrimitiveType.Cylinder, "Grease", new Vector3(pos.x, 0.02f, pos.z), new Vector3(radius * 2f, 0.01f, radius * 2f), Mats.Grease);
            g.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            var s = g.AddComponent<GreaseSpill>();
            s.radius = radius;
            s.life = life;
            return s;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            if (life > 0f)
            {
                age += Time.deltaTime;
                if (age > life) { Destroy(gameObject); return; }
            }
            var p = gm.Player;
            if (p != null)
            {
                float dx = p.transform.position.x - transform.position.x;
                float dz = p.transform.position.z - transform.position.z;
                if (dx * dx + dz * dz < radius * radius && p.transform.position.y < 0.5f) p.Slip();
            }
            for (int i = 0; i < Inspector.All.Count; i++)
            {
                var ins = Inspector.All[i];
                float dx = ins.transform.position.x - transform.position.x;
                float dz = ins.transform.position.z - transform.position.z;
                if (dx * dx + dz * dz < radius * radius) ins.SlipOnGrease();
            }
        }
    }
}
