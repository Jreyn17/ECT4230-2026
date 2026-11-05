// Toon Tropical - "Toonify": gives models from other packs (or your own) the Toon Tropical
// look so they sit naturally next to the pack's assets.
//   * Materials: each one becomes a ToonTropical/Toon material in Textured Mode (same cel
//     bands and soft shadows, the model's own texture and colour, tinted by the theme and
//     time of day). Cutout materials (foliage cards) keep alpha clipping, double-sided.
//   * Outlines: an ink hull mesh child (_Outline), like the pack's own prefabs. Hard edges
//     are welded first, so the ink never cracks at corners. Works on skinned meshes too.
// Menu: Tools > Toon Tropical > Toonify Selection...   Generated assets go to
// Assets/ToonTropical_Generated (outside the pack folder, so updates never overwrite them).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToonTropical
{
    public class TT_Toonify : EditorWindow
    {
        public const string GenDir = "Assets/ToonTropical_Generated";
        const string InkPath = "Assets/ToonTropical/Materials/M_Island_Outline.mat";

        [System.Serializable]
        public class Settings
        {
            public bool convertMaterials = true;
            public bool addOutlines = true;
            [Tooltip("Outline width as a fraction of each object's size (otherwise a fixed width in metres).")]
            public bool relativeWidth = true;
            public float relative = 0.012f;     // 1.2 % of the object's largest dimension
            public float metres = 0.012f;
            public float minMetres = 0.007f, maxMetres = 0.04f;
            [Tooltip("No ink around cutout (alpha-clipped) parts such as foliage cards.")]
            public bool skipCutoutOutlines = true;
        }

        Settings settings = new Settings();

        [MenuItem("Tools/Toon Tropical/Toonify Selection...")]
        static void Open() => GetWindow<TT_Toonify>(true, "Toonify", true).minSize = new Vector2(360, 290);

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Gives the selected objects (and their children) the Toon Tropical look: " +
                "cel-shaded materials that keep their own textures, and ink outlines. Works on prefabs " +
                "in a scene; generated assets go to " + GenDir + ".", MessageType.Info);
            var s = settings;
            s.convertMaterials = EditorGUILayout.Toggle("Toon materials", s.convertMaterials);
            s.addOutlines = EditorGUILayout.Toggle("Ink outlines", s.addOutlines);
            using (new EditorGUI.DisabledScope(!s.addOutlines))
            {
                s.relativeWidth = EditorGUILayout.Toggle("Width relative to size", s.relativeWidth);
                if (s.relativeWidth)
                    s.relative = EditorGUILayout.Slider("Width (% of size)", s.relative * 100f, 0.1f, 3f) / 100f;
                else
                    s.metres = EditorGUILayout.Slider("Width (m)", s.metres, 0.002f, 0.06f);
                s.skipCutoutOutlines = EditorGUILayout.Toggle("No ink on cutout parts", s.skipCutoutOutlines);
            }
            GUILayout.Space(8);
            var sel = Selection.gameObjects.Where(g => g.scene.IsValid()).ToArray();
            using (new EditorGUI.DisabledScope(sel.Length == 0))
                if (GUILayout.Button(sel.Length == 0 ? "Select objects in the scene" : $"Toonify {sel.Length} object(s)", GUILayout.Height(30)))
                    Run(sel, s);
            if (GUILayout.Button("Remove outlines from selection"))
                RemoveOutlines(sel);
        }

        // ------------------------------------------------------------------- run --
        public static int Run(GameObject[] roots, Settings s)
        {
            Directory.CreateDirectory(GenDir + "/Materials");
            Directory.CreateDirectory(GenDir + "/Outlines");
            var ink = AssetDatabase.LoadAssetAtPath<Material>(InkPath);
            int count = 0;
            Undo.SetCurrentGroupName("Toonify");
            foreach (var root in roots)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    if (r.name.EndsWith("_Outline") || IsPackMaterial(r.sharedMaterials)) continue;
                    var mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    var mats = r.sharedMaterials;
                    bool[] cutout = mats.Select(IsCutout).ToArray();
                    if (s.convertMaterials)
                    {
                        Undo.RecordObject(r, "Toonify");
                        r.sharedMaterials = mats.Select(ToonMaterial).ToArray();
                    }
                    if (s.addOutlines && ink != null && r.transform.Find(r.name + "_Outline") == null)
                    {
                        var keep = Enumerable.Range(0, mesh.subMeshCount)
                            .Where(i => !(s.skipCutoutOutlines && i < cutout.Length && cutout[i])).ToArray();
                        if (keep.Length > 0) AddOutline(r, mesh, keep, Width(r, s), ink);
                    }
                    count++;
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TT] Toonify: {count} renderer(s)");
            return count;
        }

        static bool IsPackMaterial(Material[] mats) =>
            mats.Any(m => m != null && m.shader != null && m.shader.name.StartsWith("ToonTropical/"));

        static float Width(Renderer r, Settings s)
        {
            if (!s.relativeWidth) return s.metres;
            var b = r.bounds.size;
            return Mathf.Clamp(Mathf.Max(b.x, b.y, b.z) * s.relative, s.minMetres, s.maxMetres);
        }

        // --------------------------------------------------------------- material --
        public static bool IsCutout(Material m)
        {
            if (m == null) return false;
            if (m.IsKeywordEnabled("_ALPHATEST_ON")) return true;                          // URP Lit, Standard
            if (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) return true;
            if (m.HasProperty("_Mode") && Mathf.Approximately(m.GetFloat("_Mode"), 1f)) return true;   // Standard: Cutout
            return m.GetTag("RenderType", false) == "TransparentCutout";
        }

        public static Material ToonMaterial(Material src)
        {
            if (src == null || (src.shader != null && src.shader.name.StartsWith("ToonTropical/"))) return src;
            string path = $"{GenDir}/Materials/{Safe(src.name)}_Toon.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("ToonTropical/Toon")) { name = src.name + "_Toon" };
            string texProp = src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null ? "_BaseMap"
                           : src.HasProperty("_MainTex") ? "_MainTex" : null;
            if (texProp != null)
            {
                m.SetTexture("_BaseMap", src.GetTexture(texProp));
                m.SetTextureScale("_BaseMap", src.GetTextureScale(texProp));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset(texProp));
            }
            Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                    : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Textured", 1f);
            m.EnableKeyword("_TT_TEXTURED");
            m.SetFloat("_AnimateWater", 0f);
            if (IsCutout(src))
            {
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_TT_ALPHACLIP");
                m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                m.SetFloat("_Cull", (float)CullMode.Off);                                  // foliage cards
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- outline --
        static void AddOutline(Renderer r, Mesh mesh, int[] subMeshes, float widthMetres, Material ink)
        {
            Vector3 ls = r.transform.lossyScale;
            float scale = Mathf.Max(1e-4f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
            if (r is SkinnedMeshRenderer) scale = 1f;          // skinned: bind-pose units are already metres-ish
            float width = widthMetres / scale;
            // one hull per (mesh asset, sub-meshes, width): copies of a prop share it
            string id = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long local)
                ? $"{guid.Substring(0, 8)}{local & 0xFFFF:X4}" : $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(mesh) & 0xFFFF:X4}";   // not GetInstanceID: an error from Unity 6.4
            string key = $"{Safe(mesh.name)}_{id}_{string.Join("", subMeshes)}_{Mathf.RoundToInt(width * 10000)}";
            string path = $"{GenDir}/Outlines/{key}.asset";
            var hull = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (hull == null)
            {
                hull = BuildHull(mesh, subMeshes, width);
                AssetDatabase.CreateAsset(hull, path);
            }
            var go = new GameObject(r.name + "_Outline");
            Undo.RegisterCreatedObjectUndo(go, "Toonify");
            go.transform.SetParent(r.transform, false);
            Renderer o;
            if (r is SkinnedMeshRenderer src)
            {
                var sk = go.AddComponent<SkinnedMeshRenderer>();
                sk.sharedMesh = hull;
                sk.bones = src.bones;
                sk.rootBone = src.rootBone;
                sk.localBounds = src.localBounds;
                o = sk;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = hull;
                o = go.AddComponent<MeshRenderer>();
            }
            o.sharedMaterial = ink;
            o.shadowCastingMode = ShadowCastingMode.Off;
            o.receiveShadows = false;
        }

        /// Inverted hull: vertices pushed out along normals averaged over every vertex at the same
        /// position (so split hard edges stay closed), faces flipped, no vertex colours (no
        /// animation mask), bone weights kept for skinned meshes.
        public static Mesh BuildHull(Mesh src, int[] subMeshes, float width)
        {
            var v = src.vertices;
            var acc = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => Vector3Int.RoundToInt(p * 10000f);
            var tris = new List<int>();
            foreach (int s in subMeshes) tris.AddRange(src.GetTriangles(s));
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);                   // area weighted
                foreach (var p in new[] { a, b, c })
                {
                    var k = Key(p);
                    acc[k] = acc.TryGetValue(k, out var e) ? e + n : n;
                }
            }
            var outV = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
                outV[i] = acc.TryGetValue(Key(v[i]), out var n) && n.sqrMagnitude > 1e-12f
                    ? v[i] + n.normalized * width : v[i];
            for (int i = 0; i < tris.Count; i += 3)
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);         // flip
            var hull = new Mesh { name = src.name + "_Outline" };
            hull.indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            hull.vertices = outV;
            hull.colors32 = new Color32[v.Length];                             // mask 0: no sway / flame
            hull.SetTriangles(tris, 0);
            if (src.boneWeights.Length == v.Length)
            {
                hull.boneWeights = src.boneWeights;
                hull.bindposes = src.bindposes;
            }
            hull.RecalculateNormals();
            hull.RecalculateBounds();
            return hull;
        }

        static void RemoveOutlines(GameObject[] roots)
        {
            foreach (var root in roots)
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_Outline")).ToArray())
                    if (t.GetComponent<Renderer>() is Renderer rr && rr.sharedMaterial != null &&
                        rr.sharedMaterial.shader.name == "ToonTropical/Outline" && t.parent != null &&
                        !PrefabUtility.IsPartOfPrefabAsset(t.gameObject) &&
                        !PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                        Undo.DestroyObjectImmediate(t.gameObject);
        }

        static string Safe(string n) => string.Concat(n.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == ' ' ? '_' : ch));
    }
}
