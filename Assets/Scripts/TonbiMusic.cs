using System;
using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// Procedural BGM + SFX (no audio assets). Lives on the camera (AudioListener) and fills the mix in OnAudioFilterRead.
    /// The bass lines use the open strings of a 4-string bass: E1 (41.20 Hz) - A1 (55.00) - D2 (73.42) - G2 (98.00).
    /// Scenes: 0 title, 1 beach, 2 night, 3 festival, 4 ending (G-flat major: Gb - Bb - Db).
    /// SFX: 0 grab, 1 hit, 2 go, 3 stamp, 4 alert, 5 kite call "ピーヒョロロロ", 6 stage jingle, 7 thunder.
    /// </summary>
    public class TonbiMusic : MonoBehaviour
    {
        static TonbiMusic inst;
        static int pendingScene = -1;
        public static void SetScene(int s) { if (inst != null) inst.scene = s; else pendingScene = s; }
        public static void Sfx(int id) { if (inst != null) inst.QueueSfx(id); }
        public static void Flight(float dive, float speed, bool live) { if (inst != null) { inst.diveAmt = dive; inst.speedN = speed; inst.liveFlight = live ? 1f : 0f; } }
        public static void ToggleMute() { if (inst != null) inst.muted = !inst.muted; }
        public static bool Muted { get { return inst != null && inst.muted; } }

        public float musicVol = 0.5f, sfxVol = 0.8f;
        volatile int scene = -1;
        volatile float diveAmt, speedN, liveFlight;
        volatile bool muted;
        readonly int[] sfxQ = new int[32];
        volatile int qHead; int qTail;

        const int N = -99;
        class Song
        {
            public double bpm; public float[] roots; public int[] third; public int[] bass; public int[][] lead;
            public string kick, snare, hat, taiko, rim; public float padVol, leadVol, bassVol, drumVol; public int leadWave; public float leadOct;
        }
        static Song[] songs;

        static void MakeSongs()
        {
            songs = new Song[5];
            // 0 TITLE - bold: E A D G (the four open bass strings), major, drive
            songs[0] = new Song { bpm = 118, roots = new float[] { 41.20f, 55.00f, 73.42f, 98.00f }, third = new int[] { 4, 4, 4, 4 },
                bass = new int[] { 0, N, 0, 12, 0, N, 12, 0, 0, N, 0, 12, 0, 7, 12, N },
                lead = new int[][] { new int[] { 0, N, 1, N, 2, N, 3, N, 2, N, 1, N, 0, 1, 2, N }, new int[] { 3, N, 2, N, 1, N, 2, N, 3, N, 4, N, 3, 2, 1, N },
                                     new int[] { 0, N, 1, N, 2, N, 3, N, 4, N, 3, N, 2, N, 1, N }, new int[] { 5, N, 4, N, 3, N, 2, N, 1, N, 2, N, 3, N, 5, N } },
                kick = "k..k..k...k.k...", snare = "....s.......s...", hat = "h.h.h.h.h.h.h.hh", taiko = "", rim = "",
                padVol = 0.10f, leadVol = 0.22f, bassVol = 0.55f, drumVol = 0.8f, leadWave = 0, leadOct = 8f };
            // 1 BEACH - surf-rock groove: E A D A
            songs[1] = new Song { bpm = 126, roots = new float[] { 41.20f, 55.00f, 73.42f, 55.00f }, third = new int[] { 4, 4, 4, 4 },
                bass = new int[] { 0, N, 12, N, 0, N, 12, 0, N, 12, N, 7, N, 12, N, 10 },
                lead = new int[][] { new int[] { 0, N, N, 2, N, 3, N, N, 2, N, 3, N, 4, N, 3, N }, new int[] { 1, N, N, 3, N, 2, N, N, 3, N, 4, N, 5, N, 4, N },
                                     new int[] { 0, N, N, 2, N, 3, N, N, 2, N, N, 1, N, 0, N, N }, new int[] { 3, N, 2, N, 1, N, 0, N, 1, N, 2, N, 3, N, N, N } },
                kick = "k...k...k...k...", snare = "....s.......s...", hat = "..h...h...h...h.", taiko = "", rim = "",
                padVol = 0.0f, leadVol = 0.2f, bassVol = 0.55f, drumVol = 0.7f, leadWave = 0, leadOct = 8f };
            // 2 NIGHT - E A D G, minor-ish, slow
            songs[2] = new Song { bpm = 94, roots = new float[] { 41.20f, 55.00f, 73.42f, 98.00f }, third = new int[] { 3, 3, 4, 4 },
                bass = new int[] { 0, N, N, N, 0, N, 12, N, N, N, 0, N, 7, N, N, N },
                lead = new int[][] { new int[] { N, N, N, N, 2, N, N, N, N, N, 3, N, N, N, 2, N }, new int[] { N, N, 1, N, N, N, N, N, 2, N, N, N, 3, N, N, N },
                                     new int[] { N, N, N, N, 3, N, N, N, N, N, 2, N, N, N, 1, N }, new int[] { 4, N, N, N, 3, N, N, N, 2, N, N, N, 0, N, N, N } },
                kick = "k.......k.k.....", snare = "....s.......s...", hat = "..h...h...h...h.", taiko = "", rim = "",
                padVol = 0.16f, leadVol = 0.2f, bassVol = 0.5f, drumVol = 0.45f, leadWave = 1, leadOct = 8f };
            // 3 FESTIVAL - taiko + plucked "shamisen"
            songs[3] = new Song { bpm = 148, roots = new float[] { 41.20f, 41.20f, 55.00f, 61.74f }, third = new int[] { 3, 3, 3, 3 },
                bass = new int[] { 0, N, 0, N, 12, N, 0, N, 0, N, 0, N, 12, 0, N, 12 },
                lead = new int[][] { new int[] { 3, N, 2, 3, N, 2, 1, N, 3, N, 2, 3, 4, N, 3, N }, new int[] { 1, N, 2, 3, N, 2, 1, N, 0, N, 1, 2, 3, N, 2, N },
                                     new int[] { 3, N, 2, 3, N, 2, 1, N, 3, N, 2, 3, 4, N, 5, N }, new int[] { 4, N, 3, 2, N, 3, 4, N, 5, N, 4, 3, 2, N, 1, N } },
                kick = "", snare = "", hat = "h.h.h.h.h.h.h.h.", taiko = "d..d.dd.d..d.dd.", rim = "..k...k...k...k.",
                padVol = 0.0f, leadVol = 0.24f, bassVol = 0.45f, drumVol = 0.85f, leadWave = 2, leadOct = 8f };
            // 4 ENDING - warm, G-flat major (Gb - Bb - Db), no drums
            songs[4] = new Song { bpm = 72, roots = new float[] { 46.25f, 69.30f, 77.78f, 69.30f }, third = new int[] { 4, 4, 3, 4 },
                bass = new int[] { 0, N, N, N, N, N, N, N, 0, N, N, N, N, N, 7, N },
                lead = new int[][] { new int[] { 0, N, 2, N, 3, N, 2, N, 0, N, 2, N, 3, N, 5, N }, new int[] { 1, N, 2, N, 3, N, 4, N, 3, N, 2, N, 1, N, 2, N },
                                     new int[] { 0, N, 2, N, 3, N, 2, N, 4, N, 3, N, 2, N, 0, N }, new int[] { 2, N, 3, N, 4, N, 5, N, 4, N, 3, N, 2, N, N, N } },
                kick = "", snare = "", hat = "", taiko = "", rim = "",
                padVol = 0.26f, leadVol = 0.2f, bassVol = 0.5f, drumVol = 0.0f, leadWave = 1, leadOct = 8f };
        }

        // ---------------- audio-thread state ----------------
        double sr = 48000; bool ready;
        int curScene = -2; Song song; int step; double stepCtr, stepLen; float fade = 1f;
        uint rs = 2463534242u;
        double bph, lph, kph, tph, rph; float bf, bEnv, bAtt, bRel, bGateDec; int bGate;
        float lf, lEnv, lDec, llp; float kEnv, sEnv, hEnv, tEnv, rEnv, hPrev, kDec, sDec, hDec, tDec, rDec; double sph;
        readonly float[] pf = new float[3]; readonly float[] pft = new float[3]; readonly double[] pph = new double[3]; float pEnv;
        // sfx blips
        const int NB = 10;
        readonly double[] bPh = new double[NB]; readonly float[] bFreq = new float[NB], bFreq2 = new float[NB], bAmp = new float[NB], bDec = new float[NB]; readonly int[] bWave = new int[NB], bDelay = new int[NB];
        double callT = -1; float windLp, windAmp; float thunder;

        void Awake()
        {
            inst = this;
            if (songs == null) MakeSongs();
            sr = AudioSettings.outputSampleRate; if (sr <= 0) sr = 48000;
            kDec = Dec(0.09); sDec = Dec(0.07); hDec = Dec(0.025); tDec = Dec(0.28); rDec = Dec(0.02);
            bRel = Dec(0.06); bGateDec = Dec(2.0);
            if (pendingScene >= 0) { scene = pendingScene; pendingScene = -1; }
            ready = true;
        }
        void OnDestroy() { if (inst == this) inst = null; }
        float Dec(double tau) { return (float)Math.Exp(-1.0 / (tau * sr)); }

        void QueueSfx(int id) { sfxQ[qHead & 31] = id; qHead = qHead + 1; }

        float Rnd() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs & 0xFFFFFF) / (float)0x800000 - 1f; }

        void SwitchScene(int sc)
        {
            curScene = sc; song = (sc >= 0 && sc < songs.Length) ? songs[sc] : null; step = 0; stepCtr = 0; fade = 0f;
            if (song != null) { stepLen = 60.0 / song.bpm / 4.0 * sr; }
            bf = 0; lf = 0; kEnv = sEnv = hEnv = tEnv = rEnv = 0; pEnv = 0;
        }

        void OnStep()
        {
            Song s = song; int s16 = step & 15; int b = (step >> 4) & 3; float root = s.roots[b]; int third = s.third[b];
            int bn = s.bass[s16];
            if (bn > -90) { bf = root * (float)Math.Pow(2.0, bn / 12.0); bEnv = 1f; bAtt = 0f; bGate = (int)(stepLen * 0.85); }
            if (s.kick.Length > s16 && s.kick[s16] == 'k') kEnv = 1f;
            if (s.snare.Length > s16 && s.snare[s16] == 's') sEnv = 1f;
            if (s.hat.Length > s16 && s.hat[s16] == 'h') hEnv = 1f;
            if (s.taiko.Length > s16 && s.taiko[s16] == 'd') { tEnv = 1f; }
            if (s.rim.Length > s16 && s.rim[s16] == 'k') rEnv = 1f;
            int li = s.lead[b][s16];
            if (li >= 0)
            {
                lf = root * s.leadOct * (float)Math.Pow(2.0, Tone(li, third) / 12.0); lEnv = 1f;
                lDec = Dec(s.leadWave == 1 ? 0.8 : s.leadWave == 2 ? 0.12 : 0.18);
            }
            if (s16 == 0)
            {
                float pr = root * 4f;
                pft[0] = pr; pft[1] = pr * (float)Math.Pow(2.0, third / 12.0); pft[2] = pr * (float)Math.Pow(2.0, 7.0 / 12.0);
                if (pf[0] <= 0f) { pf[0] = pft[0]; pf[1] = pft[1]; pf[2] = pft[2]; }
            }
            step++;
        }

        static int Tone(int i, int third)
        {
            switch (i) { case 0: return 0; case 1: return third; case 2: return 7; case 3: return 12; case 4: return 12 + third; default: return 19; }
        }

        void SpawnBlip(float f, float f2, float amp, float tau, int wave, float delaySec)
        {
            for (int i = 0; i < NB; i++)
                if (bAmp[i] < 0.002f && bDelay[i] <= 0)
                {
                    bFreq[i] = f; bFreq2[i] = f2; bAmp[i] = amp; bDec[i] = Dec(tau); bWave[i] = wave; bDelay[i] = (int)(delaySec * sr); bPh[i] = 0; return;
                }
        }

        void HandleSfx(int id)
        {
            switch (id)
            {
                case 0: SpawnBlip(880f, 880f, 0.35f, 0.22f, 0, 0f); SpawnBlip(1175f, 1175f, 0.3f, 0.22f, 0, 0.06f); SpawnBlip(1760f, 1760f, 0.28f, 0.3f, 0, 0.12f); SpawnBlip(2349f, 2349f, 0.2f, 0.3f, 0, 0.18f); break;
                case 1: SpawnBlip(120f, 45f, 0.7f, 0.22f, 0, 0f); SpawnBlip(0f, 0f, 0.5f, 0.16f, 2, 0f); break;
                case 2: SpawnBlip(1200f, 1900f, 0.3f, 0.4f, 0, 0f); SpawnBlip(600f, 950f, 0.2f, 0.4f, 1, 0f); break;
                case 3: SpawnBlip(70f, 32f, 0.9f, 0.5f, 0, 0f); SpawnBlip(0f, 0f, 0.6f, 0.2f, 2, 0f); SpawnBlip(554f, 554f, 0.25f, 0.9f, 0, 0.12f); SpawnBlip(698f, 698f, 0.22f, 0.9f, 0, 0.12f); SpawnBlip(831f, 831f, 0.2f, 0.9f, 0, 0.12f); break;
                case 4: SpawnBlip(1400f, 1400f, 0.3f, 0.07f, 1, 0f); SpawnBlip(1400f, 1400f, 0.3f, 0.07f, 1, 0.11f); break;
                case 5: callT = 0; break;
                case 6: SpawnBlip(659f, 659f, 0.3f, 0.3f, 1, 0f); SpawnBlip(830f, 830f, 0.3f, 0.3f, 1, 0.12f); SpawnBlip(988f, 988f, 0.3f, 0.3f, 1, 0.24f); SpawnBlip(1319f, 1319f, 0.3f, 0.6f, 1, 0.36f); break;
                case 7: thunder = 1f; break;
            }
        }

        float Wave(int w, double ph)
        {
            if (w == 1) return ph < 0.5 ? 0.5f : -0.5f;
            return (float)Math.Sin(6.283185307 * ph);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!ready || channels < 1) return;
            int n = data.Length / channels;
            float mv = muted ? 0f : musicVol, sv = muted ? 0.3f * sfxVol : sfxVol;
            float dive = diveAmt, spd = speedN, live = liveFlight;
            int sc = scene;
            while (qTail != qHead) { HandleSfx(sfxQ[qTail & 31]); qTail++; }

            for (int i = 0; i < n; i++)
            {
                if (sc != curScene) SwitchScene(sc);
                float m = 0f;
                if (song != null)
                {
                    stepCtr -= 1.0; if (stepCtr <= 0.0) { OnStep(); stepCtr += stepLen; }
                    if (fade < 1f) fade += 1f / (float)(0.8 * sr);
                    Song s = song;

                    // bass (E1/A1/D2/G2 open strings): saw + sine through a one-pole low-pass
                    if (bf > 0f)
                    {
                        bph += bf / sr; if (bph >= 1.0) bph -= 1.0;
                        float x = 0.55f * (float)(2.0 * bph - 1.0) + 0.7f * (float)Math.Sin(6.283185307 * bph);
                        bAtt = Math.Min(1f, bAtt + 1f / (0.004f * (float)sr));
                        if (bGate > 0) { bGate--; bEnv *= bGateDec; } else bEnv *= bRel;
                        m += x * bEnv * bAtt * 0.45f * s.bassVol;
                    }
                    // kick
                    if (kEnv > 0.001f)
                    {
                        kph += (42f + 150f * kEnv * kEnv * kEnv) / sr; if (kph >= 1.0) kph -= 1.0;
                        m += (float)Math.Sin(6.283185307 * kph) * kEnv * 0.9f * s.drumVol; kEnv *= kDec;
                    }
                    // snare
                    if (sEnv > 0.001f)
                    {
                        sph += 190.0 / sr; if (sph >= 1.0) sph -= 1.0;
                        m += (Rnd() * 0.6f + (float)Math.Sin(6.283185307 * sph) * 0.4f) * sEnv * 0.5f * s.drumVol; sEnv *= sDec;
                    }
                    // hat
                    if (hEnv > 0.001f) { float r = Rnd(); m += (r - hPrev) * hEnv * 0.22f * s.drumVol; hPrev = r; hEnv *= hDec; }
                    // taiko
                    if (tEnv > 0.001f)
                    {
                        tph += (52f + 50f * tEnv * tEnv) / sr; if (tph >= 1.0) tph -= 1.0;
                        m += ((float)Math.Sin(6.283185307 * tph) * 0.9f + Rnd() * 0.12f * tEnv) * tEnv * 0.9f * s.drumVol; tEnv *= tDec;
                    }
                    if (rEnv > 0.001f)
                    {
                        rph += 1050.0 / sr; if (rph >= 1.0) rph -= 1.0;
                        m += (float)Math.Sin(6.283185307 * rph) * rEnv * 0.35f * s.drumVol; rEnv *= rDec;
                    }
                    // lead
                    if (lEnv > 0.001f && lf > 0f)
                    {
                        lph += lf / sr; if (lph >= 1.0) lph -= 1.0;
                        float x;
                        if (s.leadWave == 1)
                        {   // bell
                            x = (float)(Math.Sin(6.283185307 * lph) + 0.45 * Math.Sin(6.283185307 * lph * 2.76) + 0.2 * Math.Sin(6.283185307 * lph * 5.4)) * 0.45f;
                        }
                        else
                        {   // plucked saw (shamisen-like on wave 2 with a noise attack)
                            float saw = (float)(2.0 * lph - 1.0);
                            llp += (saw - llp) * (0.06f + 0.5f * lEnv);
                            x = llp * 0.9f + (s.leadWave == 2 ? Rnd() * 0.25f * lEnv * lEnv * lEnv : 0f);
                        }
                        m += x * lEnv * s.leadVol; lEnv *= lDec;
                    }
                    // pad (slow triads)
                    if (s.padVol > 0f)
                    {
                        pEnv += (1f - pEnv) * 0.00004f;
                        float p = 0f;
                        for (int v = 0; v < 3; v++)
                        {
                            pf[v] += (pft[v] - pf[v]) * 0.0015f;
                            pph[v] += pf[v] / sr; if (pph[v] >= 1.0) pph[v] -= 1.0;
                            p += (float)(Math.Abs(pph[v] * 4.0 - 2.0) - 1.0) * 0.5f;
                        }
                        m += p * pEnv * s.padVol;
                    }
                    m *= fade;
                }

                float sfx = 0f;
                for (int bi = 0; bi < NB; bi++)
                {
                    if (bDelay[bi] > 0) { bDelay[bi]--; continue; }
                    if (bAmp[bi] < 0.002f) continue;
                    float f = bFreq[bi];
                    bFreq[bi] += (bFreq2[bi] - f) * (float)(8.0 / sr * 4.0);
                    float o;
                    if (bWave[bi] == 2) o = Rnd();
                    else { bPh[bi] += f / sr; if (bPh[bi] >= 1.0) bPh[bi] -= 1.0; o = Wave(bWave[bi], bPh[bi]); }
                    sfx += o * bAmp[bi]; bAmp[bi] *= bDec[bi];
                }
                // kite call: ピーヒョロロロ... rising "pii" then a falling trill
                if (callT >= 0.0)
                {
                    double t = callT; callT += 1.0 / sr;
                    float f; float a;
                    if (t < 0.30) { double u = t / 0.30; f = (float)(1700.0 + 1400.0 * Math.Pow(u, 1.3)); a = (float)Math.Min(1.0, t / 0.03) * (u > 0.85 ? (float)((1.0 - u) / 0.15) : 1f); }
                    else if (t < 0.38) { f = 3100f; a = 0f; }
                    else if (t < 1.6)
                    {
                        double u = (t - 0.38) / 1.22;
                        f = (float)(2650.0 - 800.0 * u + 280.0 * Math.Sin(6.283185307 * 17.0 * (t - 0.38)));
                        a = (float)Math.Min(1.0, (t - 0.38) / 0.04) * (float)Math.Pow(1.0 - u, 0.7);
                    }
                    else { f = 0f; a = 0f; callT = -1.0; }
                    if (f > 0f)
                    {
                        cph += f / sr; if (cph >= 1.0) cph -= 1.0;
                        sfx += ((float)Math.Sin(6.283185307 * cph) + 0.3f * (float)Math.Sin(12.566370614 * cph)) * a * 0.32f;
                    }
                }
                if (thunder > 0.001f) { sfx += Rnd() * thunder * 0.5f; thunder *= 0.99995f; }

                // wind while flying (louder + brighter in a dive)
                float wt = live * (0.02f + 0.22f * spd * spd + 0.25f * dive);
                windAmp += (wt - windAmp) * 0.0004f;
                windLp += (Rnd() - windLp) * (0.03f + 0.3f * spd);
                float wind = windLp * windAmp;

                float o2 = (float)Math.Tanh(m * mv + sfx * sv + wind * sv);
                for (int c = 0; c < channels; c++) data[i * channels + c] += o2;
            }
        }
        double cph;
    }
}
