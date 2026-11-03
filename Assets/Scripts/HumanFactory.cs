using UnityEngine;

namespace Tonbi
{
    /// <summary>OS font fallback so Japanese text shows in IMGUI / TextMesh without any font asset.</summary>
    public static class TonbiFont
    {
        static Font f;
        public static Font Get()
        {
            if (f == null)
            {
                string[] wanted = { "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Hiragino Sans W3", "Hiragino Sans W4", "Hiragino Kaku Gothic ProN W3",
                    "Yu Gothic UI", "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "PingFang SC", "Arial Unicode MS" };
                string[] installed = { };
                try { installed = Font.GetOSInstalledFontNames(); } catch { }

                var log = new System.Text.StringBuilder();
                log.AppendLine("installed(" + installed.Length + "): " + string.Join(", ", installed));

                // 1) installed名とwantedの完全一致を個別に試す(1個ずつ=落ちた名前だけ失敗ログ、他は巻き込まない)
                foreach (var w in wanted)
                {
                    bool present = false;
                    foreach (var i in installed) if (i == w) { present = true; break; }
                    if (!present) continue;
                    try { var t = Font.CreateDynamicFontFromOSFont(new[] { w }, 32); if (t != null) { f = t; log.AppendLine("OK: " + w); break; } log.AppendLine("NULL: " + w); }
                    catch (System.Exception e) { log.AppendLine("THROW: " + w + " -> " + e.Message); }
                }
                // 2) 何も当たらなければ、installed配列を先頭から総当たり(CJK名が別表記の場合の保険)
                if (f == null)
                {
                    foreach (var i in installed)
                    {
                        if (string.IsNullOrEmpty(i)) continue;
                        try { var t = Font.CreateDynamicFontFromOSFont(new[] { i }, 32); if (t != null) { f = t; log.AppendLine("FALLBACK-OK: " + i); break; } }
                        catch { }
                    }
                }
                try { System.IO.File.WriteAllText(Application.dataPath + "/../font_debug.log", log.ToString()); } catch { }

                if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return f;
        }
    }

    /// <summary>Limb / head animation for the procedural humans. Driven by HumanAI.Tick (or its own Update for vigilantes).</summary>
    public class HumanRig : MonoBehaviour
    {
        public Transform root, armLU, armLF, armRU, armRF, legL, legR, head, pony, handR;
        public bool female, autoAnimate;
        public int reactionKind = -1;               // >= 0: ending-theatre pose
        public float restArm = 8f;
        float phase, t, speedSm; Vector3 last; bool init;

        void Update() { if (autoAnimate) Animate(Time.deltaTime, HState.Walk, HAct.Walk); }

        static void Rot(Transform tr, float x, float y, float z) { if (tr) tr.localRotation = Quaternion.Euler(x, y, z); }

        public void Animate(float dt, HState st, HAct act)
        {
            if (dt <= 1e-5f || root == null) return;
            Vector3 pos = transform.position;
            if (!init) { last = pos; init = true; }
            float spd = new Vector2(pos.x - last.x, pos.z - last.z).magnitude / dt; last = pos;
            speedSm += (spd - speedSm) * Mathf.Min(1f, dt * 8f);
            float mv = Mathf.Clamp01(speedSm / 1.4f);
            phase += dt * Mathf.Lerp(2f, 7f, mv);
            t += dt;
            float sw = Mathf.Sin(phase) * mv;

            float lL = sw * 30f, lR = -sw * 30f;
            float auL = -sw * 26f, auR = sw * 26f, afL = -8f - 12f * mv, afR = -8f - 12f * mv;
            float zL = -restArm, zR = restArm;
            float hx = 0f, rootY = Mathf.Sin(t * 2f) * 0.004f, rootRx = 0f, rootRy = female ? sw * 7f : sw * 3f;

            if (reactionKind >= 0)
            {
                lL = lR = 0f; auL = auR = 0f; afL = afR = -5f; rootRy = 0f;
                switch (reactionKind)
                {
                    case 0: // 悔しがり: fists up, stomping
                        auL = -150f + Mathf.Sin(t * 12f) * 18f; auR = -150f - Mathf.Sin(t * 12f) * 18f; afL = afR = -40f;
                        lL = Mathf.Sin(t * 10f) * 22f; lR = -lL; hx = 10f; rootY = Mathf.Abs(Mathf.Sin(t * 10f)) * 0.05f; break;
                    case 1: // 号泣
                        auL = auR = -60f; afL = afR = -140f; hx = 30f + Mathf.Sin(t * 18f) * 4f; rootY = Mathf.Sin(t * 18f) * 0.01f; rootRx = 8f; break;
                    case 2: // へたり込み
                        lL = lR = -85f; auL = auR = 10f; hx = 35f; rootY = -0.5f; rootRx = 6f; break;
                    case 3: // あぜん
                        zL = -22f; zR = 22f; hx = -14f; rootY = Mathf.Sin(t * 30f) * 0.005f; break;
                    case 4: // 怒って指さし
                        auR = -100f + Mathf.Sin(t * 6f) * 8f; afR = -4f; auL = -20f; afL = -90f; rootRy = Mathf.Sin(t * 5f) * 5f; hx = -6f; break;
                    default: // 大笑い
                        auL = auR = -30f; afL = afR = -70f; rootRx = Mathf.Sin(t * 14f) * 8f; hx = -20f; rootY = Mathf.Abs(Mathf.Sin(t * 14f)) * 0.03f; break;
                }
            }
            else if (st == HState.Walk)
            {
                if (act == HAct.Eat) { auR = -35f; afR = -115f + 8f * Mathf.Sin(t * 5f); hx = 5f; }
                else if (act == HAct.Sea) { zL = -26f; zR = 26f; afL = afR = -45f; auL = auR = 0f; hx = -8f; }
                else if (act == HAct.Phone) { auL = auR = -40f; afL = afR = -95f; hx = 25f; zL = zR = 0f; }
            }
            else if (st == HState.Dodge) { auL = auR = -165f; afL = afR = -10f; lL = 18f; lR = -18f; }
            else if (st == HState.Guard) { auR = -150f + 40f * Mathf.Sin(t * 14f); afR = -30f; auL = -60f; afL = -90f; hx = -5f; }
            else if (st == HState.Stun) { auR = -150f + Mathf.Sin(t * 12f) * 25f; auL = -150f - Mathf.Sin(t * 12f) * 25f; rootRx = -10f; hx = -15f; }

            Rot(legL, lL, 0f, 0f); Rot(legR, lR, 0f, 0f);
            Rot(armLU, auL, 0f, zL); Rot(armRU, auR, 0f, zR);
            Rot(armLF, afL, 0f, 0f); Rot(armRF, afR, 0f, 0f);
            Rot(head, hx, 0f, 0f);
            Rot(root, rootRx, rootRy, 0f);
            Vector3 lp = root.localPosition; lp.y = rootY; root.localPosition = lp;
            if (pony) pony.localRotation = Quaternion.Euler(-12f - 14f * mv * Mathf.Abs(Mathf.Sin(phase)) + Mathf.Sin(t * 3f) * 3f, Mathf.Sin(phase * 0.5f) * 10f * mv, 0f);
        }
    }

    /// <summary>Builds muscular men (壮観) and curvy women in stylish swimwear from lofted vertex-colour meshes.</summary>
    public static class HumanFactory
    {
        static readonly Color[] Skin = {
            new Color(.96f, .80f, .66f), new Color(.90f, .69f, .52f), new Color(.80f, .58f, .41f), new Color(.65f, .44f, .30f), new Color(.47f, .31f, .21f) };
        static readonly Color[] Hair = {
            new Color(.07f, .05f, .04f), new Color(.30f, .17f, .10f), new Color(.82f, .66f, .38f), new Color(.48f, .20f, .10f), new Color(.95f, .55f, .72f) };
        static readonly Color[] Bikini = {
            new Color(1f, .35f, .38f), new Color(.98f, .98f, .98f), new Color(.10f, .78f, .80f), new Color(1f, .82f, .15f), new Color(.08f, .08f, .10f), new Color(1f, .25f, .62f), new Color(.85f, .1f, .12f) };
        static readonly Color[] Trunks = {
            new Color(.10f, .16f, .45f), new Color(.85f, .12f, .12f), new Color(.08f, .55f, .35f), new Color(1f, .55f, .10f), new Color(.07f, .07f, .09f), new Color(.95f, .95f, .95f), new Color(.1f, .6f, .85f) };
        const float S = 1.4f;

        static Ring R(float y, float a, float b, Color c) { return new Ring(new Vector3(0f, y, 0f), a, b, c); }
        static Color Sh(Color c, float m) { return new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), 1f); }
        static Transform Part(string name, Transform parent, Vector3 lp, MeshKit k, Material m)
        {
            GameObject go = MeshKit.Make(name, parent, k, m);
            go.transform.localPosition = lp;
            return go.transform;
        }

        public static HumanAI Create(Transform parent, bool female, int seed, bool vigilante = false, float bigness = 1f)
        {
            System.Random rng = new System.Random(seed);
            Color skin = Skin[rng.Next(Skin.Length)];
            Color hair = Hair[rng.NextDouble() < 0.12 ? 4 : rng.Next(4)];
            GameObject go = new GameObject(female ? "Woman" : (vigilante ? "Vigilante" : "Man"));
            if (parent) go.transform.SetParent(parent, false);
            HumanAI ai = go.AddComponent<HumanAI>();
            HumanRig rig = go.AddComponent<HumanRig>();
            rig.female = female;
            Transform model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);
            model.localScale = Vector3.one * (S * bigness);
            rig.root = model;
            Material lit = TonbiMat.Lit(0.28f);

            if (female) BuildWoman(model, rig, lit, rng, skin, hair);
            else BuildMan(model, rig, lit, rng, skin, hair, vigilante);

            // food in the right hand
            GameObject food = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            food.name = "Food";
            Object.Destroy(food.GetComponent<Collider>());
            food.transform.SetParent(rig.armRF, false);
            food.transform.localPosition = new Vector3(0f, female ? -0.30f : -0.36f, 0.04f);
            food.transform.localScale = Vector3.one * 0.24f;
            Material fm = TonbiMat.Plain(Color.white, 0.35f);
            food.GetComponent<MeshRenderer>().sharedMaterial = fm;
            ai.foodAnchor = food.transform;
            rig.handR = food.transform;

            // umbrella (shown in rain)
            MeshKit uk = new MeshKit();
            Color uc = Color.HSVToRGB((float)rng.NextDouble(), .6f, .95f);
            uk.Loft(new Ring[] { R(0.02f, .02f, .02f, uc), R(-0.05f, .5f, .5f, uc), R(-0.17f, .95f, .95f, uc), R(-0.30f, 1.1f, 1.1f, Sh(uc, .8f)) }, 0, 14, Matrix4x4.identity);
            uk.Loft(new Ring[] { R(-0.9f, .015f, .015f, new Color(.2f, .2f, .2f)), R(0.02f, .015f, .015f, new Color(.2f, .2f, .2f)) }, 0, 5, Matrix4x4.identity);
            GameObject umb = MeshKit.Make("Umbrella", model, uk, lit);
            umb.transform.localPosition = new Vector3(0f, female ? 2.4f : 2.6f, 0f);
            umb.SetActive(false);
            ai.umbrella = umb;

            // badge ("?" "!" ...)
            GameObject bg = new GameObject("Badge");
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, (female ? 3.35f : 3.65f) * bigness, 0f);
            TextMesh tm = bg.AddComponent<TextMesh>();
            tm.font = TonbiFont.Get();
            bg.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64; tm.characterSize = 0.12f; tm.anchor = TextAnchor.MiddleCenter; tm.fontStyle = FontStyle.Bold;
            bg.SetActive(false);
            ai.badge = tm;

            ai.rig = rig;
            rig.autoAnimate = vigilante;
            ai.earbuds = !vigilante && rng.NextDouble() < 0.25;
            return ai;
        }

        // ------------------------------------------------------------------ MAN
        static void BuildMan(Transform model, HumanRig rig, Material lit, System.Random rng, Color skin, Color hair, bool vig)
        {
            Color trunk = vig ? new Color(.10f, .14f, .30f) : Trunks[rng.Next(Trunks.Length)];
            Color band = vig ? new Color(.10f, .14f, .30f) : Sh(trunk, 1.35f);
            Color up = vig ? new Color(1f, .82f, .08f) : skin;
            MeshKit k = new MeshKit();
            Ring[] torso = new Ring[] {
                R(.80f, .17f, .11f, trunk), R(.90f, .20f, .12f, trunk), R(1.02f, .205f, .125f, trunk), R(1.04f, .195f, .12f, band), R(1.08f, .19f, .115f, band),
                R(1.081f, .185f, .11f, up), R(1.20f, .20f, .12f, up), R(1.34f, .25f, .135f, up), R(1.46f, .29f, .15f, up), R(1.56f, .31f, .145f, up),
                R(1.62f, .20f, .11f, up), R(1.68f, .075f, .075f, skin), R(1.76f, .07f, .07f, skin) };
            k.Loft(torso, 0, 16, Matrix4x4.identity);
            if (!vig)
            {
                Color pec = Sh(skin, 1.05f), seam = Sh(skin, .80f);
                for (int s = -1; s <= 1; s += 2)
                {
                    k.Ellipsoid(new Vector3(.125f * s, 1.45f, .115f), new Vector3(.12f, .08f, .06f), pec, Matrix4x4.identity, 5, 10);          // pecs
                    k.Ellipsoid(new Vector3(.10f * s, 1.62f, -.02f), new Vector3(.10f, .05f, .07f), pec, Matrix4x4.identity, 4, 8);            // traps
                    k.Ellipsoid(new Vector3(.20f * s, 1.33f, -.10f), new Vector3(.09f, .17f, .05f), Sh(skin, 1.02f), Matrix4x4.identity, 5, 8); // lats
                    for (int row = 0; row < 3; row++)
                        k.Ellipsoid(new Vector3(.048f * s, 1.29f - row * 0.085f, .112f), new Vector3(.048f, .036f, .03f), pec, Matrix4x4.identity, 4, 8); // abs
                }
                k.Box(new Vector3(0f, 1.20f, .121f), new Vector3(.008f, .24f, .008f), seam, Matrix4x4.identity);
                k.Box(new Vector3(0f, 1.46f, .15f), new Vector3(.008f, .12f, .006f), seam, Matrix4x4.identity);
            }
            else
            {
                // reflective stripes on the vest
                k.Box(new Vector3(0f, 1.30f, .136f), new Vector3(.40f, .04f, .01f), new Color(.9f, .9f, .9f), Matrix4x4.identity);
            }
            Part("Body", model, Vector3.zero, k, lit);

            // legs
            Color legCol = vig ? new Color(.10f, .14f, .30f) : trunk;
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit lk = new MeshKit();
                Color lower = vig ? legCol : skin;
                lk.Loft(new Ring[] {
                    R(0f, .125f, .12f, legCol), R(-.15f, .13f, .125f, legCol), R(-.36f, .10f, .10f, legCol), R(-.37f, .098f, .098f, lower),
                    R(-.48f, .075f, .08f, lower), R(-.62f, .085f, .085f, lower), R(-.72f, .06f, .06f, lower), R(-.80f, .045f, .045f, lower), R(-.84f, .04f, .045f, lower) }, 0, 10, Matrix4x4.identity);
                lk.Ellipsoid(new Vector3(0f, -.855f, .05f), new Vector3(.05f, .035f, .11f), vig ? new Color(.08f, .08f, .08f) : skin, Matrix4x4.identity, 4, 8);
                Transform leg = Part(s > 0 ? "LegR" : "LegL", model, new Vector3(.115f * s, .89f, 0f), lk, lit);
                if (s > 0) rig.legR = leg; else rig.legL = leg;
            }

            // arms
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit uk = new MeshKit();
                uk.Loft(new Ring[] { R(.03f, .08f, .08f, up), R(0f, .09f, .09f, skin), R(-.12f, .095f, .095f, skin), R(-.26f, .065f, .07f, skin), R(-.30f, .06f, .06f, skin) }, 0, 10, Matrix4x4.identity);
                Transform upper = Part(s > 0 ? "ArmR" : "ArmL", model, new Vector3(.37f * s, 1.52f, 0f), uk, lit);
                MeshKit fk = new MeshKit();
                fk.Loft(new Ring[] { R(0f, .06f, .06f, skin), R(-.10f, .07f, .07f, skin), R(-.26f, .04f, .04f, skin), R(-.30f, .038f, .038f, skin) }, 0, 10, Matrix4x4.identity);
                fk.Ellipsoid(new Vector3(0f, -.35f, .01f), new Vector3(.045f, .06f, .035f), skin, Matrix4x4.identity, 4, 8);
                Transform fore = Part("Fore", upper, new Vector3(0f, -.30f, 0f), fk, lit);
                if (s > 0) { rig.armRU = upper; rig.armRF = fore; } else { rig.armLU = upper; rig.armLF = fore; }
            }
            rig.restArm = 9f;

            // head
            MeshKit hk = new MeshKit();
            hk.Ellipsoid(new Vector3(0f, .12f, .01f), new Vector3(.095f, .115f, .105f), skin, Matrix4x4.identity, 7, 12);
            hk.Ellipsoid(new Vector3(0f, .05f, .03f), new Vector3(.088f, .075f, .095f), skin, Matrix4x4.identity, 5, 10);   // square jaw
            hk.Ellipsoid(new Vector3(0f, .11f, .11f), new Vector3(.018f, .025f, .02f), Sh(skin, .95f), Matrix4x4.identity, 4, 6); // nose
            double style = rng.NextDouble();
            if (vig)
            {
                hk.Ellipsoid(new Vector3(0f, .17f, 0f), new Vector3(.105f, .07f, .115f), new Color(.10f, .14f, .30f), Matrix4x4.identity, 5, 10);
                hk.Box(new Vector3(0f, .15f, .12f), new Vector3(.16f, .012f, .11f), new Color(.10f, .14f, .30f), Matrix4x4.identity);
            }
            else
            {
                hk.Ellipsoid(new Vector3(0f, .15f, -.005f), new Vector3(.10f, style < 0.4 ? .075f : .09f, .108f), hair, Matrix4x4.identity, 5, 10);
                if (style > 0.55 && style < 0.8) hk.Ellipsoid(new Vector3(0f, .21f, .04f), new Vector3(.07f, .05f, .07f), hair, Matrix4x4.identity, 4, 8);
                if (style >= 0.8) hk.Ellipsoid(new Vector3(0f, .23f, -.06f), new Vector3(.05f, .05f, .05f), hair, Matrix4x4.identity, 4, 8); // man-bun
                if (rng.NextDouble() < 0.35) hk.Ellipsoid(new Vector3(0f, .03f, .075f), new Vector3(.075f, .055f, .04f), hair, Matrix4x4.identity, 4, 8); // beard
            }
            if (rng.NextDouble() < 0.6)
            {
                Color glass = new Color(.05f, .05f, .07f);
                hk.Box(new Vector3(0f, .125f, .105f), new Vector3(.19f, .03f, .03f), glass, Matrix4x4.identity);
                for (int s = -1; s <= 1; s += 2) hk.Ellipsoid(new Vector3(.05f * s, .125f, .108f), new Vector3(.04f, .028f, .015f), glass, Matrix4x4.identity, 4, 8);
            }
            rig.head = Part("Head", model, new Vector3(0f, 1.74f, 0f), hk, lit);
        }

        // ------------------------------------------------------------------ WOMAN
        static void BuildWoman(Transform model, HumanRig rig, Material lit, System.Random rng, Color skin, Color hair)
        {
            Color bk = Bikini[rng.Next(Bikini.Length)];
            Color bk2 = Sh(bk, 0.85f);
            MeshKit k = new MeshKit();
            Ring[] torso = new Ring[] {
                R(.84f, .165f, .105f, skin), R(.89f, .205f, .125f, bk), R(.96f, .215f, .13f, bk), R(1.02f, .195f, .12f, bk), R(1.021f, .185f, .115f, skin),
                R(1.10f, .15f, .10f, skin), R(1.18f, .145f, .10f, skin), R(1.27f, .16f, .11f, skin), R(1.275f, .162f, .111f, bk2), R(1.31f, .168f, .115f, bk2), R(1.315f, .17f, .118f, skin),
                R(1.43f, .165f, .105f, skin), R(1.50f, .17f, .09f, skin), R(1.54f, .12f, .08f, skin), R(1.58f, .05f, .05f, skin), R(1.64f, .045f, .045f, skin) };
            k.Loft(torso, 0, 16, Matrix4x4.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                k.Ellipsoid(new Vector3(.072f * s, 1.37f, .075f), new Vector3(.082f, .075f, .07f), skin, Matrix4x4.identity, 5, 10);
                k.Ellipsoid(new Vector3(.072f * s, 1.375f, .082f), new Vector3(.088f, .078f, .072f), bk, Matrix4x4.identity, 5, 10);    // bikini cups
                k.Box(new Vector3(.085f * s, 1.50f, .0f), new Vector3(.016f, .15f, .02f), bk, Matrix4x4.identity);                       // straps
                k.Ellipsoid(new Vector3(.15f * s, .94f, .02f), new Vector3(.085f, .1f, .12f), bk, Matrix4x4.identity, 4, 8);              // hip curve
            }
            if (rng.NextDouble() < 0.45)
            {   // pareo / wrap skirt
                Color pc = Color.HSVToRGB((float)rng.NextDouble(), .55f, 1f);
                k.Loft(new Ring[] { R(.56f, .30f, .22f, pc), R(.75f, .27f, .19f, Sh(pc, .95f)), R(.98f, .215f, .135f, pc) }, 0, 16, Matrix4x4.identity);
            }
            Part("Body", model, Vector3.zero, k, lit);

            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit lk = new MeshKit();
                lk.Loft(new Ring[] {
                    R(0f, .11f, .11f, skin), R(-.18f, .105f, .105f, skin), R(-.42f, .068f, .075f, skin), R(-.62f, .066f, .066f, skin), R(-.80f, .036f, .04f, skin), R(-.86f, .034f, .04f, skin) }, 0, 10, Matrix4x4.identity);
                lk.Ellipsoid(new Vector3(0f, -.89f, .05f), new Vector3(.04f, .03f, .10f), skin, Matrix4x4.identity, 4, 8);
                Transform leg = Part(s > 0 ? "LegR" : "LegL", model, new Vector3(.095f * s, .92f, 0f), lk, lit);
                if (s > 0) rig.legR = leg; else rig.legL = leg;
            }
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit uk = new MeshKit();
                uk.Loft(new Ring[] { R(.02f, .045f, .045f, skin), R(-.10f, .048f, .048f, skin), R(-.24f, .038f, .038f, skin), R(-.27f, .036f, .036f, skin) }, 0, 8, Matrix4x4.identity);
                Transform upper = Part(s > 0 ? "ArmR" : "ArmL", model, new Vector3(.205f * s, 1.50f, 0f), uk, lit);
                MeshKit fk = new MeshKit();
                fk.Loft(new Ring[] { R(0f, .036f, .036f, skin), R(-.12f, .04f, .04f, skin), R(-.24f, .028f, .028f, skin), R(-.26f, .027f, .027f, skin) }, 0, 8, Matrix4x4.identity);
                fk.Ellipsoid(new Vector3(0f, -.30f, .01f), new Vector3(.03f, .045f, .022f), skin, Matrix4x4.identity, 4, 8);
                Transform fore = Part("Fore", upper, new Vector3(0f, -.27f, 0f), fk, lit);
                if (s > 0) { rig.armRU = upper; rig.armRF = fore; } else { rig.armLU = upper; rig.armLF = fore; }
            }
            rig.restArm = 5f;

            MeshKit hk = new MeshKit();
            hk.Ellipsoid(new Vector3(0f, .105f, .01f), new Vector3(.085f, .105f, .095f), skin, Matrix4x4.identity, 7, 12);
            hk.Ellipsoid(new Vector3(0f, .04f, .03f), new Vector3(.07f, .06f, .08f), skin, Matrix4x4.identity, 5, 10);
            hk.Ellipsoid(new Vector3(0f, .10f, .10f), new Vector3(.015f, .02f, .018f), Sh(skin, .95f), Matrix4x4.identity, 4, 6);
            hk.Ellipsoid(new Vector3(0f, .03f, .085f), new Vector3(.03f, .008f, .012f), new Color(.85f, .2f, .28f), Matrix4x4.identity, 3, 8); // lips
            hk.Ellipsoid(new Vector3(0f, .135f, -.02f), new Vector3(.092f, .1f, .102f), hair, Matrix4x4.identity, 5, 10);
            double hs = rng.NextDouble();
            MeshKit ponyKit = null;
            if (hs < 0.55)
                hk.Loft(new Ring[] { new Ring(new Vector3(0f, .10f, -.075f), .095f, .05f, hair), new Ring(new Vector3(0f, -.02f, -.085f), .105f, .06f, hair),
                    new Ring(new Vector3(0f, -.18f, -.09f), .11f, .06f, hair), new Ring(new Vector3(0f, -.34f, -.085f), .07f, .04f, hair) }, 0, 10, Matrix4x4.identity);   // long hair
            else if (hs < 0.85)
            {
                ponyKit = new MeshKit();
                ponyKit.Loft(new Ring[] { R(0f, .035f, .035f, hair), R(-.1f, .048f, .048f, hair), R(-.25f, .032f, .032f, hair), R(-.34f, .008f, .008f, hair) }, 0, 8, Matrix4x4.identity);
            }
            else hk.Ellipsoid(new Vector3(0f, .25f, -.05f), new Vector3(.06f, .06f, .06f), hair, Matrix4x4.identity, 4, 8); // bun
            Color glass = new Color(.05f, .05f, .07f);
            double gs = rng.NextDouble();
            if (gs < 0.5)
            {
                hk.Box(new Vector3(0f, .115f, .095f), new Vector3(.17f, .03f, .03f), glass, Matrix4x4.identity);
                for (int s = -1; s <= 1; s += 2) hk.Ellipsoid(new Vector3(.048f * s, .115f, .098f), new Vector3(.038f, .028f, .014f), glass, Matrix4x4.identity, 4, 8);
            }
            else if (gs < 0.8) hk.Box(new Vector3(0f, .20f, .05f), new Vector3(.17f, .02f, .02f), glass, Matrix4x4.identity);  // glasses pushed up
            if (rng.NextDouble() < 0.4)
            {   // wide-brim straw sun hat
                Color straw = new Color(.9f, .78f, .5f);
                hk.Ellipsoid(new Vector3(0f, .20f, 0f), new Vector3(.31f, .012f, .31f), straw, Matrix4x4.identity, 3, 16);
                hk.Ellipsoid(new Vector3(0f, .235f, 0f), new Vector3(.12f, .06f, .12f), straw, Matrix4x4.identity, 5, 12);
                hk.Ellipsoid(new Vector3(0f, .212f, 0f), new Vector3(.125f, .02f, .125f), bk, Matrix4x4.identity, 3, 12);
            }
            rig.head = Part("Head", model, new Vector3(0f, 1.62f, 0f), hk, lit);
            if (ponyKit != null) rig.pony = Part("Pony", rig.head, new Vector3(0f, .14f, -.11f), ponyKit, lit);
        }
    }
}
