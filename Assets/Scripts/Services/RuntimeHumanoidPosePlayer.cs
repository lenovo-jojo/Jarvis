using System;
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
    // Run after VRM10Service (11000) so saved VRMA/VPD playback cannot
    // overwrite the shared Character Mode body pose.
    [DefaultExecutionOrder(12000)]
    public sealed class RuntimeHumanoidPosePlayer : MonoBehaviour
    {
        [Serializable]
        private sealed class FavoritePoseData
        {
            public Vector3 bodyPosition;
            public Quaternion bodyRotation;
            public float[] muscles;
        }

        private const string PoseResourcePath =
            "PicnicPoses/CrissCross";
        private const string DefaultFavoritePosePath =
            "CharacterPoses/criss cross/pose";

        private Animator animator;
        private AnimationClip poseClip;
        private HumanPoseHandler humanPoseHandler;
        private HumanPose favoritePose;
        private HumanPose currentPose;
        private bool favoritePoseReady;
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

            if (animator == null ||
                !animator.isHuman)
            {
                Debug.LogError(
                    "[CharacterPose] Character Mode requires " +
                    "a Humanoid avatar.");
                enabled = false;
                return;
            }

            humanPoseHandler =
                new HumanPoseHandler(
                    animator.avatar,
                    animator.transform);

            if (!TryLoadFavoritePose(DefaultFavoritePosePath))
            {
                poseClip = Resources.Load<AnimationClip>(
                    PoseResourcePath);
                if (poseClip == null)
                {
                    Debug.LogError(
                        "[CharacterPose] No usable criss-cross " +
                        "favorite pose or AnimationClip was found.");
                    enabled = false;
                    return;
                }

                BuildGraph();
            }
        }

        public bool TryLoadFavoritePose(string resourcePath)
        {
            TextAsset poseAsset =
                Resources.Load<TextAsset>(resourcePath);
            FavoritePoseData data =
                poseAsset != null
                    ? JsonUtility.FromJson<FavoritePoseData>(
                        poseAsset.text)
                    : null;

            if (data == null ||
                data.muscles == null ||
                data.muscles.Length != HumanTrait.MuscleCount)
            {
                return false;
            }

            favoritePose = new HumanPose
            {
                bodyPosition = data.bodyPosition,
                bodyRotation = data.bodyRotation,
                muscles = (float[])data.muscles.Clone()
            };
            currentPose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };
            favoritePoseReady = true;
            return true;
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
            if (!poseActive)
            {
                return;
            }

            if (favoritePoseReady)
            {
                ApplyFavoritePose();
            }
            else if (graphReady)
            {
                // Retain the older looping clip as a fallback for projects
                // that do not yet contain the favorite-pose library.
                double time =
                    poseClip.length > 0.001f
                        ? Time.timeAsDouble % poseClip.length
                        : 0d;
                clipPlayable.SetTime(time);
                graph.Evaluate(0f);
            }

            if (groundingFrames > 0)
            {
                GroundToBlanket();
                groundingFrames--;
            }
        }

        private void ApplyFavoritePose()
        {
            humanPoseHandler.GetHumanPose(ref currentPose);

            // Keep the live head/face aim from UniVRM while applying the
            // favorited body pose to the rest of the humanoid.
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscleName = HumanTrait.MuscleName[i];
                if (!IsHeadMuscle(muscleName))
                {
                    currentPose.muscles[i] =
                        favoritePose.muscles[i];
                }
            }

            currentPose.bodyPosition =
                favoritePose.bodyPosition;
            currentPose.bodyRotation =
                favoritePose.bodyRotation;
            humanPoseHandler.SetHumanPose(ref currentPose);
        }

        private static bool IsHeadMuscle(string muscleName)
        {
            return muscleName.IndexOf(
                       "Neck",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf(
                       "Head",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf(
                       "Eye",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   muscleName.IndexOf(
                       "Jaw",
                       StringComparison.OrdinalIgnoreCase) >= 0;
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

            humanPoseHandler?.Dispose();
        }
    }
}
