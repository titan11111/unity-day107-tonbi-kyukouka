using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// Weather = difficulty + the time-of-day look. Static values are read by HumanAI (vision / noise / shadow) and BirdController (wind drift).
    /// The environment (sky, sun, fog, ambient, window glow) follows the game phase: day beach / golden-hour ending (night + festival presets are ready for the stage update).
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance;
        public static float Vis = 1, Noise = 1, Shadow = 1, Rain, Wind, WindDir, Dark;
        public static string Label = "☀ 晴れ";
        public static Vector3 WindVec { get { return new Vector3(Mathf.Sin(WindDir), 0, Mathf.Cos(WindDir)) * Wind; } }

        [System.Serializable]
        public struct W { public string name; public float vis, noise, shadow, rain, wind, dark, fogNear, fogFar, sunI, ambI; public Color fog; }
        public W[] table = {
            new W{ name="☀ 晴れ",  vis=1f,   noise=1f,   shadow=1f,  rain=0, wind=0.5f, dark=0f,   fogNear=260, fogFar=1600, sunI=1.2f, ambI=1f,   fog=new Color(.8f,.9f,.95f) },
            new W{ name="☁ 曇り",  vis=0.95f,noise=1f,   shadow=0.2f,rain=0, wind=2f,   dark=0.35f,fogNear=200, fogFar=1100, sunI=0.7f, ambI=0.95f,fog=new Color(.72f,.77f,.82f) },
            new W{ name="🌬 強風", vis=1f,   noise=0.65f,shadow=1f,  rain=0, wind=8f,   dark=0.1f, fogNear=240, fogFar=1400, sunI=1.1f, ambI=1f,   fog=new Color(.78f,.88f,.93f) },
            new W{ name="☔ 雨",    vis=0.7f, noise=0.55f,shadow=0.1f,rain=1, wind=3f,   dark=0.75f,fogNear=90,  fogFar=620,  sunI=0.45f,ambI=0.8f, fog=new Color(.45f,.52f,.58f) } };
        public Light sun; public int current; ParticleSystem rain; float t, next = 35, windBase;
        int stateIdx;

        // ---- time-of-day environment presets ----
        struct Env { public Color top, hor, bot, sunCol, amb; public Vector3 sunDir; public float sunI, night, glow; }
        static Env Day() { Env e = new Env(); e.top = new Color(.20f, .48f, .88f); e.hor = new Color(.78f, .90f, .97f); e.bot = new Color(.55f, .72f, .80f); e.sunCol = new Color(1f, .95f, .82f); e.amb = new Color(.62f, .70f, .78f); e.sunDir = new Vector3(-0.45f, 0.62f, 0.45f).normalized; e.sunI = 1.2f; e.night = 0f; e.glow = 0f; return e; }
        static Env Night() { Env e = new Env(); e.top = new Color(.02f, .04f, .14f); e.hor = new Color(.08f, .12f, .28f); e.bot = new Color(.03f, .05f, .10f); e.sunCol = new Color(.6f, .7f, 1f); e.amb = new Color(.12f, .16f, .30f); e.sunDir = new Vector3(-0.3f, 0.55f, 0.5f).normalized; e.sunI = 0.35f; e.night = 1f; e.glow = 1f; return e; }
        static Env Festival() { Env e = new Env(); e.top = new Color(.18f, .14f, .42f); e.hor = new Color(1f, .55f, .35f); e.bot = new Color(.4f, .25f, .3f); e.sunCol = new Color(1f, .6f, .35f); e.amb = new Color(.55f, .4f, .5f); e.sunDir = new Vector3(-0.75f, 0.30f, 0.45f).normalized; e.sunI = 0.9f; e.night = 0.35f; e.glow = 0.85f; return e; }
        static Env Golden() { Env e = new Env(); e.top = new Color(.30f, .25f, .55f); e.hor = new Color(1f, .70f, .40f); e.bot = new Color(.5f, .35f, .3f); e.sunCol = new Color(1f, .7f, .4f); e.amb = new Color(.6f, .48f, .5f); e.sunDir = new Vector3(-0.8f, 0.24f, 0.35f).normalized; e.sunI = 1.0f; e.night = 0.1f; e.glow = 0.5f; return e; }
        static Env Lerp(Env a, Env b, float k)
        {
            Env e = new Env(); e.top = Color.Lerp(a.top, b.top, k); e.hor = Color.Lerp(a.hor, b.hor, k); e.bot = Color.Lerp(a.bot, b.bot, k);
            e.sunCol = Color.Lerp(a.sunCol, b.sunCol, k); e.amb = Color.Lerp(a.amb, b.amb, k); e.sunDir = Vector3.Slerp(a.sunDir, b.sunDir, k);
            e.sunI = Mathf.Lerp(a.sunI, b.sunI, k); e.night = Mathf.Lerp(a.night, b.night, k); e.glow = Mathf.Lerp(a.glow, b.glow, k); return e;
        }
        Env cur; bool curInit; public static float Glow, Flash;

        void Awake() { Instance = this; BuildRain(); cur = Day(); curInit = true; }

        void BuildRain()
        {
            var go = new GameObject("Rain"); rain = go.AddComponent<ParticleSystem>();
            var main = rain.main; main.loop = true; main.startLifetime = 0.9f; main.startSpeed = 0; main.maxParticles = 1500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = 0.06f; main.startColor = new Color(.85f, .9f, 1f, .5f);
            var sh = rain.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70, 1, 70);
            var vel = rain.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(-55f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var em = rain.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 6; r.velocityScale = 0.04f;
            r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        Env Target(GameManager gm)
        {
            if (gm == null) return Day();
            if (gm.phase == Phase.Ending) return Golden();
            switch (gm.stage) { case 1: return Night(); case 2: return Festival(); default: return Day(); }
        }

        void Update()
        {
            var gm = GameManager.Instance; float dt = gm ? gm.SimDt : Time.deltaTime; bool live = gm && gm.playing;
            if (live)
            {
                t += dt;
                if (t >= next)
                {
                    t = 0; next = Random.Range(25f, 45f); float prog = Mathf.Min(1, gm.elapsed / 90f);
                    float rr = Random.value; int n = rr < 0.3f ? 0 : rr < 0.5f + 0.1f * prog ? 1 : rr < 0.75f ? 2 : 3;
                    if (n == stateIdx) n = (n + 1) % table.Length; stateIdx = n;
                }
            }
            else { stateIdx = 0; t = 0; }
            var w = table[stateIdx]; float k = Mathf.Min(1, dt * 0.7f + 0.002f);
            Vis += (w.vis - Vis) * k; Noise += (w.noise - Noise) * k; Shadow += (w.shadow - Shadow) * k; Rain += (w.rain - Rain) * k; Dark += (w.dark - Dark) * k;
            windBase += (w.wind - windBase) * k; Wind = windBase * (0.75f + 0.25f * Mathf.Sin(Time.time * 0.6f)); WindDir += dt * 0.03f; Label = w.name;

            // environment (real-time smoothing so title / ending transitions are always smooth)
            float rk = 1f - Mathf.Exp(-Time.deltaTime * 1.1f);
            cur = Lerp(cur, Target(gm), rk);
            float grey = Mathf.Clamp01(Dark * 0.8f);
            Color greyC = new Color(.5f, .55f, .6f) * Mathf.Lerp(1f, 0.35f, cur.night);
            Color top = Color.Lerp(cur.top, greyC, grey), hor = Color.Lerp(cur.hor, greyC * 1.1f, grey), bot = Color.Lerp(cur.bot, greyC * 0.9f, grey);
            Flash = Mathf.Max(0f, Flash - Time.deltaTime * 3f);
            Glow = cur.glow; Shader.SetGlobalFloat("_TonbiGlow", Glow);
            if (SkyDome.Instance) SkyDome.Instance.Apply(top, hor, bot, cur.sunDir, cur.sunCol * (1f - grey * 0.8f), cur.night);
            RenderSettings.fogColor = hor;
            RenderSettings.fogStartDistance += (w.fogNear - RenderSettings.fogStartDistance) * k;
            RenderSettings.fogEndDistance += (w.fogFar - RenderSettings.fogEndDistance) * k;
            RenderSettings.ambientLight = cur.amb * (1f + Flash);
            if (sun)
            {
                sun.color = cur.sunCol;
                sun.intensity = cur.sunI * Mathf.Lerp(1f, w.sunI / 1.2f, 0.9f) + Flash * 1.5f;
                sun.transform.rotation = Quaternion.LookRotation(-cur.sunDir);
            }
            if (Camera.main) { Camera.main.backgroundColor = hor; rain.transform.position = Camera.main.transform.position + Vector3.up * 28; }
            var em = rain.emission; em.rateOverTime = 1100f * Rain;
        }
    }
}
