// Toon Tropical - scene environment: time of day, wind and the animation clock.
// Drop one into a scene (the demo scenes already have one). It drives the pack shaders
// through global properties, so every Toon Tropical material reacts at once:
//   * Theme (Tropical, Autumn, Spooky, Winter) re-colours the whole pack.
//   * Time of day swaps the shared palette (day / sunset / night), re-aims the sun,
//     sets the camera background and scales fire/lamp point lights.
//   * Wind sways palms, plants, grass and the flag (per-vertex weights baked in the FBX).
//   * The clock animates water, foam, flames and lamp glow.
//   * Sky (optional): the ToonTropical/Sky material follows the theme and time of day on
//     perspective cameras; models toonified from other packs follow them as a tint.
// Works in edit mode too, so the Scene view shows the animation.
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToonTropical
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Toon Tropical/TT Environment")]
    public class TT_Environment : MonoBehaviour
    {
        public enum TimeOfDay { Day, Sunset, Night }
        public enum Theme { Tropical, Autumn, Spooky, Winter }

        // One colour theme: the three time-of-day palettes plus matching backgrounds.
        [System.Serializable]
        public class ThemeSet
        {
            public Texture2D day, sunset, night;
            public Color dayBackground = Color.white, sunsetBackground = Color.white, nightBackground = Color.black;
        }

        [Header("Theme")]
        [Tooltip("Re-colours the whole pack: every Toon Tropical material at once.")]
        public Theme theme = Theme.Tropical;
        [Tooltip("Bonus themes, indexed by Theme (Autumn, Spooky, Winter). Tropical uses the fields below.")]
        public ThemeSet[] bonusThemes = new ThemeSet[3];

        [Header("Time of day")]
        public TimeOfDay timeOfDay = TimeOfDay.Day;
        public Texture2D dayPalette, sunsetPalette, nightPalette;
        [Tooltip("Directional light to re-aim per time of day (optional).")]
        public Light sun;
        [Tooltip("Sun rotation used for Day; sunset and night are derived from its heading.")]
        public Vector3 daySunEuler = new Vector3(55f, 160f, 0f);
        [Tooltip("Cameras whose solid background colour follows the time of day (optional).")]
        public Camera[] cameras;
        public Color dayBackground = new Color32(0xFB, 0xE9, 0xD4, 0xFF);
        public Color sunsetBackground = new Color32(0xF3, 0xB5, 0x96, 0xFF);
        public Color nightBackground = new Color32(0x1E, 0x27, 0x44, 0xFF);
        [Tooltip("Strength of point/spot lights (campfire, torches, lanterns) per time of day.")]
        public Vector3 fireLightScale = new Vector3(0.15f, 0.3f, 1.4f);

        [Header("Sky")]
        [Tooltip("ToonTropical/Sky material (M_Island_Sky). Perspective cameras in Cameras show it; " +
                 "orthographic ones keep the solid background colour.")]
        public Material skyMaterial;
        public bool useSky = true;

        [Header("Wind")]
        public Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        [Range(0f, 0.5f)] public float windStrength = 0.12f;
        [Range(0.1f, 4f)] public float windFrequency = 1.3f;
        [Range(0f, 0.1f)] public float flutter = 0.03f;
        [Range(0f, 1f)] public float gusts = 0.35f;

        [Header("Animation clock")]
        public bool animate = true;
        public bool animateInEditMode = true;
        [Range(0f, 3f)] public float timeScale = 1f;
        [Tooltip("Freeze the clock at a fixed time (useful for screenshots).")]
        public bool useFixedTime;
        public float fixedTime;

        static readonly int TimeId = Shader.PropertyToID("_TT_Time");
        static readonly int WindId = Shader.PropertyToID("_TT_Wind");
        static readonly int WindParamsId = Shader.PropertyToID("_TT_WindParams");
        static readonly int PaletteId = Shader.PropertyToID("_TT_PaletteGlobal");
        static readonly int PaletteOverrideId = Shader.PropertyToID("_TT_PaletteOverride");
        static readonly int AddLightScaleId = Shader.PropertyToID("_TT_AdditionalLightScale");
        static readonly int PaletteBaseId = Shader.PropertyToID("_TT_PaletteBase");
        static readonly int TintReadyId = Shader.PropertyToID("_TT_TintReady");
        static readonly int SkyTopId = Shader.PropertyToID("_TT_SkyTop");
        static readonly int SkyHorizonId = Shader.PropertyToID("_TT_SkyHorizon");
        static readonly int SkyGroundId = Shader.PropertyToID("_TT_SkyGround");
        static readonly int SkyCloudId = Shader.PropertyToID("_TT_SkyCloud");
        static readonly int SkyIslandId = Shader.PropertyToID("_TT_SkyIsland");
        static readonly int SunDirId = Shader.PropertyToID("_TT_SunDir");
        static readonly int SunColorId = Shader.PropertyToID("_TT_SunColor");
        static readonly int InkId = Shader.PropertyToID("_TT_InkColor");
        static readonly int FxTintId = Shader.PropertyToID("_TT_FxTint");
        static readonly Color[] FxTint = {                       // unlit particles: day, sunset, night
            new Color(1f, 1f, 1f, 1f), new Color(1f, 0.88f, 0.82f, 1f), new Color(0.42f, 0.47f, 0.66f, 1f) };

        // sky colour at the zenith per time of day, blended with the theme's backdrop colour
        static readonly Color[] SkyZenith = {
            new Color(0.45f, 0.72f, 0.92f), new Color(0.50f, 0.38f, 0.64f), new Color(0.06f, 0.08f, 0.18f) };
        static readonly Color[] SunDisc = {
            new Color(1f, 0.96f, 0.82f), new Color(1f, 0.72f, 0.45f), new Color(0.93f, 0.94f, 1f) };

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += EditorTick;
#endif
            Apply();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            Shader.SetGlobalFloat(TimeId, 0f);
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalFloat(PaletteOverrideId, 0f);
            Shader.SetGlobalFloat(AddLightScaleId, 0f);
            Shader.SetGlobalFloat(TintReadyId, 0f);
            Shader.SetGlobalColor(SkyHorizonId, Color.clear);
            Shader.SetGlobalColor(FxTintId, Color.clear);
        }

        void OnValidate() => Apply();

#if UNITY_EDITOR
        // Backdrop colours for the bonus themes (Autumn, Spooky, Winter): day, sunset, night.
        static readonly Color32[,] ThemeBackdrops = {
            { new Color32(0xF6, 0xE2, 0xC8, 0xFF), new Color32(0xF0, 0xB0, 0x8A, 0xFF), new Color32(0x2A, 0x22, 0x38, 0xFF) },
            { new Color32(0xC9, 0xC3, 0xD6, 0xFF), new Color32(0x9E, 0x7F, 0xA8, 0xFF), new Color32(0x1A, 0x17, 0x26, 0xFF) },
            { new Color32(0xE8, 0xF0, 0xF6, 0xFF), new Color32(0xF0, 0xC9, 0xC0, 0xFF), new Color32(0x1C, 0x27, 0x40, 0xFF) },
        };

        // Adding the component (or "Find Palettes" from its context menu) wires up all 12
        // palettes from the project: Tropical + the three bonus themes, each day / sunset / night.
        void Reset() => AutoAssignPalettes();

        [ContextMenu("Find Palettes")]
        public void AutoAssignPalettes()
        {
            Texture2D Find(string n)
            {
                foreach (var g in AssetDatabase.FindAssets(n + " t:Texture2D"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileNameWithoutExtension(p) == n)
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
                return null;
            }
            dayPalette = Find("T_Island_Palette");
            sunsetPalette = Find("T_Island_Palette_Sunset");
            nightPalette = Find("T_Island_Palette_Night");
            string[] names = { "Autumn", "Spooky", "Winter" };
            bonusThemes = new ThemeSet[names.Length];
            for (int i = 0; i < names.Length; i++)
                bonusThemes[i] = new ThemeSet
                {
                    day = Find($"T_Island_Palette_{names[i]}"),
                    sunset = Find($"T_Island_Palette_{names[i]}_Sunset"),
                    night = Find($"T_Island_Palette_{names[i]}_Night"),
                    dayBackground = ThemeBackdrops[i, 0], sunsetBackground = ThemeBackdrops[i, 1],
                    nightBackground = ThemeBackdrops[i, 2],
                };
            if (skyMaterial == null)
                foreach (var g in AssetDatabase.FindAssets("M_Island_Sky t:Material"))
                    skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            EditorUtility.SetDirty(this);
        }
#endif
        void Update() => Apply();

#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying || !animateInEditMode || !animate) return;
            Apply();
            SceneView.RepaintAll();
        }
#endif

        float Clock()
        {
            if (!animate) return 0f;
            if (useFixedTime) return fixedTime;
#if UNITY_EDITOR
            if (!Application.isPlaying) return (float)EditorApplication.timeSinceStartup * timeScale;
#endif
            return Time.time * timeScale;
        }

        public void Apply()
        {
            Shader.SetGlobalFloat(TimeId, Clock());
            Vector3 d = windDirection.sqrMagnitude > 0f ? windDirection.normalized * windStrength : Vector3.zero;
            Shader.SetGlobalVector(WindId, new Vector4(d.x, d.y, d.z, windFrequency));
            Shader.SetGlobalVector(WindParamsId, new Vector4(flutter, gusts, 0f, 0f));

            ThemeSet ts = theme != Theme.Tropical && bonusThemes != null && (int)theme - 1 < bonusThemes.Length
                          ? bonusThemes[(int)theme - 1] : null;
            if (ts != null && ts.day == null) ts = null;           // theme not set up: stay Tropical
            Texture2D pal = ts != null
                ? (timeOfDay == TimeOfDay.Sunset ? ts.sunset : timeOfDay == TimeOfDay.Night ? ts.night : ts.day)
                : (timeOfDay == TimeOfDay.Sunset ? sunsetPalette : timeOfDay == TimeOfDay.Night ? nightPalette : dayPalette);
            currentPalette = pal;
            if (pal != null)
            {
                Shader.SetGlobalTexture(PaletteId, pal);
                Shader.SetGlobalFloat(PaletteOverrideId, 1f);
            }
            else Shader.SetGlobalFloat(PaletteOverrideId, 0f);
            Shader.SetGlobalFloat(AddLightScaleId, fireLightScale[(int)timeOfDay]);
            if (dayPalette != null)                 // textured-mode tint is measured against it
            {
                Shader.SetGlobalTexture(PaletteBaseId, dayPalette);
                Shader.SetGlobalFloat(TintReadyId, 1f);
            }
            else Shader.SetGlobalFloat(TintReadyId, 0f);

            if (sun != null)
            {
                Vector3 e = daySunEuler;
                if (timeOfDay == TimeOfDay.Sunset) e = new Vector3(16f, daySunEuler.y, 0f);
                if (timeOfDay == TimeOfDay.Night) e = new Vector3(48f, daySunEuler.y + 70f, 0f);
                sun.transform.rotation = Quaternion.Euler(e);
            }
            Color bg = ts != null
                ? (timeOfDay == TimeOfDay.Sunset ? ts.sunsetBackground : timeOfDay == TimeOfDay.Night ? ts.nightBackground : ts.dayBackground)
                : (timeOfDay == TimeOfDay.Sunset ? sunsetBackground : timeOfDay == TimeOfDay.Night ? nightBackground : dayBackground);
            ApplySky(bg);
            bool sky = useSky && skyMaterial != null;
            if (sky && RenderSettings.skybox != skyMaterial) RenderSettings.skybox = skyMaterial;
            if (cameras != null)
                foreach (var c in cameras)
                {
                    if (c == null) continue;
                    // only switch between sky and solid colour (leave depth-only / overlay cameras alone)
                    if (c.clearFlags == CameraClearFlags.SolidColor || c.clearFlags == CameraClearFlags.Skybox)
                    {
                        var want = sky && !c.orthographic ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                        if (c.clearFlags != want) c.clearFlags = want;
                    }
                    c.backgroundColor = bg;
                }
        }

        Texture2D currentPalette;

        // One palette swatch (8 x 8 grid, row-major from the top-left); the fallback when the
        // texture isn't readable.
        static Color SwatchOr(Texture2D pal, int index, Color fallback)
        {
            if (pal == null || !pal.isReadable) return fallback;
            int r = index / 8, c = index % 8;
            return pal.GetPixelBilinear((c + 0.5f) / 8f, 1f - (r + 0.5f) / 8f);
        }

        // Sky colours derived from the theme backdrop and the time of day (ToonTropical/Sky).
        void ApplySky(Color bg)
        {
            int tod = (int)timeOfDay;
            bool night = timeOfDay == TimeOfDay.Night;
            Color zen = Color.Lerp(bg, SkyZenith[tod], 0.8f);
            Color A(Color c) { c.a = 1f; return c; }
            Shader.SetGlobalColor(SkyTopId, A(zen));
            Shader.SetGlobalColor(SkyHorizonId, A(bg));                 // alpha 1 = sky colours are set
            // below the horizon: open sea in the palette's own water colour, so a small level
            // reads as an island in the ocean
            Color sea = SwatchOr(currentPalette, 2, Color.Lerp(new Color(0.25f, 0.62f, 0.82f), zen, 0.3f) * (night ? 0.45f : 1f))
                        * new Color(0.86f, 0.87f, 0.93f);   // the toon mid band, like the water tiles
            Shader.SetGlobalColor(SkyGroundId, A(Color.Lerp(sea, bg, 0.1f)));
            Shader.SetGlobalColor(FxTintId, FxTint[tod]);
            Shader.SetGlobalColor(SkyCloudId, A(night ? Color.Lerp(bg, Color.white, 0.18f) : Color.Lerp(Color.white, bg, 0.15f)));
            Shader.SetGlobalColor(SkyIslandId, A(Color.Lerp(bg, new Color(0.3f, 0.52f, 0.52f), 0.62f) * (night ? 0.55f : 0.92f)));
            Vector3 d = sun != null ? -sun.transform.forward : Vector3.zero;
            Shader.SetGlobalVector(SunDirId, new Vector4(d.x, d.y, d.z, sun != null ? 1f : 0f));
            Color disc = SunDisc[tod];
            Shader.SetGlobalVector(SunColorId, new Vector4(disc.r, disc.g, disc.b, night ? 1f : 0f));
            Shader.SetGlobalColor(InkId, new Color(0.1686f, 0.1216f, 0.1608f, 1f));
        }
    }
}
