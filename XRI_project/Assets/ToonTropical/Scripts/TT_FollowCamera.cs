// Toon Tropical: orbit camera that follows a target (walk-around demo).
// Hold a mouse button and drag (or use the right stick) to orbit, scroll to zoom.
using UnityEngine;

namespace ToonTropical
{
    [AddComponentMenu("Toon Tropical/TT Follow Camera")]
    public class TT_FollowCamera : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Point on the target to look at (above its feet).")]
        public Vector3 targetOffset = new Vector3(0f, 1.1f, 0f);
        public float distance = 6f;
        public float minDistance = 3f;
        public float maxDistance = 14f;
        public float yaw = 0f;
        public float pitch = 24f;
        public float minPitch = 5f;
        public float maxPitch = 70f;
        public float orbitSpeed = 3f;
        public float followSharpness = 10f;

        Vector3 focus;

        void Start()
        {
            if (target != null) { focus = target.position + targetOffset; lastTarget = target.position; }
            current = distance;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var look = TT_DemoInput.Look();
            yaw += look.x * orbitSpeed;

            // swing gently behind the target while it walks and the player isn't steering the view
            // (only for mostly-forward walking: sideways or toward the camera would spin the view)
            var vel = (target.position - lastTarget) / Mathf.Max(Time.deltaTime, 1e-4f);
            lastTarget = target.position;
            vel.y = 0f;
            if (autoFollow > 0f && look.sqrMagnitude < 1e-4f && vel.sqrMagnitude > 0.25f)
            {
                float heading = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(yaw, heading);
                if (Mathf.Abs(diff) < 70f)
                    yaw += diff * (1f - Mathf.Exp(-autoFollow * Time.deltaTime));
            }
            pitch = Mathf.Clamp(pitch - look.y * orbitSpeed, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - TT_DemoInput.Zoom() * 0.8f, minDistance, maxDistance);

            var goal = target.position + targetOffset;
            focus = Vector3.Lerp(focus, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var back = rot * Vector3.back;

            // pull in when a palm, roof or cliff is between the camera and the target, ease back out
            float want = distance;
            if (avoidObstacles)
                foreach (var h in Physics.SphereCastAll(focus, 0.25f, back, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (h.distance > 0f && !h.transform.IsChildOf(target) && h.distance - 0.1f < want)
                        want = Mathf.Max(h.distance - 0.1f, minDistance * 0.4f);
            current = want < current ? want : Mathf.Lerp(current, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
            transform.SetPositionAndRotation(focus + back * current, rot);
        }

        [Tooltip("Move in front of objects that would block the view of the target.")]
        public bool avoidObstacles = true;
        [Tooltip("How quickly the view swings behind a walking target (0 = never).")]
        public float autoFollow = 1.2f;
        float current = 6f;
        Vector3 lastTarget;
    }
}
