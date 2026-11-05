// Toon Tropical - plays one of a character's clips when it is enabled (Idle, Walk, ...),
// with an optional start offset so several copies don't move in sync. The prefabs'
// Animator controllers also have one trigger per clip for switching at runtime.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ToonTropical
{
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Toon Tropical/TT Character Clip")]
    public class TT_CharacterClip : MonoBehaviour
    {
        public string clip = "Idle";
        [Range(0f, 1f)] public float startOffset;

        void OnEnable()
        {
            var a = GetComponent<Animator>();
            if (a != null && a.runtimeAnimatorController != null)
                a.Play(clip, 0, startOffset);
        }

        /// Pose every TT Character Clip in the open scenes at time t (screenshots, edit mode).
        /// Humanoid poses are sampled together: each editor sampling pass resets the last one.
        public static void SampleAll(float t)
        {
            var all = FindObjectsByType<TT_CharacterClip>(FindObjectsSortMode.None);
#if UNITY_EDITOR
            bool block = !Application.isPlaying;
            if (block)
            {
                if (!UnityEditor.AnimationMode.InAnimationMode()) UnityEditor.AnimationMode.StartAnimationMode();
                UnityEditor.AnimationMode.BeginSampling();
            }
            foreach (var c in all) c.SampleCore(t, block);
            if (block) UnityEditor.AnimationMode.EndSampling();
#else
            foreach (var c in all) c.SampleCore(t, false);
#endif
        }

        /// Pose this character at time t (seconds) without entering play mode (screenshots).
        public void Sample(float t)
        {
#if UNITY_EDITOR
            bool block = !Application.isPlaying && GetComponent<Animator>() is Animator an && an.isHuman;
            if (block)
            {
                if (!UnityEditor.AnimationMode.InAnimationMode()) UnityEditor.AnimationMode.StartAnimationMode();
                UnityEditor.AnimationMode.BeginSampling();
            }
            SampleCore(t, block);
            if (block) UnityEditor.AnimationMode.EndSampling();
#else
            SampleCore(t, false);
#endif
        }

        void SampleCore(float t, bool editorBlock)
        {
            var a = GetComponent<Animator>();
            if (a == null || a.runtimeAnimatorController == null) return;
            foreach (var c in a.runtimeAnimatorController.animationClips)
                if (c.name == clip)
                {
                    float time = (t + startOffset * c.length) % c.length;
                    if (a.isHuman)
                    {
                        // humanoid (muscle) clips can't be sampled straight onto the transforms
#if UNITY_EDITOR
                        if (editorBlock)
                        {
                            UnityEditor.AnimationMode.SampleAnimationClip(gameObject, c, time);
                            return;
                        }
#endif
                        var graph = PlayableGraph.Create("TT_Sample");
                        var output = AnimationPlayableOutput.Create(graph, "pose", a);
                        var play = AnimationClipPlayable.Create(graph, c);
                        output.SetSourcePlayable(play);
                        play.SetTime(time);
                        graph.Evaluate();
                        graph.Destroy();
                    }
                    else c.SampleAnimation(gameObject, time);
                    return;
                }
        }
    }
}
