using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AICompanion.Services
{
    /// <summary>
    /// Samples a bundled Humanoid AnimationClip after the normal avatar
    /// update. The head is masked so UniVRM can continue aiming the face and
    /// eyes independently of the body pose.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RuntimeHumanoidPosePlayer : MonoBehaviour
    {
        private const string PoseResourcePath =
            "PicnicPoses/CrissCross";

        private Animator animator;
        private AnimationClip poseClip;
        private AvatarMask bodyMask;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private AnimationLayerMixerPlayable layerMixer;
        private bool graphReady;
        private bool poseActive;
        private float surfaceY = 0.02f;
        private int groundingFrames;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            poseClip = Resources.Load<AnimationClip>(
                PoseResourcePath);

            if (animator == null ||
                !animator.isHuman ||
                poseClip == null)
            {
                Debug.LogError(
                    "[CharacterPose] CrissCross must be a " +
                    "Unity AnimationClip on a Humanoid avatar.");
                enabled = false;
                return;
            }

            BuildGraph();
        }

        public void SetPoseActive(
            bool active,
            float blanketSurfaceY)
        {
            bool startingPose = active && !poseActive;
            poseActive = active;
            surfaceY = blanketSurfaceY;
            if (startingPose)
            {
                groundingFrames = 8;
            }
        }

        private void BuildGraph()
        {
            bodyMask = new AvatarMask();
            for (int i = 0;
                 i < (int)AvatarMaskBodyPart.LastBodyPart;
                 i++)
            {
                bodyMask.SetHumanoidBodyPartActive(
                    (AvatarMaskBodyPart)i,
                    true);
            }
            bodyMask.SetHumanoidBodyPartActive(
                AvatarMaskBodyPart.Head,
                false);

            graph = PlayableGraph.Create(
                $"CharacterPose_{gameObject.name}");
            graph.SetTimeUpdateMode(
                DirectorUpdateMode.Manual);

            clipPlayable =
                AnimationClipPlayable.Create(
                    graph,
                    poseClip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);

            layerMixer =
                AnimationLayerMixerPlayable.Create(
                    graph,
                    1);
            graph.Connect(
                clipPlayable,
                0,
                layerMixer,
                0);
            layerMixer.SetInputWeight(0, 1f);
            layerMixer.SetLayerMaskFromAvatarMask(
                0,
                bodyMask);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    graph,
                    "Character Pose",
                    animator);
            output.SetSourcePlayable(layerMixer);
            graph.Play();
            graphReady = true;
        }

        private void LateUpdate()
        {
            if (!graphReady || !poseActive)
            {
                return;
            }

            // The exported clip is a gentle loop. Let it advance normally,
            // wrapping at the end, while evaluating after other avatar graphs.
            double time =
                poseClip.length > 0.001f
                    ? Time.timeAsDouble % poseClip.length
                    : 0d;
            clipPlayable.SetTime(time);
            graph.Evaluate(0f);

            if (groundingFrames > 0)
            {
                GroundToBlanket();
                groundingFrames--;
            }
        }

        private void GroundToBlanket()
        {
            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds();
            bool found = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.bounds.size.sqrMagnitude < 0.0025f)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!found)
            {
                return;
            }

            float targetMinimumY = surfaceY - 0.025f;
            transform.position +=
                Vector3.up *
                (targetMinimumY - bounds.min.y);
        }

        private void OnDestroy()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            if (bodyMask != null)
            {
                Destroy(bodyMask);
            }
        }
    }
}
