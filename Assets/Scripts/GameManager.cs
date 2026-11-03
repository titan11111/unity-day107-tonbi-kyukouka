using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    [System.Serializable] public struct FoodDef { public string name; public int pts; public Color color; }
    public enum Phase { Title, Play, Ending }

    /// <summary>Rules: title -> 90 s run (lock-on cone, grab detection, stealth bonus, combo, counter-attack, slow-mo snatch cinematic) -> ending.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public BirdController bird; public CameraRig cam;
        public List<HumanAI> humans = new List<HumanAI>();
        public Light sun;

        public Phase phase = Phase.Title; public float phaseT;
        public bool playing { get { return phase == Phase.Play; } }
        public int stage;                         // 0 = beach (night / festival come with the stage update)
        public float time = 90, elapsed, timeScaleSim = 1f;
        public int score, combo, maxCombo, count, stealthCount, rank; public float lastGrab = -99, topSpeed;
        public bool newRecord; public string endReason = "TIME UP!";
        public readonly List<int> stolen = new List<int>();
        public HumanAI lockTarget; public float lockDist = 1e9f;
        public string toast, toastSub; public float toastT;
        public string cutName; public Color cutColor; public float cutT; public string cutSub;
        public int best; public float heat, SimDt;
        bool calledTitle, stamped;

        public static readonly FoodDef[] Foods = {
            new FoodDef{name="ドーナツ", pts=100, color=new Color(1f,.5f,.69f)}, new FoodDef{name="ソフトクリーム", pts=150, color=new Color(.31f,.72f,.94f)},
            new FoodDef{name="チュロス", pts=130, color=new Color(.88f,.54f,.18f)}, new FoodDef{name="ポテト", pts=120, color=new Color(.91f,.66f,0f)},
            new FoodDef{name="クレープ", pts=140, color=new Color(1f,.56f,.42f)}, new FoodDef{name="たこ焼き", pts=110, color=new Color(.66f,.38f,.16f)} };

        void Awake() { Instance = this; best = PlayerPrefs.GetInt("tonbi_best", 0); }
        void Start() { if (bird) bird.OnImpact = HandleImpact; GoTitle(true); }

        void HandleImpact(string t, string s, float speed)
        {
            if (speed <= 0) { Toast(t, s); return; }
            cam.Shake(0.9f);
            if (speed > 34 && bird.invuln <= 0) HitBird(t, s); else Toast(t, "ガツン！");
        }

        public void GiveFood(HumanAI h)
        {
            h.foodIndex = Random.Range(0, Foods.Length);
            var f = h.foodAnchor.GetComponent<MeshRenderer>(); if (f) { f.enabled = true; f.material.color = Foods[h.foodIndex].color; }
        }
        void TakeFood(HumanAI h) { h.foodIndex = -1; var f = h.foodAnchor.GetComponent<MeshRenderer>(); if (f) f.enabled = false; }

        public void GoTitle(bool first = false)
        {
            phase = Phase.Title; phaseT = 0; calledTitle = false; heat = 0; lockTarget = null;
            foreach (var h in humans) { if (!h.HasFood) GiveFood(h); h.ResetHuman(); }
            bird.titlePerch = true; bird.autoTarget = false;
            bird.ResetBird(TonbiWorld.PerchPos); bird.perchK = 1f; bird.yaw = -0.25f;
            bird.titlePerch = true;
            if (first && cam) { cam.transform.position = TonbiWorld.PerchPos + new Vector3(-6.5f, 0.9f, 8.5f); }
            if (cam) { cam.Snap(); cam.timeScale = 1f; }
            TonbiMusic.SetScene(0);
        }

        public void StartGame()
        {
            heat = 0; score = combo = maxCombo = count = stealthCount = 0; time = 90; elapsed = 0; lastGrab = -99; lockTarget = null; topSpeed = 0; newRecord = false; stolen.Clear();
            phase = Phase.Play; phaseT = 0; stamped = false; stage = 0;
            bird.titlePerch = false; bird.autoTarget = false;
            bird.ResetBird(TonbiWorld.PerchPos + new Vector3(0f, 2.5f, 0f));
            bird.yaw = Mathf.PI; bird.pitch = -0.1f; bird.v = 34f; bird.vy = 8f; bird.perchK = 1f; bird.flapAnim = 1.2f;
            foreach (var h in humans) { if (!h.HasFood) GiveFood(h); h.ResetHuman(); }
            Vector3 fwd = BirdController.Forward(bird.yaw, bird.pitch);
            cam.transform.position = bird.transform.position - fwd * 12f + Vector3.up * 3f; cam.Snap();
            Toast("GO!", "気づかれる前に、うばえ！");
            TonbiMusic.SetScene(1); TonbiMusic.Sfx(5); TonbiMusic.Sfx(2);
        }

        void Update()
        {
            TonbiInput.Poll();
            if (TonbiInput.MutePressed) TonbiMusic.ToggleMute();
            float rdt = Mathf.Min(Time.deltaTime, 1 / 20f);
            phaseT += rdt;
            cam.UpdateCine(rdt);
            float dt = rdt * cam.timeScale; SimDt = dt; heat = Mathf.Max(0, heat - 0.04f * dt);
            toastT = Mathf.Max(0, toastT - rdt); cutT = Mathf.Max(0, cutT - rdt);

            if (phase == Phase.Play)
            {
                elapsed += dt; time -= dt; topSpeed = Mathf.Max(topSpeed, bird.v * 3.6f);
                if (time <= 0) { time = 0; endReason = "TIME UP!"; EndGame(); }
            }
            else if (phase == Phase.Title)
            {
                bird.titlePerch = true; bird.transform.position = TonbiWorld.PerchPos;
                if (!calledTitle && phaseT > 0.9f) { calledTitle = true; TonbiMusic.Sfx(5); }
                if (phaseT > 0.5f && TonbiInput.SubmitPressed) StartGame();
            }
            else if (phase == Phase.Ending)
            {
                bird.autoTarget = true; float a = Time.time * 0.25f;
                bird.autoPos = new Vector3(Mathf.Cos(a) * 70f, 46f, 60f + Mathf.Sin(a) * 70f);
                if (!stamped && phaseT > 1.6f) { stamped = true; TonbiMusic.Sfx(3); }
                if (phaseT > 3f && TonbiInput.SubmitPressed) StartGame();
                if (phaseT > 1f && TonbiInput.BackPressed) GoTitle();
            }

            if (playing) UpdateLock(); else lockTarget = null;
            bool inputOk = playing && phaseT > 0.4f;
            if (TonbiInput.FlapPressed && inputOk) bird.DoFlap();
            TargetInfo ti = lockTarget && lockTarget.HasFood ? new TargetInfo { valid = true, pos = lockTarget.FoodPos } : default(TargetInfo);
            var stick = inputOk ? TonbiInput.Stick : Vector2.zero;
            bool diveIn = inputOk && TonbiInput.Dive;
            bird.Step(dt, stick, diveIn, playing, ti);
            lockDist = ti.valid ? Vector3.Distance(ti.pos, bird.transform.position) : 1e9f;
            TonbiMusic.Flight(bird.diving ? 1f : 0f, Mathf.InverseLerp(16f, 76f, bird.v), playing);

            Vector3 sunDir = sun ? -sun.transform.forward : new Vector3(-0.45f, 0.62f, 0.45f).normalized;
            if (sunDir.y < 0.25f) { sunDir.y = 0.25f; sunDir.Normalize(); }
            float prog = Mathf.Min(1, elapsed / 110f);
            foreach (var h in humans) h.Tick(dt, bird, playing, prog, sunDir, diveIn, lockDist, h == lockTarget, OnSwing);

            if (playing) CheckGrabs();
            cam.UpdateCamera(rdt, bird, this);
        }

        void UpdateLock()
        {
            Vector3 f = BirdController.Forward(bird.yaw, bird.pitch), bp = bird.transform.position;
            HumanAI pick = null; float bs = 1e9f;
            foreach (var h in humans)
            {
                if (!h.HasFood) continue;
                Vector3 d = h.FoodPos - bp; float dist = d.magnitude; if (dist > 170 || dist < 1) continue;
                float dot = Vector3.Dot(d, f) / dist; if (dot < 0.5f) continue;
                float sc = dist * (2.3f - dot); if (sc < bs) { bs = sc; pick = h; }
            }
            bool keep = false;
            if (lockTarget && lockTarget.HasFood)
            {
                Vector3 d = lockTarget.FoodPos - bp; float dist = d.magnitude, dot = Vector3.Dot(d, f) / Mathf.Max(dist, 0.01f);
                if (dot > 0.3f && dist < 180 && (pick == null || dist * (2.3f - dot) < bs * 1.35f)) keep = true;
            }
            if (!keep) lockTarget = pick;
        }

        void CheckGrabs()
        {
            Vector3 a = bird.prevPos, b = bird.transform.position;
            foreach (var h in humans)
            {
                if (!h.HasFood) continue;
                if (SegDist(a, b, h.FoodPos) < 3.2f)
                {
                    if (h.state == HState.Guard) { if (bird.invuln <= 0) { Hit(h); break; } }
                    else Grab(h);
                }
            }
        }
        static float SegDist(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a; float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f); float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return Vector3.Distance(a + ab * t, p);
        }

        void Grab(HumanAI h)
        {
            var f = Foods[h.foodIndex]; bool clutch = h.state == HState.Dodge;
            float aw = h.awareness; string tag = ""; int sneak = 0;
            if (!clutch)
            {
                bool behind = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(bird.transform.position.x - h.transform.position.x, bird.transform.position.z - h.transform.position.z) * Mathf.Rad2Deg, h.Yaw * Mathf.Rad2Deg)) > 109f;
                if (aw < 0.2f) { sneak = 300; tag = " 気づかれず！"; stealthCount++; } else if (aw < 0.5f) { sneak = 120; tag = " スキあり"; }
                if (sneak > 0 && behind) { sneak += 80; tag += " 背後"; }
                if (sneak > 0 && h.act != HAct.Walk) { sneak += 60; tag += " " + HumanAI.ActJp[(int)h.act]; }
            }
            combo = (elapsed - lastGrab < 7) ? Mathf.Min(8, combo + 1) : 1; lastGrab = elapsed; maxCombo = Mathf.Max(maxCombo, combo);
            float dropH = Mathf.Max(0, bird.diveStartY - bird.transform.position.y);
            int bonus = (bird.diving && dropH > 45) ? Mathf.Min(300, Mathf.FloorToInt(dropH * 2)) : 0;
            int gain = (f.pts + Mathf.FloorToInt(bird.v * 2) + bonus) * combo + (clutch ? 200 : 0) + sneak;
            heat += 1f; score += gain; count++; time = Mathf.Min(99, time + 2); stolen.Add(h.foodIndex);
            bird.vy = Mathf.Max(bird.vy, 6); bird.v *= 0.92f;
            cutName = f.name; cutColor = f.color; cutT = 1.5f;
            cutSub = "+" + gain + (clutch ? " 必死に守ったのに！" : "") + tag + (bonus > 0 ? " 超急降下" : "") + (combo > 1 ? " ×" + combo : "");
            TakeFood(h); h.Stun(); cam.OnSnatch(h, bird); TonbiMusic.Sfx(0);
            foreach (var h2 in humans) if (h2 != h && Vector3.Distance(h2.transform.position, h.transform.position) < 24f) h2.ReactToNearbyTheft(f.name);
        }

        /// <summary>RivalBird beat the player to this snack: remove it the same way a player Grab() would, just with no score and its own toast.</summary>
        public void RivalSteal(HumanAI h)
        {
            if (!h.HasFood) return;
            var f = Foods[h.foodIndex];
            TakeFood(h); h.Stun();
            foreach (var h2 in humans) if (h2 != h && Vector3.Distance(h2.transform.position, h.transform.position) < 24f) h2.ReactToNearbyTheft(f.name);
            Toast("横取りされた！", "カラスに先を越された…");
        }

        void OnSwing(HumanAI h) { Hit(h); Toast("反撃された！", "気づかれた人には近づかない"); }
        void Hit(HumanAI h) { HitBird("はたかれた！", "「!!」の人は狙わない"); }
        public void HitBird(string t, string s)
        {
            bird.hp--; bird.invuln = 1.6f; bird.v = Mathf.Max(18, bird.v * 0.45f); bird.pitch = 0.7f; bird.vy = 10; combo = 0;
            Toast(t, s); cam.Shake(0.7f); TonbiMusic.Sfx(1);
            if (bird.hp <= 0) { endReason = "撃墜…"; EndGame(); }
        }
        public void Toast(string t, string s) { toast = t; toastSub = s; toastT = 1.1f; }

        void EndGame()
        {
            if (phase != Phase.Play) return;
            phase = Phase.Ending; phaseT = 0; stamped = false;
            newRecord = score > best && score > 0;
            if (score > best) { best = score; PlayerPrefs.SetInt("tonbi_best", best); }
            rank = score >= 8000 ? 3 : score >= 4500 ? 2 : score >= 2000 ? 1 : 0;   // 3=S 2=A 1=B 0=C
            bird.autoTarget = true; lockTarget = null;
            cam.SlowMo(1.3f); cam.Shake(0.4f);
            TonbiMusic.SetScene(4); TonbiMusic.Sfx(5);
        }
    }
}
