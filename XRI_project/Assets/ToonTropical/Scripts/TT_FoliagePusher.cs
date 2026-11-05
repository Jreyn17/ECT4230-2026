// Toon Tropical: put this on a character (player, NPC, animal) and the pack's grass, plants,
// bushes and flowers bend away as it walks through them, then spring back. Works through the
// toon shader's wind sway, so the ink outline and shadows bend too. Up to 4 pushers at once
// (the nearest to the main camera win). No physics or colliders involved.
using System.Collections.Generic;
using UnityEngine;

namespace ToonTropical
{
    [ExecuteAlways]
    [AddComponentMenu("Toon Tropical/TT Foliage Pusher")]
    public class TT_FoliagePusher : MonoBehaviour
    {
        [Tooltip("How far around the character plants bend (metres).")]
        public float radius = 0.7f;

        const int Max = 4;
        static readonly List<TT_FoliagePusher> all = new List<TT_FoliagePusher>();
        static readonly Vector4[] slots = new Vector4[Max];
        static readonly int PushId = Shader.PropertyToID("_TT_Push");
        static int lastFrame = -1;

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }

        void OnDisable()
        {
            all.Remove(this);
            if (all.Count == 0) { System.Array.Clear(slots, 0, Max); Shader.SetGlobalVectorArray(PushId, slots); }
        }

        void LateUpdate()
        {
            if (lastFrame == Time.frameCount && Application.isPlaying) return;   // once per frame
            lastFrame = Time.frameCount;
            var cam = Camera.main;
            if (all.Count > Max && cam != null)                                  // keep the closest
            {
                var c = cam.transform.position;
                all.Sort((a, b) => (a.transform.position - c).sqrMagnitude.CompareTo((b.transform.position - c).sqrMagnitude));
            }
            for (int i = 0; i < Max; i++)
            {
                if (i < all.Count)
                {
                    var p = all[i].transform.position;
                    slots[i] = new Vector4(p.x, p.y, p.z, Mathf.Max(0f, all[i].radius));
                }
                else slots[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(PushId, slots);
        }
    }
}
