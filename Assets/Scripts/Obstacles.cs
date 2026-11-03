using System.Collections.Generic;
using UnityEngine;

namespace Tonbi
{
    /// <summary>Box building (cube primitive, scale = size, pivot centre). The roof is perchable; walls knock the bird back.</summary>
    public class BuildingObstacle : MonoBehaviour
    {
        public static readonly List<BuildingObstacle> All = new List<BuildingObstacle>();
        [HideInInspector] public float cx, cz, hw, hd, roof;
        void OnEnable() { Refresh(); All.Add(this); }
        void OnDisable() { All.Remove(this); }
        public void Refresh() { var p = transform.position; var s = transform.lossyScale; cx = p.x; cz = p.z; hw = s.x * 0.5f; hd = s.z * 0.5f; roof = p.y + s.y * 0.5f; }
    }

    /// <summary>Pine / palm tree trunk+canopy as a vertical cylinder: radius r, height h.</summary>
    public class TreeObstacle : MonoBehaviour
    {
        public static readonly List<TreeObstacle> All = new List<TreeObstacle>();
        public float r = 3.4f, h = 13f;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }
}
