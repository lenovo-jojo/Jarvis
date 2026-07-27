using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AICompanion.Services
{
    /// <summary>
    /// Retargets a small humanoid locomotion set onto any runtime-loaded VRM.
    /// World movement remains script-driven; the clips only animate the body.
    /// </summary>
    public sealed class PlayerLocomotionAnimator : MonoBehaviour
    {
        private const int IdleInput = 0;
        private const int WalkInput = 1;
        private const int RunInput = 2;
        private const int FlyInput = 3;

        [SerializeField] private float blendSpeed = 6f;

        private Animator animator;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable walkPlayable;
        private AnimationClipPlayable runPlayable;
        private float idleWeight = 1f;
        private float walkWeight;
        private float runWeight;
        private float flyWeight;
        private float requestedSpeed;
        private bool requestedSprint;
        private bool requestedFlying;
        private bool isReady;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning(
                    "[PlayerLocomotion] A humanoid Animator is required.");
                enabled = false;
                return;
            }

            AnimationClip idle = LoadClip(
                "PlayerLocomotion/Idle",
                "Idle");
            AnimationClip walk = LoadClip(
                "PlayerLocomotion/Walk",
                "Walk");
            AnimationClip run = LoadClip(
                "PlayerLocomotion/Run",
                "Run");
            AnimationClip fly = LoadClip(
                "PlayerLocomotion/Fly",
                "Falling");

            if (idle == null ||
                walk == null ||
                run == null ||
                fly == null)
            {
                Debug.LogError(
                    "[PlayerLocomotion] One or more locomotion clips " +
                    "could not be loaded.");
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create(
                $"PlayerLocomotion_{gameObject.name}");
            graph.SetTimeUpdateMode(
                DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(
                graph,
                4);

            AnimationClipPlayable idlePlayable =
                CreateClipPlayable(idle);
            walkPlayable = CreateClipPlayable(walk);
            runPlayable = CreateClipPlayable(run);
            AnimationClipPlayable flyPlayable =
                CreateClipPlayable(fly);

            graph.Connect(
                idlePlayable,
                0,
                mixer,
                IdleInput);
            graph.Connect(
                walkPlayable,
                0,
                mixer,
                WalkInput);
            graph.Connect(
                runPlayable,
                0,
                mixer,
                RunInput);
            graph.Connect(
                flyPlayable,
                0,
                mixer,
                FlyInput);

            mixer.SetInputWeight(IdleInput, 1f);
            mixer.SetInputWeight(WalkInput, 0f);
            mixer.SetInputWeight(RunInput, 0f);
            mixer.SetInputWeight(FlyInput, 0f);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    graph,
                    "Player Locomotion",
                    animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            isReady = true;
        }

        public void SetMotion(
            float horizontalSpeed,
            bool sprinting,
            bool flying)
        {
            requestedSpeed = Mathf.Max(
                0f,
                horizontalSpeed);
            requestedSprint = sprinting;
            requestedFlying = flying;
        }

        private void Update()
        {
            if (!isReady)
            {
                return;
            }

            bool isMoving = requestedSpeed > 0.05f;
            float targetFly =
                requestedFlying ? 1f : 0f;
            float targetRun =
                !requestedFlying &&
                isMoving &&
                requestedSprint
                    ? 1f
                    : 0f;
            float targetWalk =
                !requestedFlying &&
                isMoving &&
                !requestedSprint
                    ? 1f
                    : 0f;
            float targetIdle =
                !requestedFlying &&
                !isMoving
                    ? 1f
                    : 0f;
            float step = blendSpeed * Time.deltaTime;

            idleWeight = Mathf.MoveTowards(
                idleWeight,
                targetIdle,
                step);
            walkWeight = Mathf.MoveTowards(
                walkWeight,
                targetWalk,
                step);
            runWeight = Mathf.MoveTowards(
                runWeight,
                targetRun,
                step);
            flyWeight = Mathf.MoveTowards(
                flyWeight,
                targetFly,
                step);

            float total = Mathf.Max(
                idleWeight +
                walkWeight +
                runWeight +
                flyWeight,
                0.0001f);
            mixer.SetInputWeight(
                IdleInput,
                idleWeight / total);
            mixer.SetInputWeight(
                WalkInput,
                walkWeight / total);
            mixer.SetInputWeight(
                RunInput,
                runWeight / total);
            mixer.SetInputWeight(
                FlyInput,
                flyWeight / total);

            // Keep foot cadence responsive to the actual movement speed
            // without allowing the clips to move the avatar root.
            walkPlayable.SetSpeed(
                Mathf.Clamp(
                    requestedSpeed / 2.5f,
                    0.75f,
                    1.35f));
            runPlayable.SetSpeed(
                Mathf.Clamp(
                    requestedSpeed / 6.25f,
                    0.8f,
                    1.25f));
        }

        private AnimationClipPlayable CreateClipPlayable(
            AnimationClip clip)
        {
            AnimationClipPlayable playable =
                AnimationClipPlayable.Create(
                    graph,
                    clip);
            playable.SetApplyFootIK(true);
            playable.SetApplyPlayableIK(false);
            return playable;
        }

        private static AnimationClip LoadClip(
            string resourcePath,
            string preferredName)
        {
            AnimationClip[] clips =
                Resources.LoadAll<AnimationClip>(
                    resourcePath);
            foreach (AnimationClip clip in clips)
            {
                if (clip != null &&
                    string.Equals(
                        clip.name,
                        preferredName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            foreach (AnimationClip clip in clips)
            {
                if (clip != null &&
                    !clip.name.StartsWith(
                        "__preview__",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }
    }
}
