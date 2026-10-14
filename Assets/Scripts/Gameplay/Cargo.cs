using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    public enum CargoKind { Pizza, Burger, Fries, Sauce, Combo }

    /// <summary>A piece of valuable food cargo. Physics object that can be carried, dropped, thrown and delivered.</summary>
    public class Cargo : MonoBehaviour
    {
        public static readonly List<Cargo> All = new List<Cargo>();

        public CargoKind kind;
        public string label;
        public int value;
        public float weight;
        public bool carried;
        public Rigidbody rb;

        float born, bayTime, lastNoise;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Awake() { born = Time.time; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing || carried) return;

            if (transform.position.y < -3f)
            {
                // fell out of the world: put it back on the chute pad
                transform.position = gm.Level.bayCenter + new Vector3(0, 1.5f, 0);
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            if (gm.InBay(transform.position)) bayTime += Time.deltaTime; else bayTime = 0f;
            if (bayTime > 0.8f) gm.DeliverOne(this);
        }

        void OnCollisionEnter(Collision col)
        {
            if (carried || Time.time - born < 1.5f) return;
            float v = col.relativeVelocity.magnitude;
            if (v < 3f) return;

            var ins = col.collider.GetComponentInParent<Inspector>();
            if (ins != null && v > 5f)
            {
                ins.Stun(1.6f, true);
                var gm0 = GameManager.I;
                if (gm0 != null) gm0.Toast("DIRECT HIT! The Inspector is wearing your " + label + ".");
            }

            if (Time.time - lastNoise < 0.3f) return;
            lastNoise = Time.time;
            float vol = Mathf.Clamp01(v / 10f);
            ProcAudio.PlayAt(ProcAudio.Drop, transform.position, 0.4f + vol * 0.6f);
            var gm = GameManager.I;
            if (gm != null) gm.MakeNoise(transform.position, Mathf.Clamp(v * 1.8f, 3f, 14f) * (weight > 3f ? 1.2f : 1f));
        }
    }

    public static class CargoFactory
    {
        public static string NameOf(CargoKind k)
        {
            switch (k)
            {
                case CargoKind.Pizza: return "Pizza Box";
                case CargoKind.Burger: return "Double Cheeseburger";
                case CargoKind.Fries: return "Fries Bucket";
                case CargoKind.Sauce: return "Mystery Sauce Crate";
                default: return "Giant Combo Meal";
            }
        }

        public static int ValueOf(CargoKind k)
        {
            switch (k)
            {
                case CargoKind.Pizza: return 90;
                case CargoKind.Burger: return 150;
                case CargoKind.Fries: return 40;
                case CargoKind.Sauce: return 180;
                default: return 450;
            }
        }

        public static float WeightOf(CargoKind k)
        {
            switch (k)
            {
                case CargoKind.Pizza: return 2f;
                case CargoKind.Burger: return 4f;
                case CargoKind.Fries: return 1f;
                case CargoKind.Sauce: return 5f;
                default: return 8f;
            }
        }

        static Vector3 ColSize(CargoKind k)
        {
            switch (k)
            {
                case CargoKind.Pizza: return new Vector3(0.55f, 0.1f, 0.55f);
                case CargoKind.Burger: return new Vector3(0.42f, 0.4f, 0.42f);
                case CargoKind.Fries: return new Vector3(0.34f, 0.5f, 0.34f);
                case CargoKind.Sauce: return new Vector3(0.5f, 0.4f, 0.5f);
                default: return new Vector3(0.9f, 0.45f, 0.65f);
            }
        }

        /// <summary>Visual-only hierarchy (origin at the bottom). Also used for the menu backdrop.</summary>
        public static GameObject Visual(CargoKind k)
        {
            var root = new GameObject(NameOf(k));
            Transform t = root.transform;
            switch (k)
            {
                case CargoKind.Pizza:
                    Mats.Part(t, PrimitiveType.Cube, "Box", new Vector3(0, 0.04f, 0), new Vector3(0.55f, 0.07f, 0.55f), Mats.Cardboard);
                    Mats.Part(t, PrimitiveType.Cube, "Stripe", new Vector3(0, 0.076f, 0), new Vector3(0.5f, 0.006f, 0.12f), Mats.Red);
                    Mats.Part(t, PrimitiveType.Cube, "Dot", new Vector3(0, 0.08f, 0.18f), new Vector3(0.16f, 0.006f, 0.16f), Mats.Yellow, new Vector3(0, 45, 0));
                    break;
                case CargoKind.Burger:
                    Mats.Part(t, PrimitiveType.Cylinder, "BunBottom", new Vector3(0, 0.04f, 0), new Vector3(0.4f, 0.04f, 0.4f), Mats.Bun);
                    Mats.Part(t, PrimitiveType.Cylinder, "Patty1", new Vector3(0, 0.11f, 0), new Vector3(0.42f, 0.03f, 0.42f), Mats.Patty);
                    Mats.Part(t, PrimitiveType.Cube, "Cheese1", new Vector3(0, 0.15f, 0), new Vector3(0.38f, 0.012f, 0.38f), Mats.Cheese, new Vector3(0, 45, 0));
                    Mats.Part(t, PrimitiveType.Cylinder, "Patty2", new Vector3(0, 0.19f, 0), new Vector3(0.42f, 0.03f, 0.42f), Mats.Patty);
                    Mats.Part(t, PrimitiveType.Cube, "Cheese2", new Vector3(0, 0.23f, 0), new Vector3(0.38f, 0.012f, 0.38f), Mats.Cheese, new Vector3(0, 20, 0));
                    Mats.Part(t, PrimitiveType.Sphere, "BunTop", new Vector3(0, 0.27f, 0), new Vector3(0.42f, 0.22f, 0.42f), Mats.Bun);
                    break;
                case CargoKind.Fries:
                    Mats.Part(t, PrimitiveType.Cylinder, "Bucket", new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.17f, 0.3f), Mats.Red);
                    Mats.Part(t, PrimitiveType.Cylinder, "Band", new Vector3(0, 0.2f, 0), new Vector3(0.31f, 0.05f, 0.31f), Mats.Yellow);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 0.785f;
                        Mats.Part(t, PrimitiveType.Cube, "Fry", new Vector3(Mathf.Cos(a) * 0.08f, 0.44f, Mathf.Sin(a) * 0.08f), new Vector3(0.04f, 0.24f, 0.04f), Mats.Fry, new Vector3(Mathf.Sin(a) * 18f, 0, Mathf.Cos(a) * 18f));
                    }
                    break;
                case CargoKind.Sauce:
                    Mats.Part(t, PrimitiveType.Cube, "Crate", new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.4f, 0.5f), Mats.SauceCrate);
                    Mats.Part(t, PrimitiveType.Cylinder, "Cap", new Vector3(0, 0.43f, 0), new Vector3(0.18f, 0.03f, 0.18f), Mats.Dark);
                    Mats.Part(t, PrimitiveType.Cube, "Label", new Vector3(0, 0.2f, 0.255f), new Vector3(0.3f, 0.18f, 0.01f), Mats.Yellow);
                    break;
                default:
                    Mats.Part(t, PrimitiveType.Cube, "Tray", new Vector3(0, 0.04f, 0), new Vector3(0.9f, 0.08f, 0.65f), Mats.Red);
                    Mats.Part(t, PrimitiveType.Cylinder, "Cup", new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0.16f, 0.18f, 0.16f), Mats.White);
                    Mats.Part(t, PrimitiveType.Cylinder, "CupStripe", new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0.17f, 0.06f, 0.17f), Mats.Red);
                    Mats.Part(t, PrimitiveType.Cylinder, "MiniBunB", new Vector3(-0.2f, 0.12f, 0.0f), new Vector3(0.3f, 0.04f, 0.3f), Mats.Bun);
                    Mats.Part(t, PrimitiveType.Cylinder, "MiniPatty", new Vector3(-0.2f, 0.17f, 0.0f), new Vector3(0.32f, 0.03f, 0.32f), Mats.Patty);
                    Mats.Part(t, PrimitiveType.Sphere, "MiniBunT", new Vector3(-0.2f, 0.24f, 0.0f), new Vector3(0.3f, 0.17f, 0.3f), Mats.Bun);
                    Mats.Part(t, PrimitiveType.Cube, "FryBox", new Vector3(0.05f, 0.18f, -0.2f), new Vector3(0.2f, 0.2f, 0.14f), Mats.Yellow);
                    break;
            }
            return root;
        }

        public static Cargo Spawn(CargoKind k, Vector3 pos, Transform parent, float valueMult)
        {
            var root = Visual(k);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            Vector3 sz = ColSize(k);
            var col = root.AddComponent<BoxCollider>();
            col.size = sz;
            col.center = new Vector3(0, sz.y / 2f, 0);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.4f, WeightOf(k) * 0.5f);
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var c = root.AddComponent<Cargo>();
            c.kind = k;
            c.label = NameOf(k);
            c.value = Mathf.RoundToInt(ValueOf(k) * valueMult);
            c.weight = WeightOf(k);
            c.rb = rb;

            if (k == CargoKind.Sauce)
            {
                var lg = new GameObject("Glow");
                lg.transform.SetParent(root.transform, false);
                lg.transform.localPosition = new Vector3(0, 0.6f, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.3f, 1f, 0.2f); l.range = 3.5f; l.intensity = 0.8f; l.shadows = LightShadows.None;
            }
            return c;
        }

        static CargoKind Roll(System.Random rng)
        {
            int r = rng.Next(100);
            if (r < 28) return CargoKind.Fries;
            if (r < 58) return CargoKind.Pizza;
            if (r < 80) return CargoKind.Burger;
            if (r < 92) return CargoKind.Sauce;
            return CargoKind.Combo;
        }

        static void Shuffle(List<Vector3> l, System.Random rng)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        public static void Populate(LevelData d, Transform parent, float mult, System.Random rng, int count)
        {
            var s = new List<Vector3>(d.slots);
            var r = new List<Vector3>(d.rareSlots);
            Shuffle(s, rng);
            Shuffle(r, rng);
            int placed = 0;
            while (placed < count)
            {
                CargoKind k = Roll(rng);
                Vector3 slot;
                bool rare = (k == CargoKind.Combo || k == CargoKind.Sauce);
                if (rare && r.Count > 0) { slot = r[r.Count - 1]; r.RemoveAt(r.Count - 1); }
                else if (s.Count > 0)
                {
                    if (k == CargoKind.Combo) k = CargoKind.Burger; // giant meals only live in the freezer
                    slot = s[s.Count - 1]; s.RemoveAt(s.Count - 1);
                }
                else if (r.Count > 0) { slot = r[r.Count - 1]; r.RemoveAt(r.Count - 1); }
                else break;

                Vector3 p = slot + new Vector3(0, 0.03f, 0);
                if (k == CargoKind.Fries)
                {
                    Spawn(k, p + new Vector3(-0.17f, 0, 0), parent, mult);
                    Spawn(k, p + new Vector3(0.17f, 0, 0.05f), parent, mult);
                    Spawn(k, p + new Vector3(0f, 0, -0.15f), parent, mult);
                }
                else Spawn(k, p, parent, mult);
                placed++;
            }
        }
    }
}
