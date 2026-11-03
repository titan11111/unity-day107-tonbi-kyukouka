using UnityEngine;

namespace Tonbi
{
    public enum AKind { Cat, Dog }

    /// <summary>
    /// Beach cat / guard dog. Wander -> chase when the bird is low -> pounce (bite if the bird is still low). Dogs bark and alert nearby humans.
    /// Cat: fast, small, short reach, flexible gait (tail + leg swing). Dog: bigger chase radius/height, longer reach, bark range 70 m.
    /// </summary>
    public class AnimalAI : MonoBehaviour
    {
        public AKind kind = AKind.Cat; public Transform[] legs; public Transform tail;
        float chaseY, chaseD, atkY, atkD, spd, cd, barkD;
        enum M { Wander, Idle, Chase, Pounce } M m = M.Wander;
        float mt, cool, yaw, vy, gait, barkT, hitCool; bool bit; Vector3 jumpDir;

        void Awake()
        {
            if (kind == AKind.Cat) { chaseY = 10; chaseD = 32; atkY = 5; atkD = 4.4f; spd = 16; cd = 2.6f; barkD = 0; }
            else { chaseY = 18; chaseD = 48; atkY = 6.5f; atkD = 5.8f; spd = 12.5f; cd = 2.2f; barkD = 70; }
            yaw = Random.value * 6.28f;
        }

        void Update()
        {
            var gm = GameManager.Instance; if (gm == null || gm.bird == null) return;
            float dt = gm.SimDt; bool live = gm.playing; Vector3 p = transform.position, bp = gm.bird.transform.position;
            float dx = bp.x - p.x, dz = bp.z - p.z, hd = Mathf.Sqrt(dx * dx + dz * dz), toB = Mathf.Atan2(dx, dz);
            cool = Mathf.Max(0, cool - dt); mt -= dt; barkT = Mathf.Max(0, barkT - dt);
            bool threat = live && bp.y < chaseY && hd < chaseD;
            float move = 0;

            switch (m)
            {
                case M.Wander:
                    if (mt <= 0) { mt = Random.Range(1.2f, 3f); yaw += Random.Range(-1.6f, 1.6f); if (Random.value < .25f) { m = M.Idle; mt = Random.Range(1f, 2.5f); break; } }
                    move = kind == AKind.Cat ? 3.2f : 3.8f; if (threat && cool <= 0) { m = M.Chase; }
                    break;
                case M.Idle:
                    if (mt <= 0) m = M.Wander; if (threat && cool <= 0) m = M.Chase; break;
                case M.Chase:
                    yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, toB * Mathf.Rad2Deg, Mathf.Min(1, 9 * dt)) * Mathf.Deg2Rad; move = spd;
                    if (!threat) { m = M.Wander; mt = 0.5f; }
                    else if (hd < atkD * 1.5f && bp.y < atkY + 2) { m = M.Pounce; mt = 0.55f; vy = kind == AKind.Cat ? 8f : 7f; bit = false; jumpDir = new Vector3(Mathf.Sin(toB), 0, Mathf.Cos(toB)); }
                    break;
                case M.Pounce:
                    p += jumpDir * (spd * 1.25f * dt); vy -= 22f * dt; p.y += vy * dt;
                    if (!bit && live && gm.bird.invuln <= 0 && hd < atkD && bp.y < atkY + p.y) { bit = true; gm.HitBird(kind == AKind.Cat ? "猫に襲われた！" : "犬に噛まれた！", "低空は危険"); }
                    if (p.y <= 0) { p.y = 0; m = M.Wander; cool = cd; mt = 0.8f; yaw = toB + Mathf.PI; }
                    break;
            }
            if (move > 0) { p += new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * (move * dt); gait += dt * (4f + move * 1.1f); }
            if (m != M.Pounce) p.y = 0;
            if (p.x > 165 || p.x < -165 || p.z < 14 || p.z > 146) { yaw = Mathf.Atan2(-p.x, 80 - p.z); p.x = Mathf.Clamp(p.x, -165, 165); p.z = Mathf.Clamp(p.z, 14, 146); }
            transform.position = p; transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);

            // gait: leg groups swing in a gallop, tail waves, spine bob
            float amp = m == M.Chase ? 0.9f : m == M.Wander ? 0.5f : 0.05f;
            if (legs != null) for (int i = 0; i < legs.Length; i++) if (legs[i]) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(gait + (i < 2 ? 0f : 1.2f) + (i % 2) * 0.35f) * amp * 55f, 0, 0);
            if (tail) tail.localRotation = Quaternion.Euler(-35 + Mathf.Sin(Time.time * (m == M.Chase ? 14 : 3)) * 12, Mathf.Sin(Time.time * 3) * 18, 0);

            if (kind == AKind.Dog && live && hd < barkD && bp.y < 40)
            {
                if (barkT <= 0) { barkT = 2.4f; gm.Toast("ワン！ワン！", "犬が吠えて人が気づく"); }
                foreach (var h in gm.humans) if (h.state == HState.Walk && Vector3.Distance(h.transform.position, p) < 38f) h.awareness = Mathf.Min(0.95f, h.awareness + 0.5f * dt);
            }
        }
    }
}
