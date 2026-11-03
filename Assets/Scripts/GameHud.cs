using UnityEngine;

namespace Tonbi
{
    /// <summary>IMGUI HUD + exciting title screen + ending (rank stamp, count-up, confetti). Logic reads GameManager only.</summary>
    public class GameHud : MonoBehaviour
    {
        GameManager gm; Camera cam; GUIStyle st; Texture2D px; float u = 1f;
        VigilanteManager vman;
        static readonly Color Gold = new Color(1f, .82f, .25f), Coral = new Color(1f, .35f, .28f);
        static readonly string[] RankName = { "C", "B", "A", "S" };
        static readonly Color[] RankCol = { new Color(.62f, .68f, .75f), new Color(.35f, .7f, 1f), new Color(1f, .45f, .35f), new Color(1f, .85f, .2f) };

        void Start()
        {
            gm = GameManager.Instance; cam = Camera.main;
            px = new Texture2D(1, 1); px.SetPixel(0, 0, Color.white); px.Apply();
        }

        // ---------------- helpers ----------------
        void Box(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, px); GUI.color = o; }
        void Txt(Rect r, string t, float size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool shadow = true)
        {
            if (st == null) { st = new GUIStyle(GUI.skin.label); st.richText = true; st.wordWrap = false; st.font = TonbiFont.Get(); }
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * u)); st.alignment = a; st.fontStyle = FontStyle.Bold;
            if (shadow) { st.normal.textColor = new Color(0, 0, 0, .65f); float d = Mathf.Max(1.5f, size * u * 0.05f); GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), t, st); }
            st.normal.textColor = c; GUI.Label(r, t, st);
        }
        void Outlined(Rect r, string t, float size, Color c, Color outline)
        {
            float d = Mathf.Max(2f, size * u * 0.045f);
            if (st == null) Txt(r, "", 10, c);
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * u)); st.alignment = TextAnchor.MiddleCenter; st.fontStyle = FontStyle.Bold; st.normal.textColor = outline;
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; GUI.Label(new Rect(r.x + Mathf.Cos(a) * d, r.y + Mathf.Sin(a) * d, r.width, r.height), t, st); }
            st.normal.textColor = c; GUI.Label(r, t, st);
        }
        static float EaseOutBack(float t) { t = Mathf.Clamp01(t); float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        bool Pill(Rect r, string label, Color c, float size)
        {
            Box(new Rect(r.x - 4 * u, r.y - 4 * u, r.width + 8 * u, r.height + 8 * u), new Color(0, 0, 0, .35f));
            Box(r, c); Box(new Rect(r.x, r.y, r.width, r.height * 0.5f), new Color(1, 1, 1, .18f));
            Txt(r, label, size, Color.white);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        void Letterbox(float k) { float h = Screen.height * 0.11f * k; Box(new Rect(0, 0, Screen.width, h), Color.black); Box(new Rect(0, Screen.height - h, Screen.width, h), Color.black); }
        void MuteButton()
        {
            Rect r = new Rect(Screen.width - 150 * u, 12 * u, 138 * u, 44 * u);
            Box(r, new Color(0, 0, 0, .35f));
            Txt(r, TonbiMusic.Muted ? "♪ OFF (M)" : "♪ ON (M)", 20, Color.white);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) TonbiMusic.ToggleMute();
        }

        bool VigilanteManagerActive() { if (!vman) vman = Object.FindFirstObjectByType<VigilanteManager>(); return vman && vman.ActiveCount > 0; }

        // ---------------- main ----------------
        void OnGUI()
        {
            if (gm == null) return;
            float W = Screen.width, H = Screen.height;
            u = Mathf.Min(W / 540f, H / 960f);
            if (gm.phase == Phase.Title) { DrawTitle(W, H); return; }
            if (gm.phase == Phase.Ending) { DrawEnding(W, H); return; }
            DrawPlay(W, H);
        }

        // ---------------- TITLE ----------------
        void DrawTitle(float W, float H)
        {
            float T = gm.phaseT, tm = Time.time;
            // gradient vignette for readability
            for (int i = 0; i < 10; i++)
            {
                float a = (1f - i / 10f) * 0.45f; float h = H * 0.05f;
                Box(new Rect(0, i * h * 0.6f, W, h * 0.6f), new Color(0.02f, 0.05f, 0.12f, a));
                Box(new Rect(0, H - (i + 1) * h * 0.6f, W, h * 0.6f), new Color(0.02f, 0.05f, 0.12f, a * 0.9f));
            }
            // speed lines bursting from the logo
            Vector2 c = new Vector2(W * 0.5f, H * 0.30f);
            Matrix4x4 m0 = GUI.matrix;
            for (int i = 0; i < 28; i++)
            {
                float seed = i * 12.9898f; float ang = i * (360f / 28f) + Mathf.Sin(seed) * 6f;
                float ph = Mathf.Repeat(tm * 0.9f + (Mathf.Sin(seed * 3.1f) * 0.5f + 0.5f), 1f);
                float r0 = Mathf.Lerp(80f, 520f, ph) * u, len = Mathf.Lerp(30f, 180f, ph) * u;
                GUI.matrix = m0; GUIUtility.RotateAroundPivot(ang, c);
                Box(new Rect(c.x + r0, c.y - 1.5f * u, len, 3f * u), new Color(1f, 1f, 1f, (1f - ph) * 0.35f));
            }
            GUI.matrix = m0;

            // logo slam-in: "トンビ" / "急降下"
            float k = Mathf.Clamp01(T / 0.5f); float sc = 1f + (1f - k) * (1f - k) * 2.4f;
            float shake = T < 0.7f ? (0.7f - T) * 14f * u : 0f;
            GUI.matrix = m0; GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), c); GUIUtility.RotateAroundPivot(-4f, c);
            Outlined(new Rect(0 + Random.Range(-shake, shake), c.y - 130 * u, W, 150 * u), "トンビ", 120, Color.white, new Color(.10f, .22f, .45f));
            float pulse = 1f + Mathf.Sin(tm * 3.2f) * 0.025f;
            GUI.matrix = m0; GUIUtility.ScaleAroundPivot(new Vector2(sc * pulse, sc * pulse), c + new Vector2(0, 90 * u)); GUIUtility.RotateAroundPivot(-4f, c);
            Outlined(new Rect(0, c.y - 10 * u, W, 200 * u), "急降下", 170, new Color(1f, .42f, .22f), new Color(.25f, .05f, .02f));
            GUI.matrix = m0;
            float tag = Mathf.Clamp01((T - 0.5f) / 0.5f);
            Txt(new Rect(0, c.y + 190 * u, W, 44 * u), "空の掃除屋、参上。", 30, new Color(Gold.r, Gold.g, Gold.b, tag));
            Txt(new Rect(0, c.y + 232 * u, W, 30 * u), "S H O N A N   S N A T C H", 18, new Color(1, 1, 1, .8f * tag));

            // food ticker
            float ty = H * 0.60f; float off = Mathf.Repeat(tm * 70f * u, 220f * u);
            for (int i = -1; i < 7; i++)
            {
                int fi = ((i % 6) + 6) % 6; var f = GameManager.Foods[fi];
                float x = i * 220f * u - off + W * 0.0f;
                Box(new Rect(x + 6 * u, ty - 22 * u, 28 * u, 28 * u), f.color);
                Txt(new Rect(x + 40 * u, ty - 24 * u, 170 * u, 32 * u), f.name, 22, new Color(1, 1, 1, .9f), TextAnchor.MiddleLeft);
            }

            // START
            float bs = 1f + Mathf.Sin(tm * 4.5f) * 0.04f; float bw = 360f * u * bs, bh = 96f * u * bs;
            Rect br = new Rect(W * 0.5f - bw / 2, H * 0.70f - bh / 2, bw, bh);
            if (T > 0.6f && Pill(br, "▶ スタート", Coral, 44)) { if (gm.phaseT > 0.5f) gm.StartGame(); }
            Txt(new Rect(0, H * 0.70f + 66 * u, W, 30 * u), "TAP / SPACE", 20, new Color(1, 1, 1, .6f + 0.4f * Mathf.Sin(tm * 5f)));

            Txt(new Rect(0, H * 0.83f, W, 36 * u), "BEST  " + gm.best, 28, Gold);
            Txt(new Rect(0, H * 0.88f, W, 28 * u), "左ドラッグ＝旋回・上昇　右＝急降下(長押し)/羽ばたき", 18, new Color(1, 1, 1, .8f));
            Txt(new Rect(0, H * 0.91f, W, 28 * u), "PC：WASD/矢印　Space＝急降下　F＝羽ばたき", 18, new Color(1, 1, 1, .8f));
            MuteButton();
            if (T < 0.7f) Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 1f - T / 0.7f));
        }

        // ---------------- ENDING ----------------
        void DrawEnding(float W, float H)
        {
            float T = gm.phaseT, tm = Time.time;
            Letterbox(Mathf.Clamp01(T / 0.8f));
            MuteButton();
            if (T < 1.7f)
            {
                float k = EaseOutBack(T / 0.4f); float sh = Mathf.Max(0f, 0.8f - T) * 18f * u;
                GUI.matrix = Matrix4x4.identity; GUIUtility.ScaleAroundPivot(new Vector2(k, k), new Vector2(W / 2, H * 0.42f));
                Outlined(new Rect(Random.Range(-sh, sh), H * 0.42f - 90 * u, W, 180 * u), gm.endReason, 110, Color.white, new Color(.7f, .15f, .1f));
                GUI.matrix = Matrix4x4.identity;
                return;
            }
            float P = T - 1.7f;
            // dim panel
            float pa = Mathf.Clamp01(P / 0.5f);
            Rect panel = new Rect(W * 0.07f, H * 0.12f, W * 0.86f, H * 0.78f);
            Box(panel, new Color(0.05f, 0.04f, 0.1f, 0.62f * pa));
            Box(new Rect(panel.x, panel.y, panel.width, 6 * u), new Color(Gold.r, Gold.g, Gold.b, pa));
            Txt(new Rect(panel.x, panel.y + 18 * u, panel.width, 40 * u), "R E S U L T", 26, new Color(1, 1, 1, .85f * pa));

            // rank stamp
            float sk = Mathf.Clamp01(P / 0.35f); float ss = 1f + (1f - sk) * 3.2f;
            Vector2 sc = new Vector2(W * 0.5f, H * 0.30f);
            GUI.matrix = Matrix4x4.identity; GUIUtility.ScaleAroundPivot(new Vector2(ss, ss), sc); GUIUtility.RotateAroundPivot(-12f, sc);
            Color rc = RankCol[Mathf.Clamp(gm.rank, 0, 3)]; rc.a = sk;
            Outlined(new Rect(W * 0.5f - 200 * u, sc.y - 150 * u, 400 * u, 300 * u), RankName[Mathf.Clamp(gm.rank, 0, 3)], 300, rc, new Color(.12f, .08f, .2f, sk));
            GUI.matrix = Matrix4x4.identity;

            // score count-up
            float ck = Mathf.Clamp01((P - 0.5f) / 1.5f); int shown = Mathf.RoundToInt(gm.score * (1f - Mathf.Pow(1f - ck, 3f)));
            Txt(new Rect(panel.x, H * 0.45f, panel.width, 40 * u), "SCORE", 24, new Color(1, 1, 1, .7f));
            Txt(new Rect(panel.x, H * 0.45f + 34 * u, panel.width, 90 * u), shown.ToString(), 76, Gold);
            if (gm.newRecord && ck >= 1f && Mathf.Repeat(tm * 2.5f, 1f) < 0.7f) Txt(new Rect(panel.x, H * 0.45f + 112 * u, panel.width, 40 * u), "★ NEW RECORD ★", 28, new Color(1f, .5f, .6f));
            else if (!gm.newRecord) Txt(new Rect(panel.x, H * 0.45f + 112 * u, panel.width, 40 * u), "BEST " + gm.best, 22, new Color(1, 1, 1, .7f));

            // stats (staggered)
            string[] lab = { "ゲット", "ステルス成功", "最大コンボ", "最高速度" };
            string[] val = { gm.count + " 個", gm.stealthCount + " 回", "×" + gm.maxCombo, Mathf.RoundToInt(gm.topSpeed) + " km/h" };
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.Clamp01((P - 1.6f - i * 0.18f) / 0.3f); if (a <= 0f) continue;
                float y = H * 0.45f + 170 * u + i * 40 * u;
                Txt(new Rect(panel.x + 30 * u, y, panel.width * 0.55f, 36 * u), lab[i], 24, new Color(1, 1, 1, .8f * a), TextAnchor.MiddleLeft);
                Txt(new Rect(panel.x + panel.width * 0.45f, y, panel.width * 0.5f - 30 * u, 36 * u), val[i], 26, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleRight);
            }
            // diary-style closing lines
            string[] msg1 = { "人間たちの勝ち…次は気づかれずに。", "まだまだ腹ペコ。もう一周！", "夕焼けの空へ、悠々と帰還。", "今日もこの空を守り切った。" };
            string[] msg2 = { "空腹のまま、ネグラへ帰る。", "お腹いっぱいには、あと少し。", "見事な盗みっぷり！", "湘南の空は、おまえのものだ。" };
            float ma = Mathf.Clamp01((P - 2.6f) / 0.5f);
            Txt(new Rect(panel.x, H * 0.45f + 345 * u, panel.width, 36 * u), msg1[Mathf.Clamp(gm.rank, 0, 3)], 26, new Color(1, 1, 1, ma));
            Txt(new Rect(panel.x, H * 0.45f + 380 * u, panel.width, 30 * u), msg2[Mathf.Clamp(gm.rank, 0, 3)], 20, new Color(Gold.r, Gold.g, Gold.b, ma));
            Txt(new Rect(panel.x, H * 0.45f + 412 * u, panel.width, 28 * u), "明日もいい風が吹きますように。", 18, new Color(.8f, .9f, 1f, ma * .85f));

            // buttons
            if (P > 3.0f)
            {
                float bw = 300 * u, bh = 78 * u;
                if (Pill(new Rect(W * 0.5f - bw / 2, H * 0.45f + 468 * u, bw, bh), "もう一回", Coral, 38)) gm.StartGame();
                if (Pill(new Rect(W * 0.5f - bw * 0.4f, H * 0.45f + 468 * u + bh + 18 * u, bw * 0.8f, bh * 0.7f), "タイトルへ", new Color(.25f, .45f, .75f), 28)) gm.GoTitle();
            }

            // confetti for A / S
            if (gm.rank >= 2)
            {
                for (int i = 0; i < 60; i++)
                {
                    float seed = i * 7.31f; float x = (Mathf.Sin(seed) * 0.5f + 0.5f) * W; float sp = 0.25f + (Mathf.Sin(seed * 2.3f) * 0.5f + 0.5f) * 0.35f;
                    float y = Mathf.Repeat(tm * sp * H * 0.6f + seed * 40f, H * 1.1f) - H * 0.05f;
                    Color c = Color.HSVToRGB(Mathf.Repeat(seed * 0.13f, 1f), .6f, 1f); c.a = 0.9f;
                    Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(tm * 200f + seed * 30f, new Vector2(x, y));
                    Box(new Rect(x, y, 9 * u, 15 * u), c); GUI.matrix = m;
                }
            }
        }

        // ---------------- PLAY ----------------
        void DrawPlay(float W, float H)
        {
            if (cam == null) cam = Camera.main;
            Txt(new Rect(10 * u, 8 * u, 360 * u, 60 * u), "SCORE " + gm.score, 34, Gold, TextAnchor.MiddleLeft);
            Txt(new Rect(W / 2 - 80 * u, 6 * u, 160 * u, 70 * u), Mathf.CeilToInt(gm.time).ToString(), 56, gm.time < 10 ? new Color(1, .35f, .3f) : Color.white);
            Txt(new Rect(W - 260 * u, 8 * u, 250 * u, 60 * u), new string('♥', Mathf.Max(0, gm.bird.hp)), 34, new Color(1, .35f, .3f), TextAnchor.MiddleRight);
            Txt(new Rect(W / 2 - 180 * u, 74 * u, 360 * u, 34 * u), WeatherSystem.Label + "  風" + WeatherSystem.Wind.ToString("0") + "m" + (VigilanteManagerActive() ? "  自警団出動" : ""), 20, new Color(.8f, .9f, 1f));
            if (gm.combo > 1) Txt(new Rect(10 * u, 70 * u, 360 * u, 40 * u), "COMBO ×" + gm.combo, 26, new Color(1, .45f, .35f), TextAnchor.MiddleLeft);
            Txt(new Rect(10 * u, H - 56 * u, 520 * u, 36 * u), "ALT " + Mathf.RoundToInt(gm.bird.transform.position.y) + "m  SPD " + Mathf.RoundToInt(gm.bird.v * 3.6f) + "km/h  G " + gm.bird.gForce.ToString("0.0"), 20, new Color(1, 1, 1, .8f), TextAnchor.MiddleLeft);

            // lock-on reticle + awareness bar
            var L = gm.lockTarget;
            if (gm.playing && L && L.HasFood && cam)
            {
                Vector3 sp = cam.WorldToScreenPoint(L.FoodPos);
                if (sp.z > 0)
                {
                    float x = sp.x, y = H - sp.y, d = gm.lockDist, size = Mathf.Clamp(1400f / d, 18, 46) * (H / 900f);
                    Color col = L.state == HState.Guard ? new Color(1, .29f, .23f) : L.state == HState.Alert ? new Color(1, .66f, 0) : new Color(.49f, 1, .69f);
                    float t = 3f * (H / 900f);
                    Box(new Rect(x - size, y - size, size * 2, t), col); Box(new Rect(x - size, y + size, size * 2, t), col);
                    Box(new Rect(x - size, y - size, t, size * 2), col); Box(new Rect(x + size, y - size, t, size * 2 + t), col);
                    Txt(new Rect(x - 80, y + size + 4, 160, 30), Mathf.RoundToInt(d) + "m", 20, col);
                    float bw = 120 * (H / 900f), by = y + size + 36;
                    Box(new Rect(x - bw / 2 - 2, by - 2, bw + 4, 14), new Color(0, 0, 0, .55f));
                    Box(new Rect(x - bw / 2, by, bw * L.awareness, 10), L.awareness < 0.3f ? new Color(.49f, 1, .69f) : L.awareness < 0.7f ? new Color(1, .82f, .25f) : new Color(1, .29f, .23f));
                    string lab = (L.earbuds ? "イヤホン" : "") + (L.act != HAct.Walk ? (L.earbuds ? "・" : "") + HumanAI.ActJp[(int)L.act] : "");
                    if (lab.Length > 0 && L.awareness < 0.5f) Txt(new Rect(x - 150, by + 14, 300, 32), "隙あり：" + lab, 20, new Color(.49f, 1, .69f));
                }
            }
            if (gm.toastT > 0) { Txt(new Rect(0, H * 0.27f, W, 70 * u), gm.toast, 52, Color.white); Txt(new Rect(0, H * 0.27f + 60 * u, W, 40 * u), gm.toastSub, 22, Gold); }
            if (gm.cutT > 0)
            {
                Rect cr = new Rect(W * 0.1f, H * 0.17f, W * 0.8f, 90 * u);
                Box(cr, gm.cutColor); Txt(cr, gm.cutName, 52, Color.white);
                Txt(new Rect(0, H * 0.17f + 100 * u, W, 40 * u), gm.cutSub, 22, Gold);
            }
            if (WeatherSystem.Flash > 0.01f) Box(new Rect(0, 0, W, H), new Color(1, 1, 1, Mathf.Clamp01(WeatherSystem.Flash * 0.6f)));

            // touch UI
            if (TonbiInput.StickActive)
            {
                Vector2 o = new Vector2(TonbiInput.StickOrigin.x, H - TonbiInput.StickOrigin.y), p = new Vector2(TonbiInput.StickPos.x, H - TonbiInput.StickPos.y);
                Box(new Rect(o.x - 60, o.y - 60, 120, 120), new Color(1, 1, 1, .15f)); Box(new Rect(p.x - 26, p.y - 26, 52, 52), new Color(1, 1, 1, .8f));
            }
            Box(TonbiInput.DiveRect, TonbiInput.Dive ? new Color(.8f, .2f, .15f, .9f) : new Color(1, .35f, .28f, .8f));
            Txt(TonbiInput.DiveRect, "急降下\nホールド", 22, Color.white);
            Box(TonbiInput.FlapRect, gm.bird.flapCD > 0 ? new Color(1, 1, 1, .3f) : new Color(1, .85f, .3f, .85f));
            Txt(TonbiInput.FlapRect, "羽ばたき", 22, new Color(.1f, .15f, .2f), TextAnchor.MiddleCenter, false);
        }
    }
}
