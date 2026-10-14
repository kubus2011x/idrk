using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    /// <summary>
    /// THE HEALTH INSPECTOR. Patrols, hears noise, sees (and chases) the player, issues citations.
    /// Pathing is BFS over the room grid; direct pursuit when line of sight is clear.
    /// </summary>
    public class Inspector : MonoBehaviour
    {
        public static readonly List<Inspector> All = new List<Inspector>();
        public enum S { Patrol, Investigate, Chase, Search, Stunned }

        public S state = S.Patrol;
        public bool IsChasing { get { return state == S.Chase; } }

        LevelData L;
        float walkSpeed = 2.2f, runSpeed = 5f;
        readonly List<Vector3> route = new List<Vector3>();
        int ri;
        Vector3 lastKnown;
        float lastSeen = -99f, stunT, searchT, repathT, senseT, stepT, slipCool, anim;
        Transform head, lLegPivot, rLegPivot, lArmPivot, body;
        Light lamp;
        AudioSource hum;
        System.Random rng = new System.Random();

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Inspector Create(Transform parent, LevelData lvl, Vector3 pos, float speedBonus)
        {
            var g = new GameObject("HealthInspector");
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            Transform t = g.transform;

            var bodyT = new GameObject("Body").transform;
            bodyT.SetParent(t, false);

            // legs (pivot at hip)
            var lp = new GameObject("LegL").transform; lp.SetParent(bodyT, false); lp.localPosition = new Vector3(-0.22f, 1.25f, 0);
            var rp = new GameObject("LegR").transform; rp.SetParent(bodyT, false); rp.localPosition = new Vector3(0.22f, 1.25f, 0);
            Mats.Part(lp, PrimitiveType.Cube, "Leg", new Vector3(0, -0.6f, 0), new Vector3(0.28f, 1.2f, 0.28f), Mats.Pants);
            Mats.Part(lp, PrimitiveType.Cube, "Shoe", new Vector3(0, -1.18f, 0.1f), new Vector3(0.32f, 0.15f, 0.55f), Mats.Dark);
            Mats.Part(rp, PrimitiveType.Cube, "Leg", new Vector3(0, -0.6f, 0), new Vector3(0.28f, 1.2f, 0.28f), Mats.Pants);
            Mats.Part(rp, PrimitiveType.Cube, "Shoe", new Vector3(0, -1.18f, 0.1f), new Vector3(0.32f, 0.15f, 0.55f), Mats.Dark);

            // torso + tie
            Mats.Part(bodyT, PrimitiveType.Cube, "Torso", new Vector3(0, 1.75f, 0), new Vector3(0.95f, 1.0f, 0.5f), Mats.Suit);
            Mats.Part(bodyT, PrimitiveType.Cube, "Tie", new Vector3(0, 1.8f, 0.26f), new Vector3(0.12f, 0.65f, 0.03f), Mats.Red);

            // arms: left swings, right holds the clipboard
            var la = new GameObject("ArmL").transform; la.SetParent(bodyT, false); la.localPosition = new Vector3(-0.58f, 2.2f, 0);
            Mats.Part(la, PrimitiveType.Cube, "Arm", new Vector3(0, -0.45f, 0), new Vector3(0.2f, 0.95f, 0.2f), Mats.Suit);
            var ra = new GameObject("ArmR").transform; ra.SetParent(bodyT, false); ra.localPosition = new Vector3(0.58f, 2.2f, 0); ra.localRotation = Quaternion.Euler(-70, 0, 0);
            Mats.Part(ra, PrimitiveType.Cube, "Arm", new Vector3(0, -0.45f, 0), new Vector3(0.2f, 0.95f, 0.2f), Mats.Suit);
            Mats.Part(ra, PrimitiveType.Cube, "Clipboard", new Vector3(-0.1f, -0.95f, 0.05f), new Vector3(0.55f, 0.7f, 0.04f), Mats.Wood, new Vector3(70, 0, 0));
            Mats.Part(ra, PrimitiveType.Cube, "Paper", new Vector3(-0.1f, -0.95f, 0.08f), new Vector3(0.46f, 0.58f, 0.01f), Mats.White, new Vector3(70, 0, 0));

            // head: pale, masked, capped, with glowing eyes and a headlamp
            var h = new GameObject("Head").transform; h.SetParent(bodyT, false); h.localPosition = new Vector3(0, 2.5f, 0);
            Mats.Part(h, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.55f, 0.68f, 0.55f), Mats.Pale);
            Mats.Part(h, PrimitiveType.Cube, "Mask", new Vector3(0, -0.12f, 0.24f), new Vector3(0.34f, 0.22f, 0.06f), Mats.White);
            Mats.Part(h, PrimitiveType.Sphere, "EyeL", new Vector3(-0.1f, 0.08f, 0.24f), new Vector3(0.1f, 0.07f, 0.06f), Mats.EyeGlow);
            Mats.Part(h, PrimitiveType.Sphere, "EyeR", new Vector3(0.1f, 0.08f, 0.24f), new Vector3(0.1f, 0.07f, 0.06f), Mats.EyeGlow);
            Mats.Part(h, PrimitiveType.Cylinder, "CapTop", new Vector3(0, 0.3f, 0), new Vector3(0.52f, 0.06f, 0.52f), Mats.White);
            Mats.Part(h, PrimitiveType.Cube, "Brim", new Vector3(0, 0.25f, 0.28f), new Vector3(0.5f, 0.03f, 0.25f), Mats.White);
            Mats.Part(h, PrimitiveType.Cube, "Lamp", new Vector3(0, 0.2f, 0.3f), new Vector3(0.14f, 0.1f, 0.05f), Mats.Tube);

            var lampGo = new GameObject("Headlamp");
            lampGo.transform.SetParent(h, false);
            lampGo.transform.localPosition = new Vector3(0, 0.2f, 0.35f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Spot; lamp.range = 16f; lamp.spotAngle = 52f; lamp.intensity = 3f;
            lamp.color = new Color(0.85f, 1f, 0.9f); lamp.shadows = LightShadows.None;

            // physical presence (thrown food can hit him; the player can't walk through him)
            var cap = g.AddComponent<CapsuleCollider>();
            cap.radius = 0.5f; cap.height = 2.9f; cap.center = new Vector3(0, 1.45f, 0);
            var rb = g.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var ins = g.AddComponent<Inspector>();
            ins.L = lvl;
            ins.runSpeed = 5f + speedBonus;
            ins.walkSpeed = 2.2f + speedBonus * 0.3f;
            ins.head = h; ins.body = bodyT; ins.lLegPivot = lp; ins.rLegPivot = rp; ins.lArmPivot = la; ins.lamp = lamp;
            ins.hum = ProcAudio.Loop(g, ProcAudio.InspectorHum, 0.7f, true, 22f);
            ins.stunT = 1.5f; // grace period at shift start
            ins.state = S.Stunned;
            return ins;
        }

        // ---------------- public API ----------------
        public void Stun(float seconds, bool fromHit)
        {
            stunT = Mathf.Max(stunT, seconds);
            state = S.Stunned;
            route.Clear();
        }

        public void SlipOnGrease()
        {
            if (slipCool > 0f || state == S.Stunned) return;
            slipCool = 5f;
            Stun(2.5f, false);
            ProcAudio.PlayAt(ProcAudio.Slip, transform.position, 1f);
            var gm = GameManager.I;
            if (gm != null) gm.Toast("The Inspector slipped on grease!");
        }

        public void SendAway(Vector3 pos)
        {
            transform.position = pos;
            route.Clear();
            state = S.Stunned;
            stunT = 4f;
        }

        public void Hear(Vector3 pos, float radius)
        {
            if (state == S.Chase || state == S.Stunned) return;
            float dx = pos.x - transform.position.x, dz = pos.z - transform.position.z;
            if (dx * dx + dz * dz > radius * radius) return;
            lastKnown = pos;
            SetRoute(L.CellOf(pos), pos);
            state = S.Investigate;
        }

        // ---------------- brain ----------------
        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            if (hum != null) hum.volume = gm.Playing ? 0.7f : 0f;
            if (!gm.Playing) return;
            float dt = Time.deltaTime;
            var p = gm.Player;
            if (slipCool > 0f) slipCool -= dt;

            if (state == S.Stunned)
            {
                stunT -= dt;
                body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 20f) * 6f);
                lamp.intensity = Random.value < 0.5f ? 0.3f : 3f;
                if (stunT <= 0f) { state = S.Patrol; route.Clear(); }
                return;
            }
            if (p == null) return;

            senseT -= dt;
            if (senseT <= 0f)
            {
                senseT = 0.12f;
                if (CanSee(p))
                {
                    lastKnown = p.transform.position;
                    lastSeen = Time.time;
                    if (state != S.Chase)
                    {
                        state = S.Chase;
                        route.Clear();
                        gm.Toast("THE HEALTH INSPECTOR HAS SPOTTED YOU!");
                        ProcAudio.PlayAt(ProcAudio.Buzzer, transform.position, 0.8f);
                    }
                }
            }

            float speed = walkSpeed;
            switch (state)
            {
                case S.Patrol:
                    if (route.Count == 0 || ri >= route.Count) PickPatrol();
                    if (FollowRoute(walkSpeed, dt)) route.Clear();
                    break;

                case S.Investigate:
                    if (FollowRoute(walkSpeed * 1.2f, dt)) { state = S.Search; searchT = 3f; }
                    speed = walkSpeed * 1.2f;
                    break;

                case S.Chase:
                    speed = runSpeed;
                    if (Time.time - lastSeen < 0.4f)
                    {
                        MoveToward(p.transform.position, runSpeed, dt);
                        route.Clear();
                    }
                    else if (Time.time - lastSeen < 6f)
                    {
                        repathT -= dt;
                        if (route.Count == 0 || repathT <= 0f)
                        {
                            repathT = 0.5f;
                            SetRoute(L.CellOf(Time.time - lastSeen < 2.5f ? p.transform.position : lastKnown), Time.time - lastSeen < 2.5f ? p.transform.position : lastKnown);
                        }
                        FollowRoute(runSpeed, dt);
                    }
                    else { state = S.Search; searchT = 3.5f; route.Clear(); }

                    // citation!
                    Vector3 d = p.transform.position - transform.position; d.y = 0f;
                    if (d.magnitude < 1.5f && Time.time - lastSeen < 0.5f) gm.Citation(this);
                    break;

                case S.Search:
                    searchT -= dt;
                    transform.Rotate(0, 80f * dt, 0);
                    if (searchT <= 0f) { state = S.Patrol; route.Clear(); }
                    break;
            }

            Animate(dt, speed);
        }

        void PickPatrol()
        {
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(rng.Next(L.W), rng.Next(L.H));
                if (c == new Vector2Int(0, 0)) continue;
                var here = L.CellOf(transform.position);
                if (Mathf.Abs(c.x - here.x) + Mathf.Abs(c.y - here.y) < 2) continue;
                SetRoute(c, L.Center(c.x, c.y));
                return;
            }
            SetRoute(new Vector2Int(rng.Next(L.W), rng.Next(L.H)), transform.position);
        }

        bool CanSee(PlayerController p)
        {
            Vector3 e = head.position;
            Vector3 t = p.transform.position + Vector3.up * 1.2f;
            Vector3 d = t - e;
            float dist = d.magnitude;
            float range = 15f + (p.IsSprinting ? 4f : 0f) + (GameManager.I.Flashlight != null && GameManager.I.Flashlight.enabled ? 4f : 0f);
            if (dist > range) return false;
            Vector3 flat = new Vector3(d.x, 0, d.z);
            if (dist > 3.5f && Vector3.Angle(transform.forward, flat) > 70f) return false;
            if (Physics.Linecast(e, t, LevelBuilder.WallMask, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        void SetRoute(Vector2Int goal, Vector3 exactEnd)
        {
            route.Clear();
            ri = 0;
            var cells = L.Path(L.CellOf(transform.position), goal);
            Vector3 first = L.Center(cells[0].x, cells[0].y);
            Vector3 flatMe = new Vector3(transform.position.x, 0, transform.position.z);
            if (cells.Count == 1 || (flatMe - first).magnitude > 0.9f) route.Add(first);
            for (int i = 1; i < cells.Count; i++) route.Add(L.Center(cells[i].x, cells[i].y));
            if (cells.Count == 1) route.Add(new Vector3(exactEnd.x, 0, exactEnd.z));
        }

        bool FollowRoute(float spd, float dt)
        {
            if (route.Count == 0 || ri >= route.Count) return true;
            if (MoveToward(route[ri], spd, dt)) ri++;
            return ri >= route.Count;
        }

        bool MoveToward(Vector3 target, float spd, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 d = new Vector3(target.x - pos.x, 0, target.z - pos.z);
            float dist = d.magnitude;
            if (dist < 0.35f) return true;
            Vector3 dir = d / dist;
            transform.position = pos + dir * Mathf.Min(spd * dt, dist);
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 280f * dt);
            return false;
        }

        void Animate(float dt, float speed)
        {
            anim += dt * speed * 2.2f;
            float sw = Mathf.Sin(anim) * (state == S.Chase ? 40f : 24f);
            lLegPivot.localRotation = Quaternion.Euler(sw, 0, 0);
            rLegPivot.localRotation = Quaternion.Euler(-sw, 0, 0);
            lArmPivot.localRotation = Quaternion.Euler(-sw * 1.2f, 0, 0);
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(anim)) * 0.12f, 0);
            body.localRotation = Quaternion.Euler(state == S.Chase ? 12f : 0f, 0, Mathf.Sin(anim) * 3f);
            head.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 1.3f) * (state == S.Search ? 40f : 8f), Mathf.Sin(Time.time * 0.7f) * 5f);

            if (state == S.Chase)
            {
                lamp.color = new Color(1f, 0.15f, 0.1f);
                lamp.intensity = 3.5f + Mathf.Sin(Time.time * 30f);
            }
            else
            {
                lamp.color = new Color(0.85f, 1f, 0.9f);
                lamp.intensity = 3f;
            }

            stepT -= dt;
            if (stepT <= 0f)
            {
                stepT = state == S.Chase ? 0.34f : 0.7f;
                ProcAudio.PlayAt(ProcAudio.Thump, transform.position, 0.9f);
            }
        }
    }
}
