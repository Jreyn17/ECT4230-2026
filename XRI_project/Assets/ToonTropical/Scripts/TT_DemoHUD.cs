// Toon Tropical: on-screen help for the walk-around demo, in the pack's look (cream card,
// ink outline, keycaps, gold buttons), drawn with IMGUI so it needs no fonts, prefabs or UI
// packages. Shows the key bindings and switches the theme (T) and time of day (N); H hides it.
// A web page can drive it instead (SendMessage: UseWebOverlay, SetTheme, SetTimeOfDay).
// Optional: delete it freely.
using UnityEngine;

namespace ToonTropical
{
    [AddComponentMenu("Toon Tropical/TT Demo HUD")]
    public class TT_DemoHUD : MonoBehaviour
    {
        public TT_Environment environment;
        public bool visible = true;

        static readonly TT_Environment.Theme[] Themes = (TT_Environment.Theme[])System.Enum.GetValues(typeof(TT_Environment.Theme));
        static readonly TT_Environment.TimeOfDay[] Times = (TT_Environment.TimeOfDay[])System.Enum.GetValues(typeof(TT_Environment.TimeOfDay));
        static readonly Color Ink = new Color32(0x2B, 0x1F, 0x29, 0xFF);
        static readonly Color Cream = new Color32(0xFF, 0xF6, 0xE8, 0xF2);
        static readonly Color Gold = new Color32(0xF4, 0xC4, 0x30, 0xFF);
        static readonly Color GoldHover = new Color32(0xFF, 0xD8, 0x5C, 0xFF);
        static readonly Color Key = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        bool webOverlay;
        GUIStyle card, keycap, text, title, button, chip;
        float styledScale = -1f;

        void Awake()
        {
            if (environment == null) environment = FindFirstObjectByType<TT_Environment>();
        }

        void Update()
        {
            if (webOverlay) return;                                     // the page handles the keys
            if (TT_DemoInput.Pressed(KeyCode.H)) visible = !visible;
            if (environment == null) return;
            if (TT_DemoInput.Pressed(KeyCode.T)) NextTheme();
            if (TT_DemoInput.Pressed(KeyCode.N)) NextTime();
        }

        // ---- web page bridge (unityInstance.SendMessage("Demo HUD", ...)) --------------------
        public void UseWebOverlay() { webOverlay = true; }
        public void SetTheme(string name)
        {
            if (environment != null && System.Enum.TryParse(name, out TT_Environment.Theme t)) { environment.theme = t; environment.Apply(); }
        }
        public void SetTimeOfDay(string name)
        {
            if (environment != null && System.Enum.TryParse(name, out TT_Environment.TimeOfDay t)) { environment.timeOfDay = t; environment.Apply(); }
        }

        void NextTheme()
        {
            environment.theme = Themes[(System.Array.IndexOf(Themes, environment.theme) + 1) % Themes.Length];
            environment.Apply();
        }

        void NextTime()
        {
            environment.timeOfDay = Times[(System.Array.IndexOf(Times, environment.timeOfDay) + 1) % Times.Length];
            environment.Apply();
        }

        // ---- drawing ----------------------------------------------------------------------
        /// Rounded rectangle with an ink outline, 9-sliced so it stretches to any size.
        static Texture2D Rounded(Color fill, Color edge, int r, int edgeW)
        {
            int n = r * 2 + 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // distance outside the rounded rect inset by r (negative = inside)
                    float dx = Mathf.Max(Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;              // 0 at the outer edge
                    float a = Mathf.Clamp01(0.5f - d);                        // antialiased outer edge
                    float inner = Mathf.Clamp01(-d - edgeW + 0.5f);           // 1 inside the outline
                    var c = Color.Lerp(edge, fill, inner);
                    c.a *= a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        void Styles(float s)
        {
            if (Mathf.Approximately(s, styledScale)) return;
            styledScale = s;
            int r = Mathf.RoundToInt(12 * s), e = Mathf.Max(2, Mathf.RoundToInt(3 * s));
            var border = new RectOffset(r + 2, r + 2, r + 2, r + 2);
            card = new GUIStyle { normal = { background = Rounded(Cream, Ink, r, e) }, border = border,
                                  padding = new RectOffset((int)(16 * s), (int)(16 * s), (int)(12 * s), (int)(14 * s)) };
            int kr = Mathf.RoundToInt(6 * s), ke = Mathf.Max(2, Mathf.RoundToInt(2 * s));
            keycap = new GUIStyle { normal = { background = Rounded(Key, Ink, kr, ke), textColor = Ink },
                                    border = new RectOffset(kr + 2, kr + 2, kr + 2, kr + 2),
                                    alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                                    fontSize = Mathf.RoundToInt(12 * s), padding = new RectOffset((int)(7 * s), (int)(7 * s), 0, 0) };
            text = new GUIStyle { normal = { textColor = Ink }, fontSize = Mathf.RoundToInt(14 * s),
                                  alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            title = new GUIStyle(text) { fontSize = Mathf.RoundToInt(20 * s) };
            button = new GUIStyle { normal = { background = Rounded(Gold, Ink, kr, ke), textColor = Ink },
                                    hover = { background = Rounded(GoldHover, Ink, kr, ke), textColor = Ink },
                                    active = { background = Rounded(GoldHover, Ink, kr, ke), textColor = Ink },
                                    border = new RectOffset(kr + 2, kr + 2, kr + 2, kr + 2),
                                    alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                                    fontSize = Mathf.RoundToInt(13 * s) };
            chip = new GUIStyle(card) { padding = new RectOffset((int)(10 * s), (int)(10 * s), (int)(6 * s), (int)(6 * s)) };
        }

        void Row(float s, params string[] keysThenLabel)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(24 * s));
            for (int i = 0; i < keysThenLabel.Length; i += 2)
            {
                GUILayout.Label(keysThenLabel[i], keycap, GUILayout.Height(22 * s), GUILayout.MinWidth(24 * s));
                GUILayout.Space(6 * s);
                GUILayout.Label(keysThenLabel[i + 1], text, GUILayout.Height(22 * s));
                GUILayout.Space(14 * s);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4 * s);
        }

        void OnGUI()
        {
            if (webOverlay) return;
            float s = Mathf.Clamp(Screen.height / 900f, 1f, 2.5f);         // scale with the window
            Styles(s);
            float m = 16 * s;
            if (!visible)
            {
                GUILayout.BeginArea(new Rect(m, m, 160 * s, 44 * s));
                GUILayout.BeginHorizontal(chip);
                GUILayout.Label("H", keycap, GUILayout.Height(22 * s), GUILayout.MinWidth(24 * s));
                GUILayout.Space(6 * s);
                GUILayout.Label("help", text, GUILayout.Height(22 * s));
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginArea(new Rect(m, m, 330 * s, 300 * s));
            GUILayout.BeginVertical(card);
            GUILayout.Label("TOON TROPICAL", title, GUILayout.Height(28 * s));
            GUILayout.Space(6 * s);
            Row(s, "WASD", "walk", "Shift", "run");
            Row(s, "E", "wave", "F", "dig");
            Row(s, "Drag", "look", "Wheel", "zoom");
            if (environment != null)
            {
                GUILayout.Space(6 * s);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"{environment.theme}  ·  T", button, GUILayout.Height(30 * s))) NextTheme();
                GUILayout.Space(8 * s);
                if (GUILayout.Button($"{environment.timeOfDay}  ·  N", button, GUILayout.Height(30 * s))) NextTime();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8 * s);
            Row(s, "H", "hide this panel");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
