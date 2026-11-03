using UnityEngine;

namespace Tonbi
{
    /// <summary>Colour set for the procedural bird. Kite() follows a real black kite (トンビ): warm brown body, pale head with dark eye mask,
    /// yellow cere and feet, dark primaries with a pale window under the hand, forked tail.</summary>
    public struct HawkPalette
    {
        public Color bodyTop, bodyBot, head, covTop, flightTop, tipTop, covBot, flightBot, tipBot, window, tail, tailBot, beak, cere, leg, eye;

        public static HawkPalette Kite()
        {
            HawkPalette p = new HawkPalette();
            p.bodyTop = new Color(.40f, .28f, .18f); p.bodyBot = new Color(.55f, .39f, .25f); p.head = new Color(.66f, .54f, .41f);
            p.covTop = new Color(.45f, .32f, .20f); p.flightTop = new Color(.20f, .14f, .10f); p.tipTop = new Color(.13f, .10f, .08f);
            p.covBot = new Color(.56f, .38f, .22f); p.flightBot = new Color(.30f, .24f, .19f); p.tipBot = new Color(.16f, .13f, .11f);
            p.window = new Color(.84f, .76f, .58f); p.tail = new Color(.31f, .22f, .15f); p.tailBot = new Color(.50f, .40f, .30f);
            p.beak = new Color(.12f, .11f, .10f); p.cere = new Color(.96f, .79f, .20f); p.leg = new Color(.94f, .77f, .22f); p.eye = new Color(.04f, .03f, .02f);
            return p;
        }
        public static HawkPalette Gull()
        {
            HawkPalette p = new HawkPalette();
            p.bodyTop = new Color(.88f, .90f, .93f); p.bodyBot = new Color(.97f, .97f, .97f); p.head = new Color(.98f, .98f, .98f);
            p.covTop = new Color(.72f, .76f, .82f); p.flightTop = new Color(.66f, .70f, .76f); p.tipTop = new Color(.10f, .10f, .12f);
            p.covBot = new Color(.95f, .95f, .96f); p.flightBot = new Color(.85f, .87f, .90f); p.tipBot = new Color(.15f, .15f, .17f);
            p.window = new Color(.95f, .95f, .96f); p.tail = new Color(.94f, .94f, .95f); p.tailBot = new Color(.96f, .96f, .96f);
            p.beak = new Color(.95f, .72f, .15f); p.cere = new Color(.95f, .72f, .15f); p.leg = new Color(.95f, .55f, .3f); p.eye = new Color(.05f, .04f, .03f);
            return p;
        }
        public static HawkPalette Crow()
        {
            HawkPalette p = new HawkPalette();
            Color k = new Color(.07f, .07f, .09f), k2 = new Color(.12f, .12f, .15f);
            p.bodyTop = k; p.bodyBot = k2; p.head = k; p.covTop = k2; p.flightTop = k; p.tipTop = k; p.covBot = k2; p.flightBot = k; p.tipBot = k;
            p.window = k2; p.tail = k; p.tailBot = k2; p.beak = new Color(.05f, .05f, .05f); p.cere = p.beak; p.leg = new Color(.05f, .05f, .05f); p.eye = new Color(.5f, .4f, .2f);
            return p;
        }
    }

    /// <summary>Procedural hawk: lofted body, 3-joint wings with 6 spread primaries ("fingers"), forked tail, hooked beak, legs/talons.
    /// Pose() animates flap / stoop (swept-back dive) / perch (folded wings, tail down) / tail rudder.</summary>
    public class HawkModel : MonoBehaviour
    {
        Transform shL, elL, wrL, shR, elR, wrR, tailT, legL, legR;
        readonly Transform[] finL = new Transform[6];
        readonly Transform[] finR = new Transform[6];
        HawkPalette pal;
        float phase, flapK, sweepK, grabK;

        static readonly float[] SegLen = { 1.2f, 1.1f, 0.7f };
        static readonly float[] LE0 = { .50f, .52f, .46f };
        static readonly float[] LE1 = { .52f, .46f, .34f };
        static readonly float[] TE0 = { -.80f, -.75f, -.60f };
        static readonly float[] TE1 = { -.75f, -.60f, -.30f };
        static readonly float[] FinLen = { .95f, 1.0f, 1.0f, .92f, .8f, .62f };

        static Color Cm(Color c, float m) { return new Color(c.r * m, c.g * m, c.b * m, 1f); }

        public void Build(HawkPalette p)
        {
            pal = p;
            Material lit = TonbiMat.Lit(0.22f);

            MeshKit k = new MeshKit();
            Color T = pal.bodyTop, B = pal.bodyBot, H = pal.head;
            Ring[] body = new Ring[] {
                new Ring(new Vector3(0f, .10f, 1.16f), .03f, .03f, H),
                new Ring(new Vector3(0f, .13f, 1.08f), .13f, .14f, H),
                new Ring(new Vector3(0f, .14f, .92f), .205f, .205f, H),
                new Ring(new Vector3(0f, .11f, .74f), .21f, .21f, Color.Lerp(H, T, .5f), Color.Lerp(H, B, .4f)),
                new Ring(new Vector3(0f, .06f, .48f), .31f, .32f, T, B),
                new Ring(new Vector3(0f, 0f, .10f), .37f, .37f, T, B),
                new Ring(new Vector3(0f, -.03f, -.32f), .31f, .29f, T, B),
                new Ring(new Vector3(0f, -.05f, -.72f), .19f, .17f, T, B),
                new Ring(new Vector3(0f, -.06f, -1.02f), .09f, .07f, pal.tail, pal.tailBot),
                new Ring(new Vector3(0f, -.06f, -1.10f), .01f, .01f, pal.tail, pal.tailBot) };
            k.Loft(body, 1, 14, Matrix4x4.identity);
            Ring[] beak = new Ring[] {
                new Ring(new Vector3(0f, .09f, 1.14f), .075f, .08f, pal.cere),
                new Ring(new Vector3(0f, .09f, 1.24f), .06f, .07f, pal.cere),
                new Ring(new Vector3(0f, .085f, 1.25f), .055f, .065f, pal.beak),
                new Ring(new Vector3(0f, .07f, 1.33f), .035f, .05f, pal.beak),
                new Ring(new Vector3(0f, .04f, 1.38f), .012f, .02f, pal.beak) };
            k.Loft(beak, 1, 8, Matrix4x4.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                k.Ellipsoid(new Vector3(.155f * s, .20f, 1.01f), new Vector3(.035f, .035f, .035f), pal.eye, Matrix4x4.identity, 5, 8);
                // dark eye mask + brow ridge (the "stern" look of a kite)
                k.Ellipsoid(new Vector3(.17f * s, .20f, .96f), new Vector3(.03f, .05f, .12f), Cm(pal.bodyTop, .55f), Matrix4x4.identity, 4, 8);
                k.Ellipsoid(new Vector3(.14f * s, .25f, .99f), new Vector3(.07f, .025f, .07f), Color.Lerp(pal.head, pal.bodyTop, .5f), Matrix4x4.identity, 4, 8);
            }
            MeshKit.Make("Body", transform, k, lit);

            BuildWing(1, lit, out shR, out elR, out wrR, finR);
            BuildWing(-1, lit, out shL, out elL, out wrL, finL);

            tailT = MeshKit.Make("Tail", transform, TailMesh(), lit).transform;
            tailT.localPosition = new Vector3(0f, -0.05f, -1.0f);
            BuildLeg(1, lit, out legR);
            BuildLeg(-1, lit, out legL);
            Pose(0.016f, 0f, false, 0f, 0f, false);
        }

        void BuildWing(int sg, Material lit, out Transform sh, out Transform el, out Transform wr, Transform[] fins)
        {
            sh = MeshKit.Make(sg > 0 ? "ShoulderR" : "ShoulderL", transform, WingSeg(0, sg), lit).transform;
            sh.localPosition = new Vector3(.30f * sg, .08f, .05f);
            el = MeshKit.Make("Elbow", sh, WingSeg(1, sg), lit).transform;
            el.localPosition = new Vector3(SegLen[0] * sg, 0f, 0f);
            wr = MeshKit.Make("Wrist", el, WingSeg(2, sg), lit).transform;
            wr.localPosition = new Vector3(SegLen[1] * sg, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                fins[i] = MeshKit.Make("Fin" + i, wr, FingerMesh(sg, i), lit).transform;
                fins[i].localPosition = new Vector3((SegLen[2] - 0.04f) * sg, 0.004f * i, Mathf.Lerp(-.30f, .28f, i / 5f));
            }
        }

        MeshKit WingSeg(int seg, int sg)
        {
            MeshKit k = new MeshKit();
            float len = SegLen[seg], l0 = LE0[seg], l1 = LE1[seg], t0 = TE0[seg], t1 = TE1[seg];
            float jagAmt = seg < 2 ? 1f : 0.4f;
            k.Panel(8, 5,
                (u, w) =>
                {
                    float le = Mathf.Lerp(l0, l1, u);
                    float te = Mathf.Lerp(t0, t1, u) - 0.05f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 6f)) * jagAmt;
                    float camber = 0.07f * Mathf.Sin(w * Mathf.PI) * (1f - 0.4f * u);
                    return new Vector3(sg * u * len, camber, Mathf.Lerp(le, te, w));
                },
                (u, w) => (0.02f + 0.075f * (1f - 0.65f * u)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(w * Mathf.PI)), 0.7f),
                (top, u, w) => WingCol(top, seg, u, w),
                Matrix4x4.identity);
            return k;
        }

        Color WingCol(bool top, int seg, float u, float w)
        {
            float bar = 0.93f + 0.07f * Mathf.Sin((u + seg) * 38f + w * 9f);
            Color c;
            if (top)
            {
                if (seg == 2) c = pal.tipTop;
                else if (w < 0.42f) c = Color.Lerp(pal.covTop, Cm(pal.covTop, 0.8f), w * 2f);
                else c = pal.flightTop;
                if (seg < 2 && w > 0.34f && w < 0.46f) c = Color.Lerp(c, Cm(pal.covTop, 1.3f), 0.5f); // pale covert bar
            }
            else
            {
                if (seg == 2) c = (w > 0.12f && w < 0.88f && u < 0.9f) ? pal.window : pal.tipBot;       // white patch under the hand
                else c = w < 0.45f ? pal.covBot : pal.flightBot;
            }
            return Cm(c, bar);
        }

        MeshKit FingerMesh(int sg, int i)
        {
            MeshKit k = new MeshKit();
            float L = FinLen[i], fw = 0.17f;
            k.Panel(4, 2,
                (u, w) =>
                {
                    float wd = fw * (1f - 0.5f * u) * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(u, 6f)));
                    return new Vector3(sg * u * L, 0.03f * u * u * L, (w - 0.5f) * wd);
                },
                (u, w) => (0.012f + 0.02f * (1f - u)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(w * Mathf.PI)), 0.6f),
                (top, u, w) => top ? Cm(pal.tipTop, 1f + 0.25f * Mathf.Exp(-Mathf.Pow((w - 0.5f) * 6f, 2f))) : (u < 0.5f ? pal.window : pal.tipBot),
                Matrix4x4.identity);
            return k;
        }

        MeshKit TailMesh()
        {
            MeshKit k = new MeshKit();
            k.Panel(10, 6,
                (u, w) =>
                {
                    float uu = u * 2f - 1f;
                    float lenU = 0.95f + 0.30f * Mathf.Pow(Mathf.Abs(uu), 1.1f);   // centre shorter => forked (notched) tail
                    float wid = Mathf.Lerp(0.14f, 0.62f, Mathf.Pow(w, 0.8f));
                    return new Vector3(uu * wid, -0.09f * w * w, -w * lenU);
                },
                (u, w) => 0.012f * Mathf.Sin(w * Mathf.PI * 0.5f + 0.2f),
                (top, u, w) => Cm(top ? pal.tail : pal.tailBot, 0.85f + 0.15f * Mathf.Sin(w * 28f)),
                Matrix4x4.identity);
            return k;
        }

        void BuildLeg(int sg, Material lit, out Transform leg)
        {
            MeshKit k = new MeshKit();
            Color fe = pal.bodyBot, yl = pal.leg;
            Ring[] r = new Ring[] {
                new Ring(new Vector3(0f, 0f, 0f), .085f, .085f, fe),
                new Ring(new Vector3(0f, -.16f, 0f), .075f, .075f, fe),
                new Ring(new Vector3(0f, -.18f, 0f), .04f, .04f, yl),
                new Ring(new Vector3(0f, -.55f, 0f), .032f, .032f, yl),
                new Ring(new Vector3(0f, -.58f, 0f), .045f, .05f, yl) };
            k.Loft(r, 0, 8, Matrix4x4.identity);
            for (int t = -1; t <= 1; t++)
            {
                Ring[] toe = new Ring[] {
                    new Ring(new Vector3(0f, -.58f, 0f), .03f, .03f, yl),
                    new Ring(new Vector3(t * .07f, -.62f, .17f), .022f, .022f, yl),
                    new Ring(new Vector3(t * .09f, -.66f, .27f), .01f, .01f, pal.beak) };
                k.Loft(toe, 1, 6, Matrix4x4.identity);
            }
            leg = MeshKit.Make(sg > 0 ? "LegR" : "LegL", transform, k, lit).transform;
            leg.localPosition = new Vector3(.15f * sg, -.28f, .10f);
        }

        static void SetWing(Transform sh, Transform el, Transform wr, int sg, float shY, float shZ, float elY, float elZ, float wrY, float wrZ)
        {
            sh.localRotation = Quaternion.Euler(0f, sg * shY, sg * shZ);
            el.localRotation = Quaternion.Euler(0f, sg * elY, sg * elZ);
            wr.localRotation = Quaternion.Euler(0f, sg * wrY, sg * wrZ);
        }

        /// <summary>perchK 0..1 folds the wings; diving sweeps them back and extends the talons; flapBurst&gt;0 flaps.</summary>
        public void Pose(float dt, float perchK, bool diving, float roll, float flapBurst, bool stunned)
        {
            if (shR == null) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            flapK += ((flapBurst > 0f ? 1f : 0f) - flapK) * Mathf.Min(1f, dt * 10f);
            sweepK += ((diving ? 1f : 0f) - sweepK) * Mathf.Min(1f, dt * 7f);
            grabK += ((diving ? 1f : 0f) - grabK) * Mathf.Min(1f, dt * 6f);
            phase += dt * (Mathf.Lerp(1.3f, 8.5f, flapK) + (stunned ? 6f : 0f));
            float amp = stunned ? 55f : Mathf.Lerp(2.5f, 36f, flapK);
            float s0 = Mathf.Sin(phase), s1 = Mathf.Sin(phase - 0.7f), s2 = Mathf.Sin(phase - 1.4f);
            float p = perchK;

            float shZ = Mathf.Lerp(7f + amp * s0 - 10f * sweepK, -32f, p);
            float elZ = Mathf.Lerp(-3f + amp * 0.7f * s1 - 4f * sweepK, -10f, p);
            float wrZ = Mathf.Lerp(-10f + amp * 0.9f * s2, -6f, p);
            float shY = Mathf.Lerp(4f + 34f * sweepK, 80f, p);
            float elY = Mathf.Lerp(42f * sweepK, -165f, p);
            float wrY = Mathf.Lerp(6f + 46f * sweepK, 160f, p);
            SetWing(shR, elR, wrR, 1, shY, shZ, elY, elZ, wrY, wrZ);
            SetWing(shL, elL, wrL, -1, shY, shZ, elY, elZ, wrY, wrZ);

            float spread = Mathf.Lerp(1f, 0.25f, Mathf.Max(sweepK, p));
            for (int i = 0; i < 6; i++)
            {
                float fy = Mathf.Lerp(24f, -14f, i / 5f) * spread;
                float fz = (i * 3f + 6f + 3f * Mathf.Sin(Time.time * 2.5f + i)) * (1f - 0.5f * sweepK) * (1f - p);
                finR[i].localRotation = Quaternion.Euler(0f, fy, fz);
                finL[i].localRotation = Quaternion.Euler(0f, -fy, -fz);
            }

            float tw = Mathf.Clamp(roll * 18f, -25f, 25f);
            tailT.localRotation = Quaternion.Euler(Mathf.Lerp(6f + 12f * sweepK, -38f, p), 0f, -tw);
            float fan = 1f + 0.25f * Mathf.Abs(roll) - 0.35f * sweepK;
            tailT.localScale = new Vector3(Mathf.Max(0.5f, fan), 1f, 1f);

            float legA = Mathf.Lerp(78f, -32f, grabK);
            legA = Mathf.Lerp(legA, 8f, p);
            legL.localRotation = Quaternion.Euler(legA, 0f, 0f);
            legR.localRotation = Quaternion.Euler(legA, 0f, 0f);
        }
    }
}
