using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// Volumetric-looking cumulus: each cloud = ~11 soft camera-facing puffs (SpriteRenderer) in a flat-bottomed ellipsoid.
    /// Underside is grey-blue, top is white. Drifts with the wind, tinted by WeatherSystem.Dark (grey in rain). Plus a horizon cloud bank.
    /// </summary>
    public class CloudField : MonoBehaviour
    {
        public int clouds = 14, bankClouds = 16; public float drift = 2.2f;
        class Puff { public Transform t; public SpriteRenderer sr; public int layer; }
        readonly List<Puff> puffs = new List<Puff>(); readonly List<Transform> roots = new List<Transform>();
        Sprite sprite; float tintT;
        static readonly Color[] baseC = { new Color(.66f, .72f, .8f), new Color(.86f, .9f, .94f), Color.white };
        static readonly Color[] darkC = { new Color(.36f, .4f, .46f), new Color(.48f, .53f, .58f), new Color(.6f, .64f, .69f) };

        Sprite MakePuff()
        {
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
            var rnd = new System.Random(7); var cs = new Vector3[9];
            for (int i = 0; i < 9; i++) cs[i] = new Vector3(64 + (float)(rnd.NextDouble() - .5) * 46, 64 + (float)(rnd.NextDouble() - .5) * 40, 22 + (float)rnd.NextDouble() * 26);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float a = 0; foreach (var c in cs) { float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y)) / c.z; if (d < 1) a += 0.55f * (1 - d) * (1 - d * 0.35f); }
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
            }
            tex.SetPixels(px); tex.Apply(); return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
        }

        void Awake()
        {
            sprite = MakePuff();
            for (int i = 0; i < clouds; i++) Make(new Vector3(Random.Range(-800, 800), Random.Range(100, 190), Random.Range(-700, 600)), Random.Range(110, 190), Random.Range(26, 42), Random.Range(50, 80), 11, Random.Range(42, 62));
            for (int i = 0; i < bankClouds; i++) { float a = i / (float)bankClouds * Mathf.PI * 2 + Random.Range(-.1f, .1f); Make(new Vector3(Mathf.Cos(a) * 1600, Random.Range(40, 110), Mathf.Sin(a) * 1600), 420, 70, 120, 8, 190); }
        }

        void Make(Vector3 pos, float W, float H, float D, int n, float sz)
        {
            var root = new GameObject("Cloud").transform; root.SetParent(transform); root.position = pos; roots.Add(root);
            for (int k = 0; k < n; k++)
            {
                float u = Random.value * 2 - 1, v = Random.value, w = Random.value * 2 - 1, edge = 1 - Mathf.Min(1, Mathf.Sqrt(u * u + w * w * .49f));
                float hy = v * H * (.35f + .65f * edge); int layer = hy / H < .28f ? 0 : hy / H < .62f ? 1 : 2;
                var go = new GameObject("Puff"); go.transform.SetParent(root, false); go.transform.localPosition = new Vector3(u * W / 2, hy, w * D / 2);
                float size = sz * (.55f + .7f * edge) * Random.Range(.8f, 1.2f); go.transform.localScale = new Vector3(size * 1.25f, size * .85f, 1);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = baseC[layer]; puffs.Add(new Puff { t = go.transform, sr = sr, layer = layer });
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            Quaternion q = cam.transform.rotation; foreach (var p in puffs) p.t.rotation = q;
            Vector3 wind = WeatherSystem.WindVec * 0.6f + new Vector3(drift, 0, 0);
            foreach (var r in roots) { var pos = r.position + wind * Time.deltaTime; if (pos.x > 1900) pos.x = -1900; if (pos.x < -1900) pos.x = 1900; if (pos.z > 1900) pos.z = -1900; if (pos.z < -1900) pos.z = 1900; r.position = pos; }
            tintT -= Time.deltaTime; if (tintT <= 0) { tintT = 0.2f; float d = WeatherSystem.Dark; foreach (var p in puffs) p.sr.color = Color.Lerp(baseC[p.layer], darkC[p.layer], d); }
        }
    }
}
