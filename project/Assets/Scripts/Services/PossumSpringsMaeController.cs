using System;
using System.IO;
using UniGLTF;
using UnityEngine;
using VRM;

namespace AICompanion.Services
{
    /// <summary>
    /// Loads Mae's native rigged GLB and keeps its authored animation set.
    /// This intentionally does not force the non-humanoid armature through
    /// Unity's Humanoid or VRM retargeting pipeline.
    /// </summary>
    public sealed class PossumSpringsMaeController :
        MonoBehaviour
    {
        private const float DesiredHeight = 1.62f;
        private const float MaximumCallWalkDistance = 18f;
        private const float NearbyCallSpawnDistance = 7f;
        private static readonly Quaternion
            ModelForwardCorrection =
                Quaternion.Euler(0f, 180f, 0f);
        private GameObject mae;
        private Animation animationPlayer;
        private Transform mouth;
        private Transform player;
        private CharacterSpeechBubble speechBubble;
        private Vector3 mouthBaseScale;
        private Vector3 summonDestination;
        private float rootToFeetHeight;
        private float lookAtPlayerUntil;
        private bool speaking;
        private bool walkingToPlayer;
        private bool hangOutActive;
        private string activeAnimation =
            string.Empty;

        public async void Initialize(
            Transform player,
            PossumSpringsConversationController conversation)
        {
            this.player = player;
            string path = Path.Combine(
                Application.streamingAssetsPath,
                "Characters",
                "Mae",
                "mae_borowski_rigged.glb");
            if (!File.Exists(path))
            {
                Debug.LogError(
                    "[PossumSprings] Mae GLB is missing: " +
                    path);
                return;
            }

            try
            {
                RuntimeGltfInstance instance =
                    await VrmUtility.LoadAsync(
                        path,
                        new RuntimeOnlyAwaitCaller(),
                        loadAnimation: true);
                if (instance == null)
                {
                    Debug.LogError(
                        "[PossumSprings] Mae GLB returned no instance.");
                    return;
                }

                mae = instance.gameObject;
                instance.ShowMeshes();
                mae.name = "TownCharacter_mae";
                mae.transform.SetParent(
                    transform,
                    true);
                NormalizeScale(mae);
                Vector3 spawn =
                    player != null
                        ? player.position +
                          player.right * 2.2f +
                          player.forward * 1.6f
                        : transform.position;
                PlaceOnGround(mae, spawn);
                CacheFootHeight();
                AddSelectionCollider(mae);
                if (mae.GetComponent<
                        CharacterSelectionHighlight>() == null)
                {
                    mae.AddComponent<
                        CharacterSelectionHighlight>();
                }
                speechBubble =
                    mae.GetComponent<
                        CharacterSpeechBubble>();
                if (speechBubble == null)
                {
                    speechBubble =
                        mae.AddComponent<
                            CharacterSpeechBubble>();
                }
                speechBubble.SetBubbleVisible(true);
                speechBubble.SetSpeaking(false);

                animationPlayer =
                    mae.GetComponent<Animation>();
                HoldRestingPose();
                mouth = FindDescendant(
                    mae.transform,
                    "MaeMouth_0");
                if (mouth != null)
                {
                    mouthBaseScale = mouth.localScale;
                }

                conversation?.RegisterCharacter(
                    "mae",
                    mae.transform,
                    SetSpeech,
                    SummonToPlayer,
                    LookAtViewer);
                Debug.Log(
                    "[PossumSprings] Mae joined the town cast with " +
                    $"{instance.AnimationClips.Count} authored animations.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PossumSprings] Mae failed to load: " +
                    exception);
            }
        }

        public void SetSpeech(
            string message,
            bool isSpeaking)
        {
            speaking = isSpeaking;
            if (speechBubble != null)
            {
                speechBubble.SetMessage(message);
                speechBubble.SetSpeaking(isSpeaking);
            }
            if (!speaking &&
                mouth != null)
            {
                mouth.localScale =
                    mouthBaseScale;
            }
        }

        private void Update()
        {
            UpdateSummonMovement();
            if (!speaking || mouth == null)
            {
                return;
            }

            float pulse =
                1f +
                Mathf.Sin(Time.unscaledTime * 18f) *
                0.12f;
            mouth.localScale =
                new Vector3(
                    mouthBaseScale.x,
                    mouthBaseScale.y * pulse,
                    mouthBaseScale.z);
        }

        public void SummonToPlayer()
        {
            if (mae == null || player == null)
            {
                return;
            }

            UpdateHangOutDestination();

            Vector3 flatDistance =
                mae.transform.position -
                player.position;
            flatDistance.y = 0f;
            if (flatDistance.sqrMagnitude >
                MaximumCallWalkDistance *
                MaximumCallWalkDistance)
            {
                Vector3 nearbySpawn =
                    player.position -
                    player.forward *
                    NearbyCallSpawnDistance +
                    player.right * 1.4f;
                nearbySpawn.y =
                    mae.transform.position.y;
                PlaceOnGround(
                    mae,
                    nearbySpawn);
            }

            hangOutActive = true;
            walkingToPlayer = true;
            PlayLoop("Walk");
        }

        public void LookAtViewer()
        {
            if (mae == null || player == null)
            {
                return;
            }

            lookAtPlayerUntil =
                Time.unscaledTime + 2.5f;
            walkingToPlayer = false;
            HoldRestingPose();
            FaceViewer();
        }

        private void UpdateSummonMovement()
        {
            if (!hangOutActive ||
                mae == null ||
                player == null)
            {
                return;
            }

            UpdateHangOutDestination();
            if (Time.unscaledTime <
                lookAtPlayerUntil)
            {
                FaceViewer();
                return;
            }
            Vector3 current = mae.transform.position;
            Vector3 flatDelta =
                summonDestination - current;
            flatDelta.y = 0f;
            if (flatDelta.sqrMagnitude >
                MaximumCallWalkDistance *
                MaximumCallWalkDistance)
            {
                Vector3 nearbySpawn =
                    player.position -
                    player.forward *
                    NearbyCallSpawnDistance +
                    player.right * 1.4f;
                nearbySpawn.y = current.y;
                PlaceOnGround(
                    mae,
                    nearbySpawn);
                current = mae.transform.position;
                flatDelta =
                    summonDestination - current;
                flatDelta.y = 0f;
            }
            if (flatDelta.sqrMagnitude <= 1.44f)
            {
                if (walkingToPlayer)
                {
                    walkingToPlayer = false;
                    FacePlayer();
                    HoldRestingPose();
                }
                return;
            }

            if (!walkingToPlayer)
            {
                walkingToPlayer = true;
                PlayLoop("Walk");
            }
            Vector3 direction =
                flatDelta.normalized;
            mae.transform.rotation =
                Quaternion.Slerp(
                    mae.transform.rotation,
                    Quaternion.LookRotation(
                        direction,
                        Vector3.up) *
                    ModelForwardCorrection,
                    Time.deltaTime * 8f);
            Vector3 next =
                current +
                direction *
                Mathf.Min(
                    2.4f * Time.deltaTime,
                    flatDelta.magnitude);
            next.y = GetStableGroundedY(next);
            mae.transform.position = next;
        }

        private void UpdateHangOutDestination()
        {
            summonDestination =
                player.position -
                player.forward * 1.8f +
                player.right * 1.4f;
            summonDestination.y =
                mae.transform.position.y;
        }

        private void FacePlayer()
        {
            Vector3 direction =
                player.position -
                mae.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                mae.transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up) *
                    ModelForwardCorrection;
            }
        }

        private void FaceViewer()
        {
            Vector3 target =
                Camera.main != null
                    ? Camera.main.transform.position
                    : player.position;
            Vector3 direction =
                target - mae.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                mae.transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up) *
                    ModelForwardCorrection;
            }
        }

        private void PlayLoop(string clipName)
        {
            if (animationPlayer == null)
            {
                return;
            }

            AnimationClip clip =
                animationPlayer.GetClip(clipName);
            if (clip == null)
            {
                foreach (AnimationState state in
                    animationPlayer)
                {
                    clip = state.clip;
                    clipName = state.name;
                    break;
                }
            }
            if (clip == null)
            {
                return;
            }
            if (activeAnimation == clipName &&
                animationPlayer.IsPlaying(clipName))
            {
                return;
            }

            clip.wrapMode = WrapMode.Loop;
            animationPlayer.wrapMode = WrapMode.Loop;
            AnimationState animationState =
                animationPlayer[clipName];
            if (animationState != null)
            {
                animationState.speed = 1f;
                animationState.normalizedTime = 0f;
            }
            animationPlayer.Play(clipName);
            activeAnimation = clipName;
        }

        private void HoldRestingPose()
        {
            if (animationPlayer == null)
            {
                return;
            }

            AnimationState restingState =
                animationPlayer["idle_land"];
            if (restingState == null)
            {
                animationPlayer.Stop();
                activeAnimation = "resting_pose";
                return;
            }

            restingState.wrapMode =
                WrapMode.ClampForever;
            animationPlayer.Play("idle_land");
            restingState.normalizedTime = 1f;
            restingState.speed = 0f;
            animationPlayer.Sample();
            activeAnimation = "resting_pose";
        }

        private void CacheFootHeight()
        {
            if (mae != null &&
                TryGetBounds(
                    mae,
                    out Bounds bounds))
            {
                rootToFeetHeight =
                    mae.transform.position.y -
                    bounds.min.y;
            }
        }

        private float GetStableGroundedY(
            Vector3 desiredPosition)
        {
            if (mae == null)
            {
                return desiredPosition.y;
            }

            Vector3 origin =
                desiredPosition +
                Vector3.up * 20f;
            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    50f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            System.Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(
                        right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    !hit.collider.transform.IsChildOf(
                        mae.transform) &&
                    hit.normal.y >= 0.55f)
                {
                    return hit.point.y +
                           rootToFeetHeight +
                           0.01f;
                }
            }
            return desiredPosition.y;
        }

        private static void NormalizeScale(GameObject character)
        {
            if (!TryGetBounds(
                    character,
                    out Bounds bounds) ||
                bounds.size.y <= 0.01f)
            {
                return;
            }

            float scale =
                DesiredHeight / bounds.size.y;
            character.transform.localScale *=
                scale;
        }

        private static void PlaceOnGround(
            GameObject character,
            Vector3 desiredPosition)
        {
            character.transform.position =
                desiredPosition;
            Physics.SyncTransforms();
            Vector3 origin =
                desiredPosition +
                Vector3.up * 300f;
            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    700f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(
                        right.distance));
            float groundY =
                desiredPosition.y;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    !hit.collider.transform.IsChildOf(
                        character.transform) &&
                    hit.normal.y >= 0.55f)
                {
                    groundY = hit.point.y;
                    break;
                }
            }

            if (TryGetBounds(
                    character,
                    out Bounds bounds))
            {
                Vector3 position =
                    character.transform.position;
                position.y +=
                    groundY - bounds.min.y + 0.01f;
                character.transform.position =
                    position;
            }
        }

        private static void AddSelectionCollider(
            GameObject character)
        {
            if (!TryGetBounds(
                    character,
                    out Bounds bounds))
            {
                return;
            }

            CapsuleCollider collider =
                character.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider =
                    character.AddComponent<
                        CapsuleCollider>();
            }

            Vector3 localCenter =
                character.transform.InverseTransformPoint(
                    bounds.center);
            float worldScale =
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(
                        character.transform.lossyScale.y));
            collider.center = localCenter;
            collider.height =
                bounds.size.y / worldScale;
            collider.radius =
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.z) /
                worldScale *
                0.72f;
            collider.direction = 1;
        }

        private static bool TryGetBounds(
            GameObject character,
            out Bounds bounds)
        {
            bounds =
                new Bounds(
                    character.transform.position,
                    Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in
                character.GetComponentsInChildren<
                    Renderer>(true))
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            return found;
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            foreach (Transform child in
                root.GetComponentsInChildren<
                    Transform>(true))
            {
                if (child.name.Equals(
                        objectName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            return null;
        }
    }
}
