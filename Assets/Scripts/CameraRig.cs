using UnityEngine;

namespace Tonbi
{
    /// <summary>Chase camera with speed-based FOV/pull-back, dive look-ahead, the snatch cinematic (slow-mo + close-up),
    /// a title shot on the perched kite, and the ending flyby.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public float timeScale = 1f, zoom;
        Camera cam; Vector3 pos, lookTarget; float shake, grabCine, slowT; HumanAI cineHuman; Vector3 cineCP; bool cineLock;

        void Awake() { cam = GetComponent<Camera>(); }
        public void Snap() { pos = transform.position; zoom = 0; timeScale = 1; grabCine = 0; cineLock = false; slowT = 0; }
        public void Shake(float s) { shake = Mathf.Max(shake, s); }
        public void SlowMo(float seconds) { slowT = seconds; }

        public void OnSnatch(HumanAI h, BirdController bird)
        {
            grabCine = 0.95f; cineHuman = h; cineLock = false; Shake(0.25f);
        }

        public void UpdateCine(float rdt)
        {
            slowT = Mathf.Max(0f, slowT - rdt);
            if (grabCine > 0) { grabCine -= rdt; zoom = Mathf.MoveTowards(zoom, 1, rdt * 9f); timeScale = Mathf.Lerp(timeScale, 0.18f, Mathf.Min(1, rdt * 14)); }
            else
            {
                zoom = Mathf.MoveTowards(zoom, 0, rdt * 3f);
                timeScale = Mathf.Lerp(timeScale, slowT > 0f ? 0.3f : 1f, Mathf.Min(1, rdt * 5));
                if (zoom <= 0.01f) cineLock = false;
            }
        }

        public void UpdateCamera(float rdt, BirdController bird, GameManager gm)
        {
            Vector3 bp = bird.transform.position;
            Vector3 fwd = BirdController.Forward(bird.yaw, bird.pitch);

            if (gm.phase == Phase.Title)
            {
                // close 3/4 portrait of the perched kite, slowly drifting; sea and sky behind it
                float t = Time.time;
                Vector3 off = new Vector3(-6.5f + Mathf.Sin(t * 0.25f) * 2.2f, 0.9f + Mathf.Sin(t * 0.17f) * 0.5f, 8.5f + Mathf.Cos(t * 0.21f) * 1.5f);
                pos = Vector3.Lerp(pos, bp + off, Mathf.Min(1f, rdt * 2.5f));
                lookTarget = Vector3.Lerp(lookTarget, bp + new Vector3(0f, 0.4f, 0f), Mathf.Min(1f, rdt * 4f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 38f, Mathf.Min(1f, rdt * 3f));
                ApplyPose(Quaternion.LookRotation((lookTarget - pos).normalized, Vector3.up), 0f, 0f);
                return;
            }
            if (gm.phase == Phase.Ending)
            {
                float t = Time.time, ang = t * 0.22f + 2.2f;
                float dist = 13f + Mathf.Sin(t * 0.3f) * 2f;
                Vector3 off = new Vector3(Mathf.Sin(ang) * dist, 1.5f + Mathf.Sin(t * 0.4f) * 1.2f, Mathf.Cos(ang) * dist);
                pos = Vector3.Lerp(pos, bp + off, Mathf.Min(1f, rdt * 2.5f));
                lookTarget = Vector3.Lerp(lookTarget, bp, Mathf.Min(1f, rdt * 5f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 44f, Mathf.Min(1f, rdt * 3f));
                ApplyPose(Quaternion.LookRotation((lookTarget - pos).normalized, Vector3.up), 0f, 0f);
                return;
            }

            float speedK = Mathf.InverseLerp(16, 76, bird.v);
            float d2 = Mathf.Lerp(9f, 15f, speedK) + (bird.diving ? 3f : 0f);
            Vector3 chase = bp - fwd * d2 + Vector3.up * Mathf.Lerp(3.2f, 2f, speedK) + transform.right * (-bird.roll * 1.6f);
            if (zoom > 0.02f && cineHuman)
            {
                if (!cineLock) { cineLock = true; Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized; cineCP = cineHuman.transform.position + Vector3.up * 2.6f + side * 8f - fwd * 6f; }
                pos = Vector3.Lerp(pos, cineCP, Mathf.Min(1, rdt * 10));
                lookTarget = cineHuman.transform.position + Vector3.up * 2.4f;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 38f, Mathf.Min(1, rdt * 8));
            }
            else
            {
                pos = Vector3.Lerp(pos, chase, Mathf.Min(1, rdt * 6f));
                lookTarget = bp + fwd * 14f;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 68f + speedK * 18f + (bird.diving ? 8f : 0f), Mathf.Min(1, rdt * 5));
            }
            shake = Mathf.Max(0, shake - rdt * 2.2f);
            Vector3 sh = Random.insideUnitSphere * (shake * 0.35f + (bird.diving ? speedK * 0.04f : 0f));
            transform.position = pos + sh;
            Quaternion look = Quaternion.LookRotation((lookTarget - transform.position).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0, 0, -bird.roll * Mathf.Rad2Deg * 0.35f), Mathf.Min(1, rdt * 9));
        }

        void ApplyPose(Quaternion look, float roll, float shk)
        {
            shake = Mathf.Max(0, shake - Time.deltaTime * 2.2f);
            transform.position = pos + Random.insideUnitSphere * (shake * 0.35f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Min(1f, Time.deltaTime * 6f));
        }
    }
}
