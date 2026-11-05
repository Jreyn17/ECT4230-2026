// Toon Tropical: simple third-person controller for the walk-around demo (Demo_Explore).
// WASD / left stick to walk (camera relative), Shift to run, E to wave, F to dig.
// Drives the character's Animator through its clip triggers (Idle, Walk, Wave, Dig), so it
// works with the explorer as shipped. Swap in your own controller for a real game.
using UnityEngine;

namespace ToonTropical
{
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Toon Tropical/TT Explorer Controller")]
    public class TT_ExplorerController : MonoBehaviour
    {
        [Tooltip("Camera whose view direction steers the movement (defaults to the main camera).")]
        public Transform cameraTransform;
        public float walkSpeed = 1.4f;
        public float runSpeed = 2.8f;
        [Tooltip("Walk clip playback speed while running.")]
        public float runAnimSpeed = 1.8f;
        public float turnSpeed = 720f;
        public float gravity = 20f;
        [Tooltip("Ground lower than this (the water tiles sit at 0.40 m) is treated as water: " +
                 "the character won't walk into it.")]
        public float waterLevel = 0.44f;

        CharacterController cc;
        Animator anim;
        float fallSpeed;
        string state = "";
        float actionUntil;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
            var clip = GetComponent<TT_CharacterClip>();          // demo idlers: this one is player driven
            if (clip != null) clip.enabled = false;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            bool acting = Time.time < actionUntil;
            if (!acting)
            {
                if (TT_DemoInput.Pressed(KeyCode.E)) { Act("Wave"); return; }
                if (TT_DemoInput.Pressed(KeyCode.F)) { Act("Dig"); return; }
            }

            Vector2 input = acting ? Vector2.zero : TT_DemoInput.Move();
            Vector3 move = Vector3.zero;
            if (input.sqrMagnitude > 0.01f)
            {
                Vector3 fwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                move = fwd * input.y + right * input.x;
                var look = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            bool run = TT_DemoInput.Run();
            Vector3 step = move * (run ? runSpeed : walkSpeed) * Time.deltaTime;
            if (step.sqrMagnitude > 0f && IsWater(transform.position + step * 4f)) step = Vector3.zero;

            fallSpeed = cc.isGrounded ? 1f : fallSpeed + gravity * Time.deltaTime;
            cc.Move(step + Vector3.down * fallSpeed * Time.deltaTime);

            if (!acting)
            {
                bool moving = step.sqrMagnitude > 1e-8f;
                SetState(moving ? "Walk" : "Idle");
                if (anim != null) anim.speed = moving && run ? runAnimSpeed : 1f;
            }
        }

        bool IsWater(Vector3 p)
        {
            // probe from just above the feet (a step up), so palm fronds or roofs overhead don't
            // count as ground; the first surface below decides: dock, sand, or the water tile
            var from = p + Vector3.up * (cc.stepOffset + 0.2f);
            float best = float.MaxValue, groundY = float.MinValue;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.root == transform.root || h.distance >= best) continue;
                best = h.distance;                             // nearest surface below the feet
                groundY = h.point.y;
            }
            return groundY <= waterLevel;                      // water tile, or nothing at all
        }

        void Act(string clip)
        {
            SetState(clip);
            if (anim != null) anim.speed = 1f;
            float len = 1.3f;
            if (anim != null && anim.runtimeAnimatorController != null)
                foreach (var c in anim.runtimeAnimatorController.animationClips)
                    if (c.name == clip) len = c.length;
            actionUntil = Time.time + len;
            state = "";                                        // return to Idle / Walk afterwards
        }

        void SetState(string s)
        {
            if (s == state || anim == null) return;
            foreach (var p in anim.parameters)                 // clear stale triggers, then fire one
                if (p.type == AnimatorControllerParameterType.Trigger) anim.ResetTrigger(p.name);
            anim.SetTrigger(s);
            state = s;
        }
    }
}
