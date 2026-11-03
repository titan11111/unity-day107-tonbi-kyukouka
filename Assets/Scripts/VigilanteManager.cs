using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// 自警団. Every grab raises GameManager.heat (+1, decays 0.04/s). Active vigilantes = floor(heat/1.5) (+1 after 50 s), max 3.
    /// They sprint toward the bird and throw lead-aimed nets (speed 30) when it is within 28-36 m and below 40 m. Cooldown shrinks with heat.
    /// </summary>
    public class VigilanteManager : MonoBehaviour
    {
        public List<Transform> vigs = new List<Transform>();
        class Net { public Transform t; public Vector3 v; public float life; }
        readonly List<Net> nets = new List<Net>(); readonly float[] cool = new float[3]; readonly bool[] act = new bool[3];

        public int ActiveCount { get { var gm = GameManager.Instance; return gm && gm.playing ? Mathf.Min(vigs.Count, Mathf.FloorToInt(gm.heat / 1.5f) + (gm.elapsed > 50 ? 1 : 0)) : 0; } }

        void Update()
        {
            var gm = GameManager.Instance; if (gm == null || gm.bird == null) return;
            float dt = gm.SimDt; int count = ActiveCount; Vector3 bp = gm.bird.transform.position; float hk = Mathf.Min(1, gm.heat / 3f);
            for (int i = 0; i < vigs.Count && i < 3; i++)
            {
                var v = vigs[i]; bool want = i < count;
                if (want && !act[i]) { act[i] = true; v.gameObject.SetActive(true); v.position = new Vector3(Random.Range(-140, 140), 0, 125); cool[i] = 1.5f; gm.Toast("🚨自警団が出動！", "盗むほど追われる"); }
                if (!want && act[i]) { act[i] = false; v.gameObject.SetActive(false); }
                if (!act[i]) continue;
                Vector3 p = v.position; Vector3 to = new Vector3(bp.x - p.x, 0, bp.z - p.z); float hd = to.magnitude;
                if (hd > 6f) p += to / hd * ((9f + 2.5f * hk) * dt);
                p.x = Mathf.Clamp(p.x, -165, 165); p.z = Mathf.Clamp(p.z, 12, 146); p.y = 0; v.position = p;
                v.rotation = Quaternion.Euler(0, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0);
                float d3 = Vector3.Distance(new Vector3(p.x, 1.6f, p.z), bp); cool[i] -= dt;
                if (d3 < 28 + 8 * hk && bp.y < 40 && cool[i] <= 0) { Throw(p + Vector3.up * 1.8f, gm); cool[i] = 3.4f - 0.9f * hk; }
            }
            for (int i = nets.Count - 1; i >= 0; i--)
            {
                var n = nets[i]; n.life -= dt; n.t.position += n.v * dt; n.t.Rotate(200 * dt, 120 * dt, 0);
                if (gm.playing && gm.bird.invuln <= 0 && Vector3.Distance(n.t.position, bp) < 2.6f) { gm.HitBird("網をくらった！", "自警団に狙われてる"); n.life = 0; }
                if (n.life <= 0) { Destroy(n.t.gameObject); nets.RemoveAt(i); }
            }
        }

        void Throw(Vector3 from, GameManager gm)
        {
            var bird = gm.bird; Vector3 bp = bird.transform.position; float d = Vector3.Distance(from, bp);
            Vector3 aim = bp + BirdController.Forward(bird.yaw, bird.pitch) * (bird.v * d / 30f);   // lead aim
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>()); g.transform.position = from; g.transform.localScale = Vector3.one * 2.2f;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(.95f, .95f, .8f, .55f) }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nets.Add(new Net { t = g.transform, v = (aim - from).normalized * 30f, life = 2.5f });
        }
    }
}
