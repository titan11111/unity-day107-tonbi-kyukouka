using UnityEngine;

namespace Tonbi
{
    /// <summary>
    /// Milestone-2 flavor: a rival crow (カラス) that occasionally swoops in from off-screen toward whichever
    /// snack is closest to the player, sometimes beats them to it, then leaves. Pure scripted flight path
    /// (no physics/input), reuses HawkModel for the visual and GameManager.RivalSteal for a consistent snatch.
    /// </summary>
    public class RivalBird : MonoBehaviour
    {
        enum M { Hidden, Approach, Swoop, Leave }
        M m = M.Hidden;
        float cooldown, t;
        Vector3 from, to, ctrl;
        HawkModel model;
        HumanAI target;

        void Start()
        {
            model = gameObject.AddComponent<HawkModel>();
            model.Build(HawkPalette.Crow());
            transform.localScale = Vector3.one * 1.0f;
            gameObject.SetActive(false);
            cooldown = Random.Range(14f, 26f);
        }

        void Update()
        {
            var gm = GameManager.Instance; if (gm == null) return;
            if (!gm.playing) { if (gameObject.activeSelf) gameObject.SetActive(false); m = M.Hidden; return; }
            float dt = gm.SimDt > 0f ? gm.SimDt : Time.deltaTime;

            if (m == M.Hidden) { cooldown -= dt; if (cooldown <= 0f) TrySpawn(gm); return; }

            t += dt;
            switch (m)
            {
                case M.Approach:
                    {
                        float k = Mathf.Clamp01(t / 2.2f);
                        transform.position = Bezier(from, ctrl, to, k);
                        FacePath(k);
                        if (k >= 1f)
                        {
                            if (target != null && target.HasFood && Random.value < 0.55f) gm.RivalSteal(target);
                            m = M.Swoop; t = 0f;
                        }
                        break;
                    }
                case M.Swoop:
                    if (t >= 0.4f)
                    {
                        m = M.Leave; t = 0f; Vector3 p = transform.position;
                        from = p; to = p + new Vector3(Random.Range(-60f, 60f), 70f, Random.Range(40f, 90f)); ctrl = (from + to) * 0.5f + Vector3.up * 30f;
                    }
                    break;
                case M.Leave:
                    {
                        float k = Mathf.Clamp01(t / 1.8f);
                        transform.position = Bezier(from, ctrl, to, k);
                        FacePath(k);
                        if (k >= 1f) { gameObject.SetActive(false); m = M.Hidden; cooldown = Random.Range(18f, 32f); }
                        break;
                    }
            }
            if (model) model.Pose(dt, 0f, false, 0f, 1f, false);
        }

        void TrySpawn(GameManager gm)
        {
            HumanAI best = null; float bd = 1e9f;
            foreach (var h in gm.humans)
            {
                if (!h.HasFood) continue;
                float d = Vector3.Distance(h.transform.position, gm.bird.transform.position);
                if (d < bd) { bd = d; best = h; }
            }
            if (best == null) { cooldown = Random.Range(8f, 14f); return; }

            target = best;
            to = target.FoodPos + Vector3.up * 1.5f;
            Vector3 side = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 90f;
            from = to + side + Vector3.up * Random.Range(30f, 55f);
            ctrl = Vector3.Lerp(from, to, 0.5f) + Vector3.up * 20f;
            transform.position = from;
            gameObject.SetActive(true); m = M.Approach; t = 0f;
            gm.Toast("ライバル出現！", "カラスも狙ってる…");
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float u) { float v = 1f - u; return v * v * a + 2f * v * u * b + u * u * c; }
        void FacePath(float k)
        {
            Vector3 d = Bezier(from, ctrl, to, Mathf.Min(1f, k + 0.02f)) - transform.position;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
