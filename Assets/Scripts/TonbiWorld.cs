using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    /// <summary>Gradient sky dome that follows the camera. Colours are driven by WeatherSystem.</summary>
    public class SkyDome : MonoBehaviour
    {
        public static SkyDome Instance;
        Material mat; Transform cam;

        public static SkyDome Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SkyDome";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 3600f;
            SkyDome d = go.AddComponent<SkyDome>();
            d.mat = new Material(TonbiMat.Sky());
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = d.mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            Instance = d;
            return d;
        }

        public void Apply(Color top, Color hor, Color bot, Vector3 sunDir, Color sunCol, float night)
        {
            if (mat == null) return;
            mat.SetColor("_Top", top); mat.SetColor("_Hor", hor); mat.SetColor("_Bot", bot);
            mat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            mat.SetColor("_SunCol", sunCol); mat.SetFloat("_Night", night);
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main) cam = Camera.main.transform;
            if (cam) transform.position = cam.position;
        }
    }

    /// <summary>Small sine bob for swimmers / buoys.</summary>
    public class Bobber : MonoBehaviour
    {
        float y0, ph; public float amp = 0.25f;
        void Start() { y0 = transform.position.y; ph = Random.value * 6.28f; }
        void Update()
        {
            Vector3 p = transform.position; p.y = y0 + Mathf.Sin(Time.time * 1.4f + ph) * amp; transform.position = p;
            transform.Rotate(0f, 8f * Time.deltaTime, 0f);
        }
    }

    /// <summary>Builds the entire playable world at runtime (no scene editing needed): sea, sand, sky, beach props, town, trees, people, animals.</summary>
    public static class TonbiWorld
    {
        public static Vector3 PerchPos = new Vector3(135f, 33f, 118f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            Build();
        }

        static Material Lit { get { return TonbiMat.Lit(0.2f); } }
        static Color RandCol(System.Random r, float s, float v) { return Color.HSVToRGB((float)r.NextDouble(), s, v); }

        public static void Build()
        {
            System.Random rng = new System.Random(1107);
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 230f;
            QualitySettings.antiAliasing = 4;

            // clean the default template objects
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.gameObject.name != "Main Camera") Object.Destroy(c.gameObject);
            Camera cam = Camera.main;
            if (cam == null) { GameObject cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            else if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.6f, .8f, .9f);
            cam.farClipPlane = 3000f; cam.nearClipPlane = 0.5f; cam.fieldOfView = 68f;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.Destroy(l.gameObject);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.62f, .7f, .78f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.8f, .9f, .95f);
            RenderSettings.fogStartDistance = 260f; RenderSettings.fogEndDistance = 1600f;

            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, .95f, .82f); sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.62f, -0.45f));

            SkyDome.Create();
            Transform world = new GameObject("World").transform;

            BuildGround(world);
            BuildBeachProps(world, rng);
            BuildTown(world, rng);
            BuildTrees(world, rng);
            BuildScenery(world);
            BuildSnag(world);
            BuildThermals(world);

            // bird
            GameObject bird = new GameObject("Tonbi");
            BirdController bc = bird.AddComponent<BirdController>();
            HawkModel hm = bird.AddComponent<HawkModel>();
            hm.Build(HawkPalette.Kite());
            bc.model = hm; bc.visual = bird.transform;
            bird.transform.localScale = Vector3.one * 1.35f;
            bird.transform.position = PerchPos;

            CameraRig rig = cam.gameObject.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            if (cam.gameObject.GetComponent<TonbiMusic>() == null) cam.gameObject.AddComponent<TonbiMusic>();

            GameObject gmGo = new GameObject("GameManager");
            GameManager gm = gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<GameHud>();
            gm.bird = bc; gm.cam = rig; gm.sun = sun;

            BuildPeople(world, gm, rng);
            BuildAnimals(world, rng);
            BuildVigilantes();

            GameObject wx = new GameObject("Weather");
            WeatherSystem ws = wx.AddComponent<WeatherSystem>(); ws.sun = sun;
            new GameObject("Clouds").AddComponent<CloudField>();
        }

        // ------------------------------------------------------------ ground / sea
        static void BuildGround(Transform world)
        {
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Sea"; Object.Destroy(sea.GetComponent<Collider>());
            sea.transform.SetParent(world, false); sea.transform.position = new Vector3(0f, -0.05f, -1500f); sea.transform.localScale = new Vector3(400f, 1f, 300f);
            sea.GetComponent<MeshRenderer>().sharedMaterial = TonbiMat.Sea();
            GameObject sand = GameObject.CreatePrimitive(PrimitiveType.Plane); sand.name = "Sand"; Object.Destroy(sand.GetComponent<Collider>());
            sand.transform.SetParent(world, false); sand.transform.position = new Vector3(0f, 0f, 250f); sand.transform.localScale = new Vector3(400f, 1f, 50f);
            sand.GetComponent<MeshRenderer>().sharedMaterial = TonbiMat.Sand();

            // promenade + sea wall + road with lane markings
            MeshKit k = new MeshKit();
            Color asphalt = new Color(.30f, .31f, .33f);
            k.Box(new Vector3(0f, 0.05f, 186f), new Vector3(2000f, 0.1f, 40f), asphalt, Matrix4x4.identity);
            k.Box(new Vector3(0f, 0.04f, 155f), new Vector3(2000f, 0.08f, 10f), new Color(.72f, .68f, .6f), Matrix4x4.identity);
            k.Box(new Vector3(0f, 0.55f, 150.3f), new Vector3(2000f, 1.1f, 1.2f), new Color(.78f, .76f, .72f), Matrix4x4.identity);
            for (float x = -950f; x < 950f; x += 14f) k.Box(new Vector3(x, 0.11f, 186f), new Vector3(6f, 0.02f, 0.5f), new Color(.95f, .95f, .85f), Matrix4x4.identity);
            MeshKit.Make("Road", world, k, TonbiMat.Lit(0.1f));
        }

        // ------------------------------------------------------------ beach props
        static void BuildBeachProps(Transform world, System.Random rng)
        {
            MeshKit k = new MeshKit();
            Material lit = Lit;
            // striped parasols + towels
            for (int i = 0; i < 34; i++)
            {
                float x = (float)(rng.NextDouble() * 380 - 190), z = (float)(22 + rng.NextDouble() * 118);
                Color c1 = RandCol(rng, .75f, 1f); Color c2 = Color.white;
                k.Loft(new Ring[] { new Ring(new Vector3(x, 0f, z), .08f, .08f, Color.white), new Ring(new Vector3(x, 3.3f, z), .07f, .07f, Color.white) }, 0, 6, Matrix4x4.identity);
                int wedges = 8;
                for (int w = 0; w < wedges; w++)
                {
                    float a0 = Mathf.PI * 2f * w / wedges, a1 = Mathf.PI * 2f * (w + 1) / wedges;
                    Color c = (w % 2 == 0) ? c1 : c2;
                    Vector3 apex = new Vector3(x, 4.0f, z);
                    Vector3 p0 = new Vector3(x + Mathf.Cos(a0) * 2.7f, 3.2f, z + Mathf.Sin(a0) * 2.7f);
                    Vector3 p1 = new Vector3(x + Mathf.Cos(a1) * 2.7f, 3.2f, z + Mathf.Sin(a1) * 2.7f);
                    int ia = k.V(apex, c), ib = k.V(p0, c), ic = k.V(p1, c);
                    k.Tri(ia, ib, ic, Vector3.up);
                    int ja = k.V(apex - Vector3.up * 0.05f, Color.Lerp(c, Color.black, .25f)), jb = k.V(p0 - Vector3.up * 0.05f, Color.Lerp(c, Color.black, .25f)), jc = k.V(p1 - Vector3.up * 0.05f, Color.Lerp(c, Color.black, .25f));
                    k.Tri(ja, jb, jc, Vector3.down);
                }
                // towel
                float tx = x + (float)(rng.NextDouble() * 6 - 3), tz = z + (float)(rng.NextDouble() * 4 + 2);
                Color t1 = RandCol(rng, .6f, 1f);
                for (int s = 0; s < 5; s++)
                {
                    Color tc = (s % 2 == 0) ? t1 : Color.white;
                    k.Flat(new Vector3(tx - 1.0f + s * 0.4f, 0.04f, tz - 1.8f), new Vector3(tx - 0.6f + s * 0.4f, 0.04f, tz - 1.8f), new Vector3(tx - 0.6f + s * 0.4f, 0.04f, tz + 1.8f), new Vector3(tx - 1.0f + s * 0.4f, 0.04f, tz + 1.8f), tc, Vector3.up);
                }
            }
            // surfboards stuck upright in the sand
            for (int i = 0; i < 9; i++)
            {
                float x = (float)(rng.NextDouble() * 360 - 180), z = (float)(16 + rng.NextDouble() * 20);
                Color bc = RandCol(rng, .7f, 1f);
                Matrix4x4 xf = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(8f, (float)rng.NextDouble() * 360f, 0f), Vector3.one);
                k.Loft(new Ring[] { new Ring(new Vector3(0f, 0f, 0f), .02f, .01f, bc), new Ring(new Vector3(0f, .3f, 0f), .28f, .03f, bc), new Ring(new Vector3(0f, 1.6f, 0f), .30f, .03f, Color.Lerp(bc, Color.white, .6f)),
                    new Ring(new Vector3(0f, 2.6f, 0f), .22f, .025f, bc), new Ring(new Vector3(0f, 3.1f, 0f), .02f, .01f, bc) }, 0, 8, xf);
            }
            // beach balls
            for (int i = 0; i < 8; i++)
            {
                float x = (float)(rng.NextDouble() * 360 - 180), z = (float)(14 + rng.NextDouble() * 120);
                Color a = RandCol(rng, .8f, 1f);
                k.Ellipsoid(new Vector3(x, 0.5f, z), new Vector3(.5f, .5f, .5f), a, Matrix4x4.identity, 6, 10);
                k.Ellipsoid(new Vector3(x, 0.5f, z) + new Vector3(0f, 0.42f, 0f), new Vector3(.2f, .12f, .2f), Color.white, Matrix4x4.identity, 3, 8);
            }
            // sandcastles
            for (int i = 0; i < 5; i++)
            {
                float x = (float)(rng.NextDouble() * 300 - 150), z = (float)(20 + rng.NextDouble() * 60);
                Color sc = new Color(.82f, .72f, .52f);
                k.Loft(new Ring[] { new Ring(new Vector3(x, 0f, z), 1.4f, 1.4f, sc), new Ring(new Vector3(x, 0.9f, z), 1.2f, 1.2f, sc), new Ring(new Vector3(x, 1.0f, z), 0.9f, 0.9f, sc), new Ring(new Vector3(x, 1.6f, z), .8f, .8f, sc), new Ring(new Vector3(x, 2.6f, z), .05f, .05f, new Color(.95f, .4f, .4f)) }, 0, 10, Matrix4x4.identity);
                for (int t = 0; t < 4; t++)
                {
                    float a = t * Mathf.PI * 0.5f + 0.78f;
                    Vector3 tp = new Vector3(x + Mathf.Cos(a) * 1.6f, 0f, z + Mathf.Sin(a) * 1.6f);
                    k.Loft(new Ring[] { new Ring(tp, .45f, .45f, sc), new Ring(tp + Vector3.up * 1.0f, .4f, .4f, sc), new Ring(tp + Vector3.up * 1.6f, .03f, .03f, sc) }, 0, 8, Matrix4x4.identity);
                }
            }
            // rocks, shells and driftwood
            for (int i = 0; i < 40; i++)
            {
                float x = (float)(rng.NextDouble() * 380 - 190), z = (float)(2 + rng.NextDouble() * 150);
                float s = i < 14 ? (float)(0.5 + rng.NextDouble() * 1.6) : 0.18f;
                Color rc = i < 14 ? new Color(.42f, .40f, .38f) : new Color(.95f, .85f, .8f);
                k.Ellipsoid(new Vector3(x, s * 0.35f, z), new Vector3(s, s * 0.6f, s * 0.8f), rc, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one), 4, 8);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = (float)(rng.NextDouble() * 360 - 180), z = (float)(6 + rng.NextDouble() * 30);
                Matrix4x4 xf = Matrix4x4.TRS(new Vector3(x, 0.3f, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 90f), Vector3.one);
                k.Loft(new Ring[] { new Ring(new Vector3(0f, 0f, 0f), .1f, .1f, new Color(.35f, .27f, .2f)), new Ring(new Vector3(0f, 1.2f, 0f), .14f, .12f, new Color(.4f, .32f, .25f)), new Ring(new Vector3(0f, 2.6f, 0f), .06f, .06f, new Color(.33f, .26f, .2f)) }, 0, 6, xf);
            }
            // lifeguard flag poles
            for (int i = 0; i < 3; i++)
            {
                float x = -60f + i * 70f, z = 24f;
                k.Loft(new Ring[] { new Ring(new Vector3(x, 0f, z), .06f, .06f, Color.white), new Ring(new Vector3(x, 4.5f, z), .05f, .05f, Color.white) }, 0, 5, Matrix4x4.identity);
                k.Flat(new Vector3(x, 4.5f, z), new Vector3(x + 1.6f, 4.3f, z), new Vector3(x + 1.6f, 3.4f, z), new Vector3(x, 3.5f, z), i == 1 ? new Color(.9f, .15f, .1f) : new Color(.95f, .8f, .1f), Vector3.back);
                k.Flat(new Vector3(x, 4.5f, z), new Vector3(x + 1.6f, 4.3f, z), new Vector3(x + 1.6f, 3.4f, z), new Vector3(x, 3.5f, z), i == 1 ? new Color(.9f, .15f, .1f) : new Color(.95f, .8f, .1f), Vector3.forward);
            }
            MeshKit.Make("BeachProps", world, k, lit);

            // lifeguard tower (collider proxy + visual)
            GameObject proxy = new GameObject("LifeguardTower");
            proxy.transform.SetParent(world, false); proxy.transform.position = new Vector3(40f, 3.35f, 40f); proxy.transform.localScale = new Vector3(4.8f, 6.7f, 4.8f);
            proxy.AddComponent<BuildingObstacle>();
            MeshKit tk = new MeshKit();
            Color wood = new Color(.9f, .88f, .8f), red = new Color(.85f, .15f, .12f);
            for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                tk.Box(new Vector3(40f + sx * 2.0f, 2.0f, 40f + sz * 2.0f), new Vector3(.3f, 4.0f, .3f), wood, Matrix4x4.identity);
            tk.Box(new Vector3(40f, 4.2f, 40f), new Vector3(4.8f, .3f, 4.8f), wood, Matrix4x4.identity);
            tk.Box(new Vector3(40f, 5.3f, 40f), new Vector3(4.2f, 1.9f, 4.2f), wood, Matrix4x4.identity);
            tk.Box(new Vector3(40f, 5.4f, 42.12f), new Vector3(3.4f, 1.0f, .06f), new Color(.2f, .35f, .45f), Matrix4x4.identity);
            tk.Box(new Vector3(40f, 6.8f, 40f), new Vector3(5.2f, .25f, 5.2f), red, Matrix4x4.identity);
            tk.Box(new Vector3(40f, 3.2f, 42.6f), new Vector3(.3f, 2.2f, 2.4f), wood, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one));
            MeshKit.Make("LifeguardVisual", world, tk, lit);
        }

        // ------------------------------------------------------------ town
        static void MakeBuilding(Transform world, System.Random rng, Vector3 groundPos, Vector3 size, Color wall, bool hotel)
        {
            GameObject proxy = new GameObject(hotel ? "Hotel" : "Building");
            proxy.transform.SetParent(world, false);
            proxy.transform.position = groundPos + Vector3.up * (size.y * 0.5f);
            proxy.transform.localScale = size;
            proxy.AddComponent<BuildingObstacle>();

            MeshKit k = new MeshKit();
            Vector3 c = proxy.transform.position;
            k.Box(c, size, wall, Matrix4x4.identity);
            k.Box(c + new Vector3(0f, size.y * 0.5f + 0.4f, 0f), new Vector3(size.x + 0.6f, 0.8f, size.z + 0.6f), Color.Lerp(wall, Color.gray, .4f), Matrix4x4.identity);   // parapet
            // windows on the sea-facing (-Z) side and the two long sides
            float floorH = 3.2f; int floors = Mathf.Max(2, Mathf.FloorToInt((size.y - 3f) / floorH));
            int cols = Mathf.Max(2, Mathf.FloorToInt(size.x / 3.5f));
            Color glass = new Color(.18f, .30f, .42f);
            for (int f = 0; f < floors; f++)
                for (int cI = 0; cI < cols; cI++)
                {
                    float wx = c.x - size.x * 0.5f + (cI + 0.5f) * size.x / cols;
                    float wy = groundPos.y + 3f + f * floorH;
                    k.Box(new Vector3(wx, wy, c.z - size.z * 0.5f - 0.03f), new Vector3(1.6f, 1.7f, 0.08f), glass, Matrix4x4.identity);
                    if (hotel) k.Box(new Vector3(wx, wy - 0.95f, c.z - size.z * 0.5f - 0.35f), new Vector3(2.4f, 0.12f, 0.7f), new Color(.9f, .9f, .88f), Matrix4x4.identity); // balcony
                }
            // rooftop clutter (AC units / water tank)
            k.Box(c + new Vector3(size.x * 0.25f, size.y * 0.5f + 1.1f, size.z * 0.2f), new Vector3(2.2f, 1.2f, 1.6f), new Color(.7f, .72f, .74f), Matrix4x4.identity);
            if (hotel) k.Loft(new Ring[] { new Ring(c + new Vector3(-size.x * 0.25f, size.y * 0.5f + 0.8f, 0f), 1.4f, 1.4f, new Color(.55f, .6f, .65f)), new Ring(c + new Vector3(-size.x * 0.25f, size.y * 0.5f + 3.2f, 0f), 1.4f, 1.4f, new Color(.55f, .6f, .65f)) }, 0, 10, Matrix4x4.identity);
            MeshKit.Make("Visual", world, k, Lit);

            // night glow windows (unlit, driven by global _TonbiGlow)
            MeshKit gk = new MeshKit();
            for (int f = 0; f < floors; f++)
                for (int cI = 0; cI < cols; cI++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    float wx = c.x - size.x * 0.5f + (cI + 0.5f) * size.x / cols;
                    float wy = groundPos.y + 3f + f * floorH;
                    gk.Box(new Vector3(wx, wy, c.z - size.z * 0.5f - 0.09f), new Vector3(1.3f, 1.3f, 0.04f), new Color(1f, .85f, .5f), Matrix4x4.identity);
                }
            MeshKit.Make("Glow", world, gk, TonbiMat.Unlit());
        }

        static void BuildTown(Transform world, System.Random rng)
        {
            for (int i = 0; i < 40; i++)
            {
                Vector3 size = new Vector3((float)(20 + rng.NextDouble() * 20), (float)(12 + rng.NextDouble() * 28), (float)(14 + rng.NextDouble() * 12));
                Vector3 gp = new Vector3((float)(rng.NextDouble() * 1040 - 520), 0f, (float)(214 + rng.NextDouble() * 56));
                MakeBuilding(world, rng, gp, size, Color.HSVToRGB((float)rng.NextDouble(), .12f, .95f), false);
            }
            for (int i = 0; i < 9; i++)
            {
                float h = (float)(22 + rng.NextDouble() * 36);
                Vector3 size = new Vector3((float)(24 + rng.NextDouble() * 10), h, (float)(16 + rng.NextDouble() * 6));
                Vector3 gp = new Vector3(-300f + i * 75f + (float)(rng.NextDouble() * 24 - 12), 0f, (float)(198 + rng.NextDouble() * 14));
                MakeBuilding(world, rng, gp, size, Color.HSVToRGB((float)rng.NextDouble(), .08f, .93f), true);
            }
        }

        // ------------------------------------------------------------ trees
        static void BuildTrees(Transform world, System.Random rng)
        {
            Material lit = Lit;
            Color trunkC = new Color(.35f, .24f, .15f), pineC = new Color(.14f, .36f, .20f), palmT = new Color(.52f, .40f, .27f), frond = new Color(.20f, .52f, .24f);
            for (float x = -340f; x <= 340f; x += 15f)
            {
                GameObject t = new GameObject("Pine"); t.transform.SetParent(world, false);
                t.transform.position = new Vector3(x + (float)(rng.NextDouble() * 8 - 4), 0f, 156f + (float)(rng.NextDouble() * 5 - 2));
                MeshKit k = new MeshKit();
                k.Loft(new Ring[] { new Ring(new Vector3(0f, 0f, 0f), .6f, .6f, trunkC), new Ring(new Vector3(0f, 6f, 0f), .35f, .35f, trunkC) }, 0, 8, Matrix4x4.identity);
                for (int l = 0; l < 4; l++)
                {
                    float y = 4f + l * 2.6f, r = 4.2f - l * 0.8f;
                    k.Loft(new Ring[] { new Ring(new Vector3(0f, y, 0f), r, r, Color.Lerp(pineC, Color.black, .15f)), new Ring(new Vector3(0f, y + 2.4f, 0f), 0.15f, 0.15f, pineC) }, 0, 10, Matrix4x4.identity);
                }
                MeshKit.Make("Mesh", t.transform, k, lit);
                TreeObstacle to = t.AddComponent<TreeObstacle>(); to.r = 3.4f; to.h = 13f;
            }
            for (float x = -330f; x <= 330f; x += 38f)
            {
                GameObject t = new GameObject("Palm"); t.transform.SetParent(world, false);
                t.transform.position = new Vector3(x + (float)(rng.NextDouble() * 12 - 6), 0f, 166f + (float)(rng.NextDouble() * 4 - 2));
                MeshKit k = new MeshKit();
                float lean = (float)(rng.NextDouble() * 1.6 - 0.8);
                Ring[] trunk = new Ring[6];
                for (int i = 0; i < 6; i++) { float h = i * 1.7f; trunk[i] = new Ring(new Vector3(lean * (h / 8.5f) * (h / 8.5f) * 2f, h, 0f), .5f - i * .04f, .5f - i * .04f, palmT); }
                k.Loft(trunk, 0, 8, Matrix4x4.identity);
                Vector3 top = trunk[5].c;
                for (int f = 0; f < 8; f++)
                {
                    float a = f * Mathf.PI * 2f / 8f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                    int nu = 5;
                    int[] l1 = new int[nu + 1], l2 = new int[nu + 1];
                    for (int u = 0; u <= nu; u++)
                    {
                        float uu = u / (float)nu; float wd = 0.9f * Mathf.Sin(uu * Mathf.PI * 0.9f + 0.15f);
                        Vector3 p = top + dir * (uu * 4.2f) + Vector3.up * (1.2f * Mathf.Sin(uu * Mathf.PI * 0.6f) - uu * uu * 1.6f);
                        l1[u] = k.V(p + side * wd, frond); l2[u] = k.V(p - side * wd, Color.Lerp(frond, Color.black, .2f));
                    }
                    for (int u = 0; u < nu; u++) { k.Quad(l1[u], l1[u + 1], l2[u + 1], l2[u], Vector3.up); k.Quad(l1[u], l1[u + 1], l2[u + 1], l2[u], Vector3.down); }
                }
                MeshKit.Make("Mesh", t.transform, k, lit);
                TreeObstacle to = t.AddComponent<TreeObstacle>(); to.r = 3f; to.h = 10f;
            }
        }

        static void BuildScenery(Transform world)
        {
            MeshKit k = new MeshKit();
            // Enoshima island (+ small lighthouse)
            Color grass = new Color(.26f, .46f, .27f), rock = new Color(.46f, .42f, .36f);
            k.Ellipsoid(new Vector3(-430f, 0f, -330f), new Vector3(120f, 60f, 90f), rock, Matrix4x4.identity, 8, 16);
            k.Ellipsoid(new Vector3(-430f, 25f, -330f), new Vector3(105f, 45f, 78f), grass, Matrix4x4.identity, 8, 16);
            k.Loft(new Ring[] { new Ring(new Vector3(-420f, 60f, -330f), 3f, 3f, Color.white), new Ring(new Vector3(-420f, 88f, -330f), 2f, 2f, Color.white), new Ring(new Vector3(-420f, 92f, -330f), 3.2f, 3.2f, new Color(.9f, .2f, .2f)), new Ring(new Vector3(-420f, 99f, -330f), .2f, .2f, new Color(.9f, .2f, .2f)) }, 0, 10, Matrix4x4.identity);
            // Mt. Fuji: wide gently-flared skirt (real base/height ratio, not a pointy volcano cone), dark rock mid-slope, snowcap with a flattish crater top
            Vector3 fp = new Vector3(250f, 0f, 900f);
            // strong hue+value contrast on purpose: at z=900 the linear fog (ends 1600) washes ~45% of the colour toward the pale sky,
            // so a "realistic" soft haze blue (previous version) disappeared entirely. Deep indigo body + warm dark rock + bright snow survives that.
            Color bodyDeep = new Color(.20f, .22f, .38f), bodyMid = new Color(.17f, .18f, .30f), fujiRock = new Color(.30f, .20f, .18f), snow = new Color(.98f, .98f, 1f), snowShade = new Color(.78f, .80f, .90f);
            k.Loft(new Ring[] {
                new Ring(fp, 2000f, 1520f, bodyDeep),
                new Ring(fp + Vector3.up * 36f, 1680f, 1280f, Color.Lerp(bodyDeep, bodyMid, .5f)),
                new Ring(fp + Vector3.up * 92f, 1280f, 980f, bodyMid),
                new Ring(fp + Vector3.up * 170f, 820f, 630f, Color.Lerp(bodyMid, fujiRock, .55f)),
                new Ring(fp + Vector3.up * 255f, 470f, 360f, fujiRock),
                new Ring(fp + Vector3.up * 305f, 300f, 230f, Color.Lerp(fujiRock, snowShade, .6f)),
                new Ring(fp + Vector3.up * 350f, 175f, 134f, snow),
                new Ring(fp + Vector3.up * 405f, 85f, 65f, snow),
                new Ring(fp + Vector3.up * 438f, 40f, 30f, snow),
                new Ring(fp + Vector3.up * 450f, 26f, 20f, snow),   // near-flat crater rim instead of a sharp point
            }, 0, 28, Matrix4x4.identity);
            MeshKit.Make("Scenery", world, k, Lit);
        }


        /// <summary>Thermal updraft columns (soar on them like a real kite). Faint golden shafts mark them.</summary>
        static void BuildThermals(Transform world)
        {
            Material glow = new Material(Shader.Find("Sprites/Default"));
            Vector2[] pts = { new Vector2(125, 70), new Vector2(35, 118), new Vector2(-62, 52), new Vector2(-132, 104), new Vector2(70, 18) };
            foreach (Vector2 xz in pts)
            {
                GameObject t = new GameObject("Thermal"); t.transform.SetParent(world, false); t.transform.position = new Vector3(xz.x, 0f, xz.y);
                MeshKit k = new MeshKit();
                for (int i = 0; i < 3; i++)
                {
                    float r = 26f - i * 6f; Color c = new Color(1f, .93f, .7f, 0.07f + i * 0.02f);
                    k.Loft(new Ring[] { new Ring(new Vector3(0f, 0f, 0f), r, r, c), new Ring(new Vector3(0f, 60f, 0f), r * 0.9f, r * 0.9f, c), new Ring(new Vector3(0f, 115f, 0f), r * 0.6f, r * 0.6f, new Color(c.r, c.g, c.b, 0f)) }, 0, 16, Matrix4x4.identity);
                }
                GameObject vis = MeshKit.Make("Shaft", t.transform, k, glow);
                vis.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.AddComponent<Thermal>();
            }
        }

        /// <summary>Dead tree stub the hawk perches on during the title (like the reference photo).</summary>
        static void BuildSnag(Transform world)
        {
            MeshKit k = new MeshKit();
            Color bark = new Color(.42f, .38f, .33f), dry = new Color(.56f, .52f, .46f);
            Vector3 b = new Vector3(PerchPos.x - 4f, 0f, PerchPos.z);
            Ring[] trunk = new Ring[] { new Ring(b, 1.5f, 1.5f, bark), new Ring(b + new Vector3(.5f, 12f, 0f), 1.0f, 1.0f, bark), new Ring(b + new Vector3(1.5f, 24f, 0f), .7f, .7f, dry), new Ring(b + new Vector3(3.0f, 30f, 0f), .45f, .45f, dry) };
            k.Loft(trunk, 0, 10, Matrix4x4.identity);
            // perch branch (rises to the perch point)
            Ring[] br = new Ring[] { new Ring(b + new Vector3(2.0f, 28f, 0f), .38f, .38f, dry), new Ring(new Vector3(PerchPos.x - 1.5f, PerchPos.y - 1.15f, PerchPos.z), .30f, .30f, dry), new Ring(new Vector3(PerchPos.x + 1.4f, PerchPos.y - 0.85f, PerchPos.z), .16f, .16f, dry) };
            k.Loft(br, 1, 8, Matrix4x4.identity);
            k.Loft(new Ring[] { new Ring(b + new Vector3(1.0f, 16f, 0f), .25f, .25f, dry), new Ring(b + new Vector3(-3.5f, 21f, 1.0f), .12f, .12f, dry) }, 1, 6, Matrix4x4.identity);
            MeshKit.Make("Snag", world, k, Lit);
            GameObject o = new GameObject("SnagCollider");
            o.transform.SetParent(world, false); o.transform.position = b;
            TreeObstacle to = o.AddComponent<TreeObstacle>(); to.r = 1.8f; to.h = 30f;
        }

        // ------------------------------------------------------------ people / animals
        static void BuildPeople(Transform world, GameManager gm, System.Random rng)
        {
            Transform hroot = new GameObject("Humans").transform;
            Vector2[] cl = { new Vector2(110, 60), new Vector2(30, 112), new Vector2(-10, 40), new Vector2(-100, 84), new Vector2(-128, 30), new Vector2(60, 24) };
            int seed = 500;
            foreach (Vector2 c in cl)
                for (int k = 0; k < 3; k++)
                {
                    bool female = (k + seed) % 2 == 0;
                    HumanAI ai = HumanFactory.Create(hroot, female, seed++);
                    ai.personality = (Personality)rng.Next(6);
                    ai.height = 0.95f + (float)rng.NextDouble() * 0.12f;
                    ai.transform.localScale = Vector3.one * ai.height;
                    ai.Init(new Vector3(Mathf.Clamp(c.x + (float)(rng.NextDouble() * 36 - 18), -165f, 165f), 0f, Mathf.Clamp(c.y + (float)(rng.NextDouble() * 28 - 14), 12f, 146f)));
                    gm.humans.Add(ai);
                }
            foreach (HumanAI h in gm.humans)
            {
                MeshRenderer f = h.foodAnchor.GetComponent<MeshRenderer>();
                h.foodIndex = Random.Range(0, GameManager.Foods.Length);
                f.sharedMaterial.color = GameManager.Foods[h.foodIndex].color;
            }
        }

        static void BuildAnimals(Transform world, System.Random rng)
        {
            Transform aroot = new GameObject("Animals").transform;
            for (int i = 0; i < 5; i++)
            {
                bool dog = i >= 3;
                Color fur = dog ? new Color(.62f, .42f, .22f) : (rng.NextDouble() < .5 ? new Color(.85f, .55f, .25f) : new Color(.25f, .25f, .27f));
                GameObject a = new GameObject(dog ? "Dog" : "Cat"); a.transform.SetParent(aroot, false);
                a.transform.position = new Vector3((float)(rng.NextDouble() * 300 - 150), 0f, (float)(20 + rng.NextDouble() * 110));
                AnimalAI ai = a.AddComponent<AnimalAI>(); ai.kind = dog ? AKind.Dog : AKind.Cat;
                float sc = dog ? 1.4f : 0.9f;
                Material lit = TonbiMat.Lit(0.15f);
                Transform m = new GameObject("Model").transform; m.SetParent(a.transform, false); m.localScale = Vector3.one * sc;
                MeshKit k = new MeshKit();
                k.Loft(new Ring[] { new Ring(new Vector3(0f, .55f, -.55f), .09f, .1f, fur), new Ring(new Vector3(0f, .55f, -.25f), .17f, .19f, fur), new Ring(new Vector3(0f, .56f, .2f), .18f, .2f, fur), new Ring(new Vector3(0f, .58f, .48f), .12f, .14f, fur) }, 1, 10, Matrix4x4.identity);
                k.Ellipsoid(new Vector3(0f, .72f, .62f), new Vector3(.14f, .14f, .15f), fur, Matrix4x4.identity, 5, 10);
                k.Ellipsoid(new Vector3(0f, .68f, .76f), new Vector3(.07f, .06f, .08f), Color.Lerp(fur, Color.white, .5f), Matrix4x4.identity, 4, 8);
                for (int s = -1; s <= 1; s += 2)
                    k.Loft(new Ring[] { new Ring(new Vector3(.09f * s, .82f, .6f), .045f, .03f, fur), new Ring(new Vector3(.11f * s, .98f, .58f), .005f, .005f, fur) }, 0, 5, Matrix4x4.identity);
                MeshKit.Make("Body", m, k, lit);
                Transform tl = new GameObject("Tail").transform; tl.SetParent(m, false); tl.localPosition = new Vector3(0f, .62f, -.6f);
                MeshKit tk = new MeshKit();
                tk.Loft(new Ring[] { new Ring(new Vector3(0f, 0f, 0f), .035f, .035f, fur), new Ring(new Vector3(0f, .05f, -.3f), .03f, .03f, fur), new Ring(new Vector3(0f, .22f, -.5f), .018f, .018f, fur) }, 1, 6, Matrix4x4.identity);
                MeshKit.Make("TailMesh", tl, tk, lit);
                ai.tail = tl;
                Transform[] legs = new Transform[4];
                for (int l = 0; l < 4; l++)
                {
                    Transform lg = new GameObject("Leg" + l).transform; lg.SetParent(m, false);
                    lg.localPosition = new Vector3((l % 2 == 0 ? -1 : 1) * .12f, .42f, (l < 2 ? .32f : -.32f));
                    MeshKit lk = new MeshKit();
                    lk.Loft(new Ring[] { new Ring(Vector3.zero, .06f, .06f, fur), new Ring(new Vector3(0f, -.2f, 0f), .04f, .04f, fur), new Ring(new Vector3(0f, -.42f, 0f), .045f, .06f, Color.Lerp(fur, Color.white, .4f)) }, 0, 6, Matrix4x4.identity);
                    MeshKit.Make("L", lg, lk, lit);
                    legs[l] = lg;
                }
                ai.legs = legs;
            }
        }

        static void BuildVigilantes()
        {
            VigilanteManager vm = new GameObject("Vigilantes").AddComponent<VigilanteManager>();
            for (int i = 0; i < 3; i++)
            {
                HumanAI h = HumanFactory.Create(vm.transform, false, 9000 + i, true);
                Object.Destroy(h);   // movement is driven by VigilanteManager
                h.gameObject.name = "Vigilante" + i;
                h.gameObject.SetActive(false);
                vm.vigs.Add(h.transform);
            }
        }
    }
}
