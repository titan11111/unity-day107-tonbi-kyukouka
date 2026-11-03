using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// Port of the web prototype's flight model (stepBird).
    /// Convention: yaw = clockwise from +Z toward +X (Unity). Right stick => right bank => yaw increases.
    /// Speed range 16..76 m/s, energy exchange between pitch and speed, coordinated-turn yaw, dive-assist toward the locked target.
    /// </summary>
    public class BirdController : MonoBehaviour
    {
        [Header("Tuning (same values as the web prototype)")]
        public float minSpeed = 16f, maxSpeed = 76f, cruiseSpeed = 26f;
        public float diveAccel = 22f, gravityAlong = 30f, flapCooldown = 0.9f;
        public float turnK = 28f;           // yawRate = turnK * tan(roll) / max(v,16)
        public float bounds = 230f;

        [Header("State (read only)")]
        public float yaw = 0.9f, pitch = -0.12f, roll, v = 30f, vy;
        public float gForce = 1f, flapCD, invuln, diveStartY;
        public bool diving;
        public int hp = 3;
        public Vector3 prevPos;
        public Transform visual;            // child that gets pitch/roll applied (optional)
        public HawkModel model; public float flapAnim; public bool titlePerch;
        public bool autoTarget; public Vector3 autoPos;

        [Header("Perch / knockback (v2)")]
        public BuildingObstacle perch; public float perchT, perchCool, stun, perchK; public bool perchHealed; public Vector3 kb;
        /// <summary>(title, sub, impactSpeed). impactSpeed &lt; 0: info toast only. 0: toast. &gt;0: collision.</summary>
        public System.Action<string, string, float> OnImpact;
        Transform wingL, wingR, tailT; Vector3 wlP, wrP, wS, tailP;

        void Awake()
        {
            wingL = transform.Find("WingL"); wingR = transform.Find("WingR"); tailT = transform.Find("Tail");
            if (wingL) { wlP = wingL.localPosition; wS = wingL.localScale; } if (wingR) wrP = wingR.localPosition; if (tailT) tailP = tailT.localPosition;
        }

        void Reset() { visual = transform; }

        public void ResetBird(Vector3 pos)
        {
            transform.position = pos; yaw = 0.9f; pitch = -0.12f; roll = 0; v = 30; vy = 0; hp = 3; invuln = 0; flapCD = 0; diving = false;
            prevPos = pos; perch = null; perchK = 0; stun = 0; kb = Vector3.zero; perchCool = 0; ApplyTransform();
        }

        public static Vector3 Forward(float yaw, float pitch)
            => new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

        public void DoFlap()
        {
            if (perch != null) { Launch(false); return; }
            if (flapCD > 0) return;
            flapCD = flapCooldown; v = Mathf.Min(78f, v + 14f); vy = Mathf.Max(vy, 13f); flapAnim = 0.8f;
        }

        public void Step(float dt, Vector2 stick, bool diveHeld, bool live, TargetInfo lockTarget)
        {
            perchCool = Mathf.Max(0, perchCool - dt);
            flapAnim = Mathf.Max(0, flapAnim - dt);
            if (titlePerch) { perchK += (1f - perchK) * Mathf.Min(1, 7 * dt); v = 0; vy = 0; diving = false; pitch = 0; roll = 0; ApplyTransform(); return; }
            perchK += ((perch != null ? 1f : 0f) - perchK) * Mathf.Min(1, 7 * dt);
            if (perch != null && live) { StepPerch(dt, stick.x, diveHeld); return; }
            float sx = stick.x, sy = stick.y;
            if (stun > 0) { sx *= 0.15f; sy *= 0.15f; }
            if (!live)
            {
                sx = 0.3f; sy = Mathf.Clamp((62 - transform.position.y) * 0.03f, -0.35f, 0.35f);
                if (autoTarget)
                {
                    Vector3 to0 = autoPos - transform.position;
                    float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(to0.x, to0.z) * Mathf.Rad2Deg);
                    sx = Mathf.Clamp(dy * 0.04f, -1f, 1f); sy = Mathf.Clamp((autoPos.y - transform.position.y) * 0.03f, -0.4f, 0.4f);
                }
            }
            bool d = diveHeld && live && stun <= 0;
            if (d && !diving) diveStartY = transform.position.y;
            diving = d;
            flapCD = Mathf.Max(0, flapCD - dt);

            float tp = sy * 0.8f, ay = 0; bool locked = false;
            if (diving)
            {
                if (lockTarget.valid)
                {
                    Vector3 to = lockTarget.pos - transform.position;
                    float hd = Mathf.Max(0.001f, new Vector2(to.x, to.z).magnitude);
                    tp = Mathf.Clamp(Mathf.Atan2(to.y, hd), -1.3f, 0.25f);
                    ay += Mathf.Clamp(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad * 3.4f, -2.6f, 2.6f);
                    locked = true;
                }
                else tp = -0.8f + sy * 0.35f;
                v += diveAccel * dt;
            }
            Vector3 p = transform.position;
            if (!locked && p.y < 16 && tp < 0.1f) tp = Mathf.Max(tp, 0.12f * (1 - p.y / 16f) + 0.05f);
            if (p.y > 135) tp = Mathf.Min(tp, -0.15f);
            float outside = Mathf.Max(Mathf.Abs(p.x) - bounds, p.z - bounds, -170 - p.z);
            if (outside > 0 && live)
                ay += Mathf.Clamp(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(-p.x, 60 - p.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad * 2.6f, -2.2f, 2.2f);

            if (v < 19 && tp > 0) tp *= 0.4f;
            // right input / right turn (ay>0) => right bank (roll > 0)
            float rollTarget = Mathf.Clamp(sx * 1.05f + ay * 0.4f, -1.15f, 1.15f);
            roll += (rollTarget - roll) * Mathf.Min(1, 4.2f * dt);
            float yawRate = turnK * Mathf.Tan(roll) / Mathf.Max(v, 16f) + ay * 0.6f;
            float pp = pitch;
            pitch += (tp - pitch) * Mathf.Min(1, (diving ? 3.2f : 2.0f) * dt * 1.6f);
            pitch = Mathf.Clamp(pitch, -1.35f, 1.05f);
            float gT = 1f / Mathf.Cos(Mathf.Clamp(roll, -1.3f, 1.3f)) + v * ((pitch - pp) / Mathf.Max(dt, 1e-4f)) / 29f;
            gForce += (Mathf.Clamp(gT, -2, 8) - gForce) * Mathf.Min(1, 6 * dt);
            v += -Mathf.Sin(pitch) * gravityAlong * dt;
            v += (cruiseSpeed - v) * 0.6f * dt;
            v = Mathf.Clamp(v, minSpeed, maxSpeed);
            yaw += yawRate * dt;

            float th = Thermal.LiftAt(p);
            if (th > 0 && live) vy += 26f * th * (1 + 0.6f * Mathf.Min(1, Mathf.Abs(roll) / 0.8f)) * dt;

            prevPos = p;
            p += Forward(yaw, pitch) * (v * dt);
            p.y += vy * dt; vy *= Mathf.Exp(-3 * dt);
            if (live) p += WeatherSystem.WindVec * dt;   // wind drift
            if (p.y < 1.2f) { p.y = 1.2f; pitch = Mathf.Max(pitch, 0.04f); v *= 0.985f; }
            transform.position = p;
            if (live) { CollideBuildings(); if (perch != null) { ApplyTransform(); return; } CollideTrees(); }
            if (stun > 0) { stun -= dt; transform.position += kb * dt; kb *= Mathf.Exp(-4.5f * dt); }
            if (invuln > 0) invuln -= dt;
            ApplyTransform();
        }

        // ---------------- perch ----------------
        void StepPerch(float dt, float stickX, bool diveHeld)
        {
            perchT += dt; v = 0; vy = 0; diving = false; pitch = 0; roll = 0; yaw += stickX * 1.6f * dt;
            Vector3 p = transform.position, c = new Vector3(perch.cx, 0, perch.cz);
            p.x = Mathf.Lerp(p.x, c.x, Mathf.Min(1, 4 * dt)); p.z = Mathf.Lerp(p.z, c.z, Mathf.Min(1, 4 * dt)); p.y = perch.roof + 0.75f;
            transform.position = p; prevPos = p;
            if (perchT > 3f && !perchHealed) { perchHealed = true; if (hp < 3) { hp++; OnImpact?.Invoke("羽を休めた", "HP回復", -1); } }
            if (invuln > 0) invuln -= dt;
            if (diveHeld && perchT > 0.35f) Launch(true);
            ApplyTransform();
        }
        void Launch(bool ambush)
        {
            perch = null; perchCool = 1.2f; roll = 0;
            if (ambush) { v = 34; pitch = -0.7f; diving = true; diveStartY = transform.position.y; }
            else { v = 30; pitch = 0.35f; vy = 14; flapCD = flapCooldown; flapAnim = 0.8f; }
        }

        // ---------------- collisions ----------------
        void CollideBuildings()
        {
            Vector3 p = transform.position;
            foreach (var b in BuildingObstacle.All)
            {
                if (Mathf.Abs(p.x - b.cx) > b.hw + 0.6f || Mathf.Abs(p.z - b.cz) > b.hd + 0.6f) continue;
                if (p.y <= b.roof + 0.9f && prevPos.y >= b.roof - 0.3f && perchCool <= 0)
                { perch = b; perchT = 0; perchHealed = false; diving = false; v = 0; vy = 0; p.y = b.roof + 0.75f; transform.position = p; OnImpact?.Invoke("とまった！", "急降下＝奇襲　羽ばたき＝離陸", -1); return; }
                if (p.y < b.roof - 0.3f)
                {
                    float dx = p.x - b.cx, dz = p.z - b.cz, px = b.hw + 0.6f - Mathf.Abs(dx), pz = b.hd + 0.6f - Mathf.Abs(dz); Vector3 n;
                    if (px < pz) { n = new Vector3(Mathf.Sign(dx == 0 ? 1 : dx), 0, 0); p.x = b.cx + n.x * (b.hw + 1.2f); }
                    else { n = new Vector3(0, 0, Mathf.Sign(dz == 0 ? 1 : dz)); p.z = b.cz + n.z * (b.hd + 1.2f); }
                    transform.position = p; Knockback(n, "ビルに激突！", "壁にぶつかった"); return;
                }
            }
        }
        void CollideTrees()
        {
            Vector3 p = transform.position;
            foreach (var t in TreeObstacle.All)
            {
                var tp = t.transform.position;
                if (Mathf.Abs(p.x - tp.x) > t.r + 1.2f || Mathf.Abs(p.z - tp.z) > t.r + 1.2f || p.y > t.h + 0.5f) continue;
                float dx = p.x - tp.x, dz = p.z - tp.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < t.r + 0.8f)
                {
                    Vector3 n = d > 0.01f ? new Vector3(dx / d, 0, dz / d) : Vector3.right;
                    p.x = tp.x + n.x * (t.r + 1.4f); p.z = tp.z + n.z * (t.r + 1.4f); transform.position = p;
                    Knockback(n, "木に激突！", "枝でノックバック"); return;
                }
            }
        }
        void Knockback(Vector3 n, string title, string sub)
        {
            float sp = v, K = 16f + sp * 0.55f;
            kb = new Vector3(n.x, 0, n.z) * K; v = Mathf.Max(16f, sp * 0.3f); vy = 10f; pitch = 0.5f; diving = false; stun = 0.8f; roll = (Random.value < .5f ? -1 : 1) * 1.1f;
            OnImpact?.Invoke(title, sub, Mathf.Max(0.01f, sp));
        }

        void ApplyTransform()
        {
            float pk = perchK;   // perched pose: chest up, wings folded against the body, tail hangs down
            float pt = pitch * (1 - pk) + 0.62f * pk, yw = yaw + Mathf.Sin(Time.time * 0.7f) * 0.05f * pk;
            transform.rotation = Quaternion.Euler(-pt * Mathf.Rad2Deg, yw * Mathf.Rad2Deg, -roll * (1 - pk) * Mathf.Rad2Deg);
            if (model) { model.Pose(Time.deltaTime, pk, diving, roll * (1 - pk), flapAnim, stun > 0f); return; }
            if (wingL) { wingL.localPosition = Vector3.Lerp(wlP, new Vector3(-0.5f, -0.05f, -0.2f), pk); wingL.localScale = Vector3.Lerp(wS, new Vector3(0.15f, 0.7f, 1.6f), pk); }
            if (wingR) { wingR.localPosition = Vector3.Lerp(wrP, new Vector3(0.5f, -0.05f, -0.2f), pk); wingR.localScale = Vector3.Lerp(wS, new Vector3(0.15f, 0.7f, 1.6f), pk); }
            if (tailT) { tailT.localRotation = Quaternion.Euler(30f * pk, 0, 0); tailT.localPosition = tailP + new Vector3(0, -0.25f * pk, 0); }
        }
    }

    public struct TargetInfo { public bool valid; public Vector3 pos; }

    /// <summary>Thermal columns (Pilotwings-style soaring). Add this component to a GameObject; radius default 26 m.</summary>
    public class Thermal : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<Thermal> All = new System.Collections.Generic.List<Thermal>();
        public float radius = 26f, height = 110f;
        void OnEnable() { All.Add(this); } void OnDisable() { All.Remove(this); }
        public static float LiftAt(Vector3 p)
        {
            float best = 0;
            foreach (var t in All)
            {
                float r = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(t.transform.position.x, t.transform.position.z));
                if (r >= t.radius || p.y > t.height + 12) continue;
                float v = Mathf.Clamp01((1 - r / t.radius) * 1.8f);
                if (p.y > t.height) v *= Mathf.Clamp01(1 - (p.y - t.height) / 12f);
                best = Mathf.Max(best, v);
            }
            return best;
        }
    }
}
