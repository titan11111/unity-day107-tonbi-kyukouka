using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Tonbi
{
    /// <summary>Virtual stick (left half drag) + DIVE / FLAP buttons (right side). Works with touch, mouse and keyboard.</summary>
    public static class TonbiInput
    {
        public static Vector2 Stick;     // x: right+, y: up+ (push up = climb)
        public static bool Dive;         // held
        public static bool FlapPressed;  // one frame
        public static bool SubmitPressed, MutePressed, BackPressed; // one frame (Enter/Space, M, Esc)
        public static Vector2 StickOrigin, StickPos; public static bool StickActive;

        // Landscape phone: buttons sized/anchored off the SHORT side (screen height in landscape) so they land
        // inside the right thumb's natural arc near the bottom-right corner instead of being stacked too tall to fit.
        static float S => Mathf.Min(Screen.width, Screen.height);
        public static Rect DiveRect => new Rect(Screen.width - S * 0.40f, Screen.height - S * 0.42f, S * 0.34f, S * 0.34f);
        public static Rect FlapRect => new Rect(Screen.width - S * 0.72f, Screen.height - S * 0.28f, S * 0.25f, S * 0.25f);

        struct Pt { public Vector2 pos; public int id; public bool down, pressedNow; }
        static int stickId = -1;

        static int Collect(Pt[] buf)
        {
            int n = 0;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed && !t.press.wasReleasedThisFrame) continue;
                    if (n >= buf.Length) break;
                    buf[n++] = new Pt { pos = t.position.ReadValue(), id = t.touchId.ReadValue(), down = t.press.isPressed, pressedNow = t.press.wasPressedThisFrame };
                }
            if (n == 0 && Mouse.current != null)
            {
                var m = Mouse.current;
                if (m.leftButton.isPressed || m.leftButton.wasReleasedThisFrame)
                    buf[n++] = new Pt { pos = m.position.ReadValue(), id = 99, down = m.leftButton.isPressed, pressedNow = m.leftButton.wasPressedThisFrame };
            }
#else
            for (int i = 0; i < Input.touchCount && n < buf.Length; i++)
            {
                var t = Input.GetTouch(i);
                buf[n++] = new Pt { pos = t.position, id = t.fingerId, down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled, pressedNow = t.phase == TouchPhase.Began };
            }
            if (n == 0 && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
                buf[n++] = new Pt { pos = Input.mousePosition, id = 99, down = Input.GetMouseButton(0), pressedNow = Input.GetMouseButtonDown(0) };
#endif
            return n;
        }

        static readonly Pt[] buf = new Pt[6];
        public static void Poll()
        {
            FlapPressed = false; Dive = false; SubmitPressed = false; MutePressed = false; BackPressed = false;
            int n = Collect(buf);
            bool stickSeen = false;
            for (int i = 0; i < n; i++)
            {
                var p = buf[i]; var gp = new Vector2(p.pos.x, Screen.height - p.pos.y); // GUI coords
                if (DiveRect.Contains(gp) && p.down) Dive = true;
                else if (FlapRect.Contains(gp) && p.pressedNow) FlapPressed = true;
                else if (p.pos.x < Screen.width * 0.55f || p.id == stickId)
                {
                    if (stickId < 0 && p.pressedNow) { stickId = p.id; StickOrigin = p.pos; }
                    if (p.id == stickId && p.down)
                    {
                        stickSeen = true; StickPos = p.pos;
                        var d = (p.pos - StickOrigin) / (Screen.dpi > 0 ? Screen.dpi * 0.45f : 120f);
                        Stick = Vector2.ClampMagnitude(d, 1f);
                    }
                }
            }
            if (!stickSeen) { stickId = -1; Stick = Vector2.zero; }
            StickActive = stickSeen;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                var k = Vector2.zero;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) k.x -= 1; if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) k.x += 1;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) k.y += 1; if (kb.downArrowKey.isPressed || kb.sKey.isPressed) k.y -= 1;
                if (k != Vector2.zero) Stick = k;
                if (kb.spaceKey.isPressed) Dive = true;
                if (kb.leftShiftKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame) FlapPressed = true;
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) SubmitPressed = true;
                if (kb.mKey.wasPressedThisFrame) MutePressed = true;
                if (kb.escapeKey.wasPressedThisFrame) BackPressed = true;
            }
#else
            var kv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (kv != Vector2.zero) Stick = kv;
            if (Input.GetKey(KeyCode.Space)) Dive = true;
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.F)) FlapPressed = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) SubmitPressed = true;
            if (Input.GetKeyDown(KeyCode.M)) MutePressed = true;
            if (Input.GetKeyDown(KeyCode.Escape)) BackPressed = true;
#endif
        }
    }
}
