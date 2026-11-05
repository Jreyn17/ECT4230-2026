// Toon Tropical: walks a character back and forth while its Walk clip plays (the clips are
// in place, with no root motion). Crabs scuttle sideways without turning; everything else
// walks forward and turns round at each end. Follows the ground and stops short of water
// and obstacles. For ambient life in scenes; drive real NPCs with your own AI instead.
using UnityEngine;

namespace ToonTropical
{
    [AddComponentMenu("Toon Tropical/TT Patrol")]
    public class TT_Patrol : MonoBehaviour
    {
        [Tooltip("Metres per second.")]
        public float speed = 0.3f;
        [Tooltip("How far it walks from its start point each way (metres).")]
        public float distance = 1.2f;
        [Tooltip("Move along the local X axis without turning (crabs).")]
        public bool sideways;
        [Tooltip("Seconds it turns for at each end (walkers that face forward).")]
        public float turnTime = 0.5f;
        public float waterLevel = 0.44f;

        Vector3 start, axis;
        float pos, dir = 1f, turning;
        Quaternion baseRot;

        void Start()
        {
            start = transform.position;
            baseRot = transform.rotation;
            axis = Vector3.ProjectOnPlane(sideways ? transform.right : transform.forward, Vector3.up).normalized;
        }

        void Update()
        {
            if (turning > 0f)                                         // forward walkers: turn round
            {
                turning -= Time.deltaTime;
                float k = 1f - Mathf.Clamp01(turning / turnTime);
                var from = baseRot * Quaternion.Euler(0f, dir > 0f ? 180f : 0f, 0f);
                var to = baseRot * Quaternion.Euler(0f, dir > 0f ? 0f : 180f, 0f);
                transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                return;
            }
            float step = speed * Time.deltaTime * dir;
            var next = start + axis * (pos + step);
            if (Mathf.Abs(pos + step) > distance || !Walkable(next + axis * dir * 0.2f))
            {
                dir = -dir;
                if (!sideways) turning = turnTime;
                return;
            }
            pos += step;
            if (Ground(next, out float y)) next.y = y;
            transform.position = next;
        }

        bool Ground(Vector3 p, out float y)
        {
            y = p.y;
            var from = p + Vector3.up * 0.5f;
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(transform) && h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            return found;
        }

        bool Walkable(Vector3 p)
        {
            if (!Ground(p, out float y) || y <= waterLevel || Mathf.Abs(y - transform.position.y) > 0.3f) return false;
            var a = new Vector3(p.x, y + 0.15f, p.z);
            foreach (var c in Physics.OverlapSphere(a, 0.1f, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(transform) && !c.GetComponent<TerrainCollider>() && c.bounds.max.y > y + 0.12f
                    && !(c is MeshCollider && c.name.Contains("Tile_"))) return false;
            return true;
        }
    }
}
