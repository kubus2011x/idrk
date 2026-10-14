using System.Collections.Generic;
using UnityEngine;

namespace ShiftMeal
{
    /// <summary>
    /// First-person controller: momentum-based movement, sprint/stamina, weight-based slowdown,
    /// pick up / drop / throw cargo, slipping on grease, and noise that attracts the Inspector.
    /// Legacy Input Manager only (Input.GetKey / GetAxis).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float Capacity = 12f;

        public readonly List<Cargo> Carried = new List<Cargo>();
        public string Prompt = "";
        public bool IsSprinting;
        public float Stamina = 1f;
        public float Invuln;

        CharacterController cc;
        Transform head;
        float yaw, pitch, roll, velY, slipT, slipCool, bobT, stepDist, leakT, shakeT;
        Vector3 hv, slideV;
        bool grounded, exhausted, warnedLeak;

        public float TotalWeight
        {
            get { float w = 0f; for (int i = 0; i < Carried.Count; i++) w += Carried[i].weight; return w; }
        }

        public void Setup(Camera cam, float startYaw)
        {
            cc = GetComponent<CharacterController>();
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0, 1.6f, 0);
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            yaw = startYaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Invuln = 2f;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing) { Prompt = ""; return; }
            float dt = Time.deltaTime;
            if (Invuln > 0f) Invuln -= dt;
            if (slipCool > 0f) slipCool -= dt;

            Look();
            Move(dt);
            Interact(gm);
            SauceLeak(gm, dt);

            if (Input.GetKeyDown(KeyCode.F) && gm.Flashlight != null) gm.Flashlight.enabled = !gm.Flashlight.enabled;
        }

        // ---------------- look ----------------
        void Look()
        {
            const float sens = 1.6f;
            yaw += Input.GetAxis("Mouse X") * sens;
            pitch -= Input.GetAxis("Mouse Y") * sens;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            float targetRoll = slipT > 0f ? Mathf.Sin(Time.time * 9f) * 18f : 0f;
            roll = Mathf.Lerp(roll, targetRoll, Time.deltaTime * 8f);
            head.localRotation = Quaternion.Euler(pitch, 0, roll);
        }

        // ---------------- movement ----------------
        void Move(float dt)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(h, 0, v);
            if (input.sqrMagnitude > 1f) input.Normalize();

            float weight = TotalWeight;
            float weightFactor = 1f / (1f + 0.07f * weight);

            // stamina
            bool wantSprint = Input.GetKey(KeyCode.LeftShift) && v > 0.1f && weight <= 10f && !exhausted && slipT <= 0f;
            IsSprinting = wantSprint && grounded;
            if (IsSprinting) Stamina -= dt * 0.22f; else Stamina += dt * 0.14f;
            Stamina = Mathf.Clamp01(Stamina);
            if (Stamina <= 0f) exhausted = true;
            if (exhausted && Stamina > 0.3f) exhausted = false;

            float speed = (IsSprinting ? 7.5f : 4.5f) * weightFactor;

            if (slipT > 0f)
            {
                slipT -= dt;
                slideV = Vector3.MoveTowards(slideV, Vector3.zero, 2.4f * dt);
                hv = slideV;
            }
            else
            {
                Vector3 wish = transform.TransformDirection(input) * speed;
                float accel = grounded ? 32f : 6f;
                hv = Vector3.MoveTowards(hv, wish, accel * dt);
            }

            if (grounded && velY < 0f) velY = -2f;
            if (grounded && slipT <= 0f && Input.GetKeyDown(KeyCode.Space))
            {
                velY = 6f * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(weight / Capacity));
                GameManager.I.MakeNoise(transform.position, 6f);
            }
            float prevVelY = velY;
            velY -= 18f * dt;

            Vector3 motion = hv + Vector3.up * velY;
            bool wasGrounded = grounded;
            grounded = (cc.Move(motion * dt) & CollisionFlags.Below) != 0;
            if (!wasGrounded && grounded && prevVelY < -9f)
            {
                GameManager.I.MakeNoise(transform.position, 11f);
                ProcAudio.PlayAt(ProcAudio.Thump, transform.position, 0.7f, false);
                shakeT = 0.25f;
            }

            // footsteps (also noise)
            float hs = new Vector3(hv.x, 0, hv.z).magnitude;
            if (grounded && hs > 0.6f && slipT <= 0f)
            {
                stepDist += hs * dt;
                float stride = IsSprinting ? 2.0f : 2.5f;
                if (stepDist > stride)
                {
                    stepDist = 0f;
                    ProcAudio.PlayAt(ProcAudio.Step, transform.position, IsSprinting ? 0.55f : 0.3f, false, Random.Range(0.9f, 1.1f));
                    float noiseR = IsSprinting ? 13f : 4.5f + weight * 0.25f;
                    GameManager.I.MakeNoise(transform.position, noiseR);
                }
            }

            // head bob
            bobT += hs * dt * 1.7f;
            float bob = grounded ? Mathf.Sin(bobT * 2f) * 0.045f * Mathf.Clamp01(hs / 4f) : 0f;
            float shake = shakeT > 0f ? (Random.value - 0.5f) * 0.1f : 0f;
            if (shakeT > 0f) shakeT -= dt;
            head.localPosition = new Vector3(shake, 1.6f + bob, 0);
        }

        // ---------------- interaction ----------------
        void Interact(GameManager gm)
        {
            Prompt = "";
            RaycastHit hit;
            if (Physics.Raycast(head.position, head.forward, out hit, 3.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var cargo = hit.collider.GetComponentInParent<Cargo>();
                if (cargo != null && !cargo.carried)
                {
                    bool fits = TotalWeight + cargo.weight <= Capacity;
                    Prompt = fits
                        ? "[E] Pick up " + cargo.label + "  ($" + cargo.value + ", " + cargo.weight + "kg)"
                        : cargo.label + " is too heavy! (" + cargo.weight + "kg) Drop something first.";
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        if (fits) Pickup(cargo);
                        else gm.Toast("Too heavy! Drop something first (Q).");
                    }
                }
                else if (hit.collider.GetComponentInParent<DeliveryTerminal>() != null)
                {
                    int total = 0;
                    for (int i = 0; i < Carried.Count; i++) total += Carried[i].value;
                    if (Carried.Count > 0) Prompt = "[E] Deliver " + Carried.Count + " item(s) for $" + total;
                    else Prompt = "Delivery terminal. Bring cargo here, or toss it on the green chute pad.";
                    if (gm.QuotaMet) Prompt += "\n[R] CLOCK OUT NOW (bonus pay for time left)";
                    if (Input.GetKeyDown(KeyCode.E) && Carried.Count > 0) DeliverAll(gm);
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        if (gm.QuotaMet) gm.ClockOut(); else gm.Toast("Quota not met. Management says no.");
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Q) && Carried.Count > 0)
            {
                if (Input.GetKey(KeyCode.LeftShift)) { while (Carried.Count > 0) DropCargo(Carried[Carried.Count - 1], 1.5f); }
                else DropCargo(Carried[Carried.Count - 1], 1.5f);
            }
            if (Input.GetMouseButtonDown(0) && Carried.Count > 0)
                DropCargo(Carried[Carried.Count - 1], Mathf.Lerp(15f, 7f, Mathf.Clamp01(Carried[Carried.Count - 1].weight / 8f)));
        }

        void Pickup(Cargo c)
        {
            Carried.Add(c);
            c.carried = true;
            c.gameObject.SetActive(false);
            ProcAudio.PlayAt(ProcAudio.Pickup, transform.position, 0.7f, false);
            GameManager.I.MakeNoise(transform.position, 3f);
        }

        void DeliverAll(GameManager gm)
        {
            var list = new List<Cargo>(Carried);
            Carried.Clear();
            gm.DeliverMany(list);
        }

        /// <summary>Drops (speed ~1.5) or throws (speed 7-15) a carried item in front of the camera.</summary>
        public void DropCargo(Cargo c, float speed)
        {
            if (!Carried.Remove(c)) return;
            Vector3 target = head.position + head.forward * 0.9f + Vector3.down * 0.3f;
            RaycastHit h;
            if (Physics.Linecast(head.position, target, out h, LevelBuilder.WallMask, QueryTriggerInteraction.Ignore))
                target = h.point - head.forward * 0.25f;
            c.transform.position = target;
            c.carried = false;
            c.gameObject.SetActive(true);
            if (c.rb != null)
            {
                c.rb.linearVelocity = head.forward * speed + new Vector3(hv.x, 0, hv.z) * 0.5f + Vector3.up * (speed > 4f ? 1.5f : 0f);
                c.rb.angularVelocity = Random.insideUnitSphere * 4f;
            }
            ProcAudio.PlayAt(ProcAudio.Drop, target, 0.8f);
            GameManager.I.MakeNoise(transform.position, 5f + c.weight);
        }

        // ---------------- comedy ----------------
        public void Slip()
        {
            if (slipT > 0f || slipCool > 0f || !grounded) return;
            slipT = 1.5f;
            slipCool = 3.5f;
            Vector3 dir = new Vector3(hv.x, 0, hv.z);
            float sp = Mathf.Max(dir.magnitude, 4.5f) * 1.25f;
            dir = dir.sqrMagnitude > 0.1f ? dir.normalized : transform.forward;
            slideV = dir * sp;
            velY = 3.5f;
            ProcAudio.PlayAt(ProcAudio.Slip, transform.position, 1f, false);
            GameManager.I.MakeNoise(transform.position, 16f);
            if (Carried.Count > 0 && Random.value < 0.6f)
            {
                var c = Carried[Carried.Count - 1];
                DropCargo(c, 4f);
                GameManager.I.Toast("WET FLOOR! You dropped your " + c.label + ".");
            }
            else GameManager.I.Toast("WET FLOOR! (it was not water)");
        }

        void SauceLeak(GameManager gm, float dt)
        {
            bool has = false;
            for (int i = 0; i < Carried.Count; i++) if (Carried[i].kind == CargoKind.Sauce) { has = true; break; }
            if (!has) return;
            if (!warnedLeak) { warnedLeak = true; gm.Toast("The Mystery Sauce Crate is leaking behind you..."); }
            leakT += dt;
            if (leakT > 3.5f && grounded)
            {
                leakT = 0f;
                Vector3 behind = transform.position - transform.forward * 1.4f;
                GreaseSpill.Spawn(gm.worldRoot, behind, 0.65f, 25f);
            }
        }

        public void Confiscate()
        {
            for (int i = 0; i < Carried.Count; i++) if (Carried[i] != null) Destroy(Carried[i].gameObject);
            Carried.Clear();
        }

        public void Teleport(Vector3 pos, float newYaw)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            yaw = newYaw;
            hv = Vector3.zero; slideV = Vector3.zero; velY = 0f; slipT = 0f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
            float push = Mathf.Max(new Vector3(hv.x, 0, hv.z).magnitude * 0.9f, 1.2f) / (1f + rb.mass * 0.08f);
            Vector3 dir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            rb.linearVelocity = new Vector3(dir.x * push, rb.linearVelocity.y, dir.z * push);
        }
    }
}
