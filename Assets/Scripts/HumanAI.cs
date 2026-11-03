using UnityEngine;

namespace Tonbi
{
    public enum HState { Walk, Alert, Guard, Dodge, Duck, Stun }
    public enum HAct { Walk, Eat, Sea, Phone }
    public enum Personality { Angry, Kind, Cute, Noble, Humble, Dazai }

    /// <summary>
    /// Stealth AI port. Awareness (0..1) rises from: eyes (vision cone), ears (flight noise), shadow passing over.
    /// Activities (eat / sea / phone) divide eye-sensitivity => openings. Earbuds ignore noise.
    /// 0.3+ "?" suspicious, 0.7+ "!" alert, 1.0 guard (and counter-attacks when the bird comes within 7 m).
    /// </summary>
    public class HumanAI : MonoBehaviour
    {
        [Header("Refs")] public Transform foodAnchor; public TextMesh badge; public GameObject umbrella; public HumanRig rig;
        [Header("Config")] public float speed = 2f; public float height = 1f; public bool earbuds; public Personality personality;
        public Vector2 xRange = new Vector2(-165, 165), zRange = new Vector2(12, 146);

        [Header("State (read only)")]
        public HState state = HState.Walk; public HAct act = HAct.Walk;
        public float awareness; public int foodIndex = -1; public float stateT, actT, walkT, respawnT, jump;
        public bool swung; float seaYaw; float yaw;
        float reactT; string reactText; Color reactCol;

        // 性格別のリアクション/コメント(食べ物を盗まれるのを見たとき)。indexは Personality enum と対応。
        static readonly string[][] PersonaLines = {
            /*Angry : 怒りっぽい人*/ new[] { "はぁ!?ふざけんな!", "どこの鳥だ、許さんぞ!", "くそっ、やられた!", "おいコラ待て!", "ムカつく!!", "弁償しろー!" },
            /*Kind  : 優しい人*/     new[] { "あらあら、大変…", "お腹すいてたのかしらね", "大丈夫?びっくりしたでしょう", "まあまあ、しょうがないわ", "次は気をつけてね", "鳥さんも生きるのに必死なのよ" },
            /*Cute  : 可憐な人*/     new[] { "きゃあっ♡びっくりした〜", "うそぉ、かわいそう〜", "えー!?夢みたい…", "ドキドキしちゃった…", "きゃー鳥さん素敵…じゃなくて!", "えっ、今の何!?" },
            /*Noble : 高尚な人*/     new[] { "ふむ、自然の摂理ですな", "野生とは斯くも逞しきもの", "これもまた一興", "人の営みなど儚きもの", "実に興味深い光景だ", "弱肉強食、世の理というやつです" },
            /*Humble: 卑屈な人*/     new[] { "すみません、私が悪いんです…", "どうせ自分なんて…", "ああ、やっぱり…こんなもんですよね", "情けない話です…", "見てるだけですみません…", "私がもっと注意していれば…" },
            /*Dazai : 太宰な人(引用ではなく、この場面用に書いた思想寄りの台詞)*/
            new[] { "食べ物ひとつ、守れなかった。僕らしいと言えば、僕らしい。", "鳥に狙われる。それもまた、生まれ持った不運なのだろう。",
                    "奪われて初めて、持っていたことに気づく。いつもそうだ。", "笑ってくれて、構わない。こんな僕を。",
                    "期待していなかった。だから、傷ついてもいない……多分。", "ああ、また何かが、僕の手からこぼれ落ちていく。" },
        };
        public static readonly string[] TheftLinesWithFood = { "{0}取られてる!", "{0}やられた!", "{0}があ…", "{0}、持ってかれた!", "まさかの{0}…" };

        public bool HasFood => foodIndex >= 0;
        public Vector3 FoodPos => foodAnchor ? foodAnchor.position : transform.position + Vector3.up * 1.2f;
        public static readonly string[] ActJp = { "", "食べ歩き中", "海に夢中", "スマホ中" };
        static readonly float[] ActMul = { 1f, 0.55f, 0.35f, 0.2f };

        public void Init(Vector3 pos) { transform.position = pos; yaw = Random.value * 6.28f; actT = Random.Range(2f, 6f); speed = Random.Range(1.5f, 2.6f); }

        public void Tick(float dt, BirdController bird, bool playing, float progress, Vector3 sunDir, bool diveLocked, float lockDist, bool isLocked, System.Action<HumanAI> onSwing)
        {
            Vector3 bp = bird.transform.position, gp = transform.position;
            float dx = bp.x - gp.x, dz = bp.z - gp.z, hd = Mathf.Sqrt(dx * dx + dz * dz), toB = Mathf.Atan2(dx, dz);

            if (!HasFood && state != HState.Stun) { respawnT -= dt; if (respawnT <= 0) { GameManager.Instance.GiveFood(this); awareness = 0; act = HAct.Walk; actT = Random.Range(2f, 5f); } }

            // ---- awareness ----
            if (playing && HasFood && state != HState.Stun)
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(toB * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                float vis = off < 1.0f ? 1f : (off < 1.9f ? 0.25f : 0f);
                float elev = Mathf.Atan2(bp.y - 2.5f, Mathf.Max(hd, 1f));
                if (elev > 0.7f) vis *= 0.55f;               // people rarely look straight up
                vis *= WeatherSystem.Vis; if (WeatherSystem.Rain > 0.5f && elev > 0.45f) vis *= 0.3f;   // rain: umbrellas block the sky
                float mul = ActMul[(int)act];
                float d3 = Mathf.Sqrt(hd * hd + (bp.y - 2.5f) * (bp.y - 2.5f));
                float rate = vis * mul * Mathf.Clamp01(1 - d3 / 75f) * 1.15f;
                float noiseR = (8f + 0.4f * (bird.v - 16f) + (bird.diving ? 10f : 0f)) * WeatherSystem.Noise;
                if (!earbuds && d3 < noiseR) rate += (1 - d3 / noiseR) * (0.55f + 0.45f * mul);
                // shadow of the bird on the ground
                Vector2 sh = new Vector2(bp.x - sunDir.x / sunDir.y * bp.y, bp.z - sunDir.z / sunDir.y * bp.y);
                if (Vector2.Distance(sh, new Vector2(gp.x, gp.z)) < 2.4f && bp.y < 45 && WeatherSystem.Shadow > 0.5f) rate += 0.9f;
                rate *= 0.85f + 0.45f * progress;
                if (rate > 0.02f) awareness = Mathf.Min(1, awareness + rate * dt); else awareness = Mathf.Max(0, awareness - 0.3f * dt);
            }
            else if (state != HState.Guard) awareness = Mathf.Max(0, awareness - 0.5f * dt);

            // ---- reactions ----
            if ((state == HState.Walk || state == HState.Alert) && playing && HasFood && bird.diving && isLocked && lockDist < 18 && awareness > 0.55f && Random.value < dt * (2f + 2.5f * progress))
            { state = HState.Dodge; stateT = 0.85f; jump = 1; SetBadge("わっ", new Color(1, .6f, .2f)); }
            if (state == HState.Walk && playing && hd < 15 && bp.y < 15 && bird.v > 34 && awareness > 0.3f && Random.value < dt * 6) { state = HState.Duck; stateT = 0.9f; SetBadge(null, Color.white); }
            if (state == HState.Walk && awareness >= 0.7f) { state = HState.Alert; SetBadge("!", new Color(1, .7f, 0)); }
            if (state == HState.Alert && awareness >= 1f) { state = HState.Guard; stateT = 1.1f; swung = false; SetBadge("!!", Color.red); }
            if (state == HState.Alert && awareness < 0.45f) { state = HState.Walk; SetBadge(null, Color.white); }

            switch (state)
            {
                case HState.Walk:
                    actT -= dt;
                    if (actT <= 0)
                    {
                        float r = Random.value; act = r < 0.4f ? HAct.Walk : r < 0.58f ? HAct.Eat : r < 0.78f ? HAct.Sea : HAct.Phone;
                        actT = act == HAct.Walk ? Random.Range(2.5f, 5f) : Random.Range(3f, 6f);
                        if (act == HAct.Sea) seaYaw = Mathf.PI + Random.Range(-0.5f, 0.5f);   // sea is at -Z
                    }
                    bool sus = awareness > 0.3f;
                    if (act == HAct.Walk || sus)
                    {
                        walkT -= dt; if (walkT <= 0) { walkT = Random.Range(1.5f, 3.5f); yaw += Random.Range(-1.4f, 1.4f); }
                        float sp = speed * (sus ? 0.35f : 1f);
                        gp += new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * (sp * dt);
                        if (gp.x > xRange.y || gp.x < xRange.x || gp.z < zRange.x || gp.z > zRange.y) { yaw = Mathf.Atan2(-gp.x, 80 - gp.z); gp.x = Mathf.Clamp(gp.x, xRange.x, xRange.y); gp.z = Mathf.Clamp(gp.z, zRange.x, zRange.y); }
                    }
                    else if (act == HAct.Sea) yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, seaYaw * Mathf.Rad2Deg, Mathf.Min(1, 3 * dt)) * Mathf.Deg2Rad;
                    if (sus) { yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, toB * Mathf.Rad2Deg, Mathf.Min(1, (0.8f + 2.5f * awareness) * dt)) * Mathf.Deg2Rad; SetBadge("?", new Color(.35f, .66f, .9f)); }
                    else SetBadge(null, Color.white);
                    break;
                case HState.Alert:
                    yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, toB * Mathf.Rad2Deg, Mathf.Min(1, 10 * dt)) * Mathf.Deg2Rad; break;
                case HState.Guard:
                    yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, toB * Mathf.Rad2Deg, Mathf.Min(1, 10 * dt)) * Mathf.Deg2Rad;
                    stateT -= dt;
                    if (playing && !swung && bird.invuln <= 0 && Mathf.Sqrt(hd * hd + (bp.y - 2.4f) * (bp.y - 2.4f)) < 7f) { swung = true; onSwing?.Invoke(this); stateT = 0.5f; }
                    if (stateT <= 0) { state = HState.Walk; awareness = 0.5f; act = HAct.Walk; actT = Random.Range(3f, 5f); SetBadge(null, Color.white); }
                    break;
                case HState.Dodge:
                    stateT -= dt; jump = Mathf.Max(0, jump - dt * 1.4f); gp.y = Mathf.Sin(jump * Mathf.PI) * 0.7f;
                    yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, toB * Mathf.Rad2Deg, Mathf.Min(1, 10 * dt)) * Mathf.Deg2Rad;
                    if (stateT <= 0) { state = HState.Walk; gp.y = 0; awareness = 0.6f; act = HAct.Walk; actT = Random.Range(3f, 5f); SetBadge(null, Color.white); }
                    break;
                case HState.Duck:
                    stateT -= dt; transform.localScale = new Vector3(1, Mathf.Lerp(transform.localScale.y, 0.82f, Mathf.Min(1, 10 * dt)), 1) * height;
                    if (stateT <= 0) { state = HState.Walk; transform.localScale = Vector3.one * height; }
                    break;
                case HState.Stun:
                    stateT -= dt; jump = Mathf.Max(0, jump - dt * 1.6f); gp.y = Mathf.Sin(jump * Mathf.PI) * 0.9f;
                    if (stateT <= 0) { state = HState.Walk; gp.y = 0; act = HAct.Walk; actT = Random.Range(3f, 5f); SetBadge(null, Color.white); }
                    break;
            }
            transform.position = gp; transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            if (rig) rig.Animate(dt, state, act);
            if (umbrella && umbrella.activeSelf != (WeatherSystem.Rain > 0.5f)) umbrella.SetActive(WeatherSystem.Rain > 0.5f);
            if (reactT > 0) { reactT -= dt; SetBadge(reactText, reactCol); }   // bystander comment overrides the normal badge for a moment
            if (badge && badge.gameObject.activeSelf && Camera.main) badge.transform.rotation = Camera.main.transform.rotation;
        }

        /// <summary>Nearby bystanders react/comment when they see someone else's food get snatched. foodName empty/null => generic-only lines.</summary>
        public void ReactToNearbyTheft(string foodName = null)
        {
            if (state == HState.Stun) return;   // the victim already has their own "あっ" badge
            var lines = PersonaLines[(int)personality];
            reactText = (!string.IsNullOrEmpty(foodName) && Random.value < 0.5f)
                ? string.Format(TheftLinesWithFood[Random.Range(0, TheftLinesWithFood.Length)], foodName)
                : lines[Random.Range(0, lines.Length)];
            reactCol = new Color(1f, .78f, .35f); reactT = 1.4f;
        }

        public void Stun() { state = HState.Stun; stateT = 0.9f; jump = 1; respawnT = Random.Range(5f, 7.5f); SetBadge("あっ", Color.yellow); }
        public void ResetHuman() { state = HState.Walk; awareness = 0; act = HAct.Walk; actT = Random.Range(2f, 5f); transform.localScale = Vector3.one * height; SetBadge(null, Color.white); }
        public float Yaw => yaw;

        void SetBadge(string t, Color c)
        {
            if (!badge) return;
            if (string.IsNullOrEmpty(t)) { if (badge.gameObject.activeSelf) badge.gameObject.SetActive(false); return; }
            if (!badge.gameObject.activeSelf) badge.gameObject.SetActive(true);
            badge.text = t; badge.color = c;
        }
    }
}
