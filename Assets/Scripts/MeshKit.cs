using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    /// <summary>Elliptical cross-section used by MeshKit.Loft.</summary>
    public struct Ring
    {
        public Vector3 c; public float a, b; public Color top, bot;
        public Ring(Vector3 c, float a, float b, Color top) { this.c = c; this.a = a; this.b = b; this.top = top; this.bot = top; }
        public Ring(Vector3 c, float a, float b, Color top, Color bot) { this.c = c; this.a = a; this.b = b; this.top = top; this.bot = bot; }
    }

    /// <summary>Tiny procedural mesh builder with vertex colours. Winding is fixed automatically from an "outward" hint vector.</summary>
    public class MeshKit
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Color> col = new List<Color>();
        public readonly List<int> tri = new List<int>();

        public delegate Vector3 Pt(float u, float w);
        public delegate Color Cf(bool top, float u, float w);

        public int V(Vector3 p, Color c) { v.Add(p); col.Add(c); return v.Count - 1; }

        public void Tri(int a, int b, int d, Vector3 hint)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
            if (Vector3.Dot(n, hint) < 0f) { int tmp = b; b = d; d = tmp; }
            tri.Add(a); tri.Add(b); tri.Add(d);
        }
        public void Quad(int a, int b, int c, int d, Vector3 hint) { Tri(a, b, c, hint); Tri(a, c, d, hint); }

        /// <summary>axis 0: rings stacked along Y (ellipse in XZ). axis 1: stacked along Z (ellipse in XY, "top" colour on +Y).</summary>
        public void Loft(Ring[] r, int axis, int seg, Matrix4x4 xf)
        {
            int n = r.Length;
            int[,] idx = new int[n, seg];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < seg; k++)
                {
                    float th = Mathf.PI * 2f * k / seg, cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                    Vector3 off = axis == 0 ? new Vector3(cs * r[i].a, 0f, sn * r[i].b) : new Vector3(cs * r[i].a, sn * r[i].b, 0f);
                    Color c = Color.Lerp(r[i].bot, r[i].top, Mathf.Clamp01(sn * 1.2f + 0.5f));
                    idx[i, k] = V(xf.MultiplyPoint3x4(r[i].c + off), c);
                }
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int k2 = (k + 1) % seg;
                    Vector3 mid = (v[idx[i, k]] + v[idx[i + 1, k2]]) * 0.5f - xf.MultiplyPoint3x4((r[i].c + r[i + 1].c) * 0.5f);
                    Quad(idx[i, k], idx[i, k2], idx[i + 1, k2], idx[i + 1, k], mid);
                }
            Cap(r[0], idx, 0, seg, xf, r[0].c - r[1].c);
            Cap(r[n - 1], idx, n - 1, seg, xf, r[n - 1].c - r[n - 2].c);
        }

        void Cap(Ring r, int[,] idx, int row, int seg, Matrix4x4 xf, Vector3 dir)
        {
            int cen = V(xf.MultiplyPoint3x4(r.c), r.top);
            Vector3 hint = xf.MultiplyVector(dir);
            for (int k = 0; k < seg; k++) Tri(cen, idx[row, k], idx[row, (k + 1) % seg], hint);
        }

        public void Ellipsoid(Vector3 center, Vector3 radii, Color c, Matrix4x4 xf, int rings = 7, int seg = 12)
        {
            Ring[] r = new Ring[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float ph = Mathf.PI * i / rings;
                r[i] = new Ring(new Vector3(0f, Mathf.Cos(ph) * radii.y, 0f), Mathf.Sin(ph) * radii.x, Mathf.Sin(ph) * radii.z, c);
            }
            // rings run from +Y (top) to -Y, so reverse to keep "bottom to top" order
            System.Array.Reverse(r);
            Loft(r, 0, seg, xf * Matrix4x4.Translate(center));
        }

        /// <summary>Thin double-sided lens surface. mid(u,w): centre-line point, half(u,w): half thickness, col: vertex colour.</summary>
        public void Panel(int nu, int nw, Pt mid, System.Func<float, float, float> half, Cf colf, Matrix4x4 xf, float under = 0.6f)
        {
            int[,] tp = new int[nu + 1, nw + 1];
            int[,] bt = new int[nu + 1, nw + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nw; j++)
                {
                    float u = i / (float)nu, w = j / (float)nw;
                    Vector3 m = mid(u, w);
                    float h = half(u, w);
                    tp[i, j] = V(xf.MultiplyPoint3x4(m + Vector3.up * h), colf(true, u, w));
                    bt[i, j] = V(xf.MultiplyPoint3x4(m - Vector3.up * h * under), colf(false, u, w));
                }
            Vector3 up = xf.MultiplyVector(Vector3.up);
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nw; j++)
                {
                    Quad(tp[i, j], tp[i + 1, j], tp[i + 1, j + 1], tp[i, j + 1], up);
                    Quad(bt[i, j], bt[i + 1, j], bt[i + 1, j + 1], bt[i, j + 1], -up);
                }
        }

        public void Box(Vector3 ctr, Vector3 sz, Color c, Matrix4x4 xf)
        {
            Vector3 h = sz * 0.5f;
            for (int ax = 0; ax < 3; ax++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    int u = (ax + 1) % 3, w = (ax + 2) % 3;
                    Vector3 nrm = Vector3.zero; nrm[ax] = sg;
                    Vector3 du = Vector3.zero; du[u] = h[u];
                    Vector3 dw = Vector3.zero; dw[w] = h[w];
                    Vector3 cc = ctr; cc[ax] += sg * h[ax];
                    int a = V(xf.MultiplyPoint3x4(cc - du - dw), c);
                    int b = V(xf.MultiplyPoint3x4(cc + du - dw), c);
                    int d2 = V(xf.MultiplyPoint3x4(cc + du + dw), c);
                    int e = V(xf.MultiplyPoint3x4(cc - du + dw), c);
                    Quad(a, b, d2, e, xf.MultiplyVector(nrm));
                }
        }

        /// <summary>Flat quad from four corners (any order around the perimeter).</summary>
        public void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colr, Vector3 hint)
        {
            int ia = V(a, colr), ib = V(b, colr), ic = V(c, colr), id = V(d, colr);
            Quad(ia, ib, ic, id, hint);
        }

        public Mesh ToMesh(string name)
        {
            Mesh m = new Mesh();
            m.name = name;
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject Make(string name, Transform parent, MeshKit k, Material mat)
        {
            GameObject go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            if (k != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = k.ToMesh(name);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go;
        }
    }

    /// <summary>Shared materials built from the shaders in Resources/TonbiShaders.</summary>
    public static class TonbiMat
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader Sh(string n, string fallback)
        {
            Shader s = Resources.Load<Shader>("TonbiShaders/" + n);
            if (s == null) s = Shader.Find(fallback);
            if (s == null) s = Shader.Find("Standard");
            return s;
        }
        public static Material Lit(float smooth = 0.2f)
        {
            string key = "lit" + smooth.ToString("0.00");
            Material m;
            if (cache.TryGetValue(key, out m) && m) return m;
            m = new Material(Sh("VertexLit", "Standard")); m.name = key;
            if (m.HasProperty("_Smooth")) m.SetFloat("_Smooth", smooth);
            cache[key] = m; return m;
        }
        public static Material Unlit()
        {
            Material m;
            if (cache.TryGetValue("unlit", out m) && m) return m;
            m = new Material(Sh("VertexUnlit", "Sprites/Default")); m.name = "unlit"; cache["unlit"] = m; return m;
        }
        public static Material Sand() { return Named("sand", "Sand", "Standard", new Color(.86f, .78f, .62f)); }
        public static Material Sea() { return Named("sea", "Sea", "Standard", new Color(.1f, .62f, .78f)); }
        public static Material Sky() { return Named("sky", "Sky", "Sprites/Default", Color.cyan); }
        static Material Named(string key, string shader, string fallback, Color c)
        {
            Material m;
            if (cache.TryGetValue(key, out m) && m) return m;
            m = new Material(Sh(shader, fallback)); m.name = key; m.color = c; cache[key] = m; return m;
        }
        public static Material Plain(Color c, float smooth = 0.3f)
        {
            Material m = new Material(Shader.Find("Standard")); m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }
    }
}
