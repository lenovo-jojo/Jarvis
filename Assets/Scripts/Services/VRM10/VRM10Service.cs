using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using AICompanion.Core;
using UniGLTF;
using UniVRM10;
using VRM;

namespace AICompanion.Services
{
    [DefaultExecutionOrder(11000)]
    public class VRM10Service : MonoBehaviour, IVRMService
    {
        [Serializable]
        public class NormalPropData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public string Path;
            public string RelativePath;
            public int LinkedIndex;
            public int LinkedBone;
        }

        [Serializable]
        public class VpdData
        {
            public CameraInfoData CameraInfo;
            public List<VpdCharacter> CharacterList;
            public LightInfoData LightInfo;
            public List<LightPropData> LightPropsList;
            public List<NormalPropData> NormalPropsList;
        }

        [Serializable]
        public class CameraInfoData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Fov;
        }

        [Serializable]
        public class LightInfoData
        {
            public float Intensity;
            public Quaternion Direction;
            public float ShadowStrength;
            public Color LightColor;
        }

        [Serializable]
        public class LightPropData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public int LinkedBone;
            public float Intensity;
            public float Range;
            public Color LightColor;
        }

        [Serializable]
        public class VpdCharacter
        {
            public string HumanPoseJson;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        public class HumanPoseData
        {
            public Vector3 bodyPosition;
            public Quaternion bodyRotation;
            public float[] muscles;
        }

        [Serializable]
        public class PoseLibraryEntry
        {
            public string name;
            public string resourcePath;
        }

        [Serializable]
        public class PoseLibraryManifest
        {
            public List<PoseLibraryEntry> poses;
        }

        private sealed class SatelliteArrival
        {
            public GameObject avatar;
            public int slot;
            public PlayerLocomotionAnimator locomotion;
        }

        private sealed class PlacementState
        {
            public GameObject avatar;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Serializable]
        public class CustomPose
        {
            public string name;
            public string resourcePath;
            public string vrmaPath;
            public string vpdPath;

            // Bone Posing fallback
            public Vector3 bodyPosition;
            public Quaternion bodyRotation;
            public float[] muscles;

            // Character transform scene context
            public Vector3 characterPosition;
            public Quaternion characterRotation;
            public Vector3 characterScale;

            // Camera scene context
            public bool hasCameraInfo;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float cameraFov;

            // Light scene context
            public bool hasLightInfo;
            public float lightIntensity;
            public Quaternion lightDirection;
            public Color lightColor;

            // Light props list
            public List<LightPropData> lightProps;

            // Normal props list
            public List<NormalPropData> normalProps;

#if UNITY_EDITOR
            public AnimationClip animationClip;
#endif
        }

        private GameObject currentAvatarGo;
        private GameObject playerAvatarGo;
        private readonly Dictionary<string, GameObject> worldAvatars =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GameObject, int> worldPoseIndices =
            new Dictionary<GameObject, int>();
        private readonly Dictionary<GameObject, SatelliteArrival>
            satelliteArrivals =
                new Dictionary<GameObject, SatelliteArrival>();
        private readonly Dictionary<GameObject, int>
            satelliteCallSlots =
                new Dictionary<GameObject, int>();
        private readonly Stack<PlacementState>
            placementHistory =
                new Stack<PlacementState>();
        private readonly List<CharacterSpeechBubble> speechBubbles =
            new List<CharacterSpeechBubble>();
        private GameObject selectedCharacterGo;
        private GameObject draggedCharacterGo;
        private GameObject conversationFocusAvatar;
        private GameObject activeSpeakingAvatar;
        private CharacterSpeechBubble companionSpeechBubble;
        private bool speechBubblesVisible = true;
        private string primaryConversationCharacter;
        private bool cameraAttentionRequested;
        private bool cameraAttentionLocked;
        private bool primaryPicnicArranged;
        private int companionStartupPoseIndex = -1;
        private int playerStartupPoseIndex = -1;
        private static readonly string[] StartupSeatedPoseNames =
        {
            "feets to the side sit",
            "floor stretchin sit",
            "sittin knee hugging",
            "cool kid sit"
        };
        private bool isUsingCustomVrm;
        private VRMBlendShapeProxy vrmProxy;

        [Header("World Placement")]
        [SerializeField] private Transform seatingAnchor;
        [SerializeField] private bool snapToSeatingAnchorOnLoad = true;

        private bool isAnimationEnabled = true;

        // VRM 1.0 / VRM Animation runtime state
        private Vrm10Instance vrm10Instance;
        private Vrm10Instance playerVrm10Instance;
        private RuntimeGltfInstance activeVrmaInstance;
        private Vrm10AnimationInstance activeVrmaAnimation;
        private int appliedVpdPoseIndex = -1;
        private bool hasReportedVrmaRetargetFailure;

        // HumanPose handling fallback
        private HumanPoseHandler poseHandler;
        private List<CustomPose> loadedOptionalPoses = new List<CustomPose>();
        private List<string> availablePoseNames = new List<string>();
        private List<GameObject> activeLightProps = new List<GameObject>();
        private List<GameObject> activeNormalProps = new List<GameObject>();
        private List<Renderer> avatarRenderers = new List<Renderer>();

        // PlayableGraph for Animation Clips
        private UnityEngine.Playables.PlayableGraph playableGraph;
        private UnityEngine.Animations.AnimationPlayableOutput playableOutput;
        private UnityEngine.Animations.AnimationClipPlayable clipPlayable;

        private void SpawnLightProps(CustomPose cp)
        {
            if (cp.lightProps == null) return;

            foreach (var lp in cp.lightProps)
            {
                GameObject lightGo = new GameObject("VpdLightProp");
                var lightComp = lightGo.AddComponent<Light>();
                lightComp.type = LightType.Point;
                lightComp.intensity = lp.Intensity;
                lightComp.range = lp.Range;
                lightComp.color = lp.LightColor;

                // Parent to hips if linked, or relative to avatar
                Transform parentTransform = currentAvatarGo != null ? currentAvatarGo.transform : null;
                if (lp.LinkedBone >= 0 && hipsBone != null)
                {
                    parentTransform = hipsBone;
                }

                lightGo.transform.SetParent(parentTransform, false);
                lightGo.transform.localPosition = lp.Position;
                lightGo.transform.localRotation = lp.Rotation;

                activeLightProps.Add(lightGo);
            }
        }

        private void DestroyActiveLightProps()
        {
            foreach (var go in activeLightProps)
            {
                if (go != null) Destroy(go);
            }
            activeLightProps.Clear();
        }

        private string ResolvePropPath(string vpdPath, string relativePath, string absolutePath)
        {
            if (!string.IsNullOrEmpty(relativePath))
            {
                string normalizedRelative = relativePath.Replace('\\', '/');
                string vpdDir = Path.GetDirectoryName(vpdPath);
                string resolvedPath = Path.GetFullPath(Path.Combine(vpdDir, normalizedRelative));
                if (File.Exists(resolvedPath))
                {
                    return resolvedPath;
                }

                string fileName = Path.GetFileName(normalizedRelative);
                string fallbackPath = Path.Combine(Application.dataPath, "OptionalContent/Props", fileName);
                if (File.Exists(fallbackPath))
                {
                    return fallbackPath;
                }
            }

            if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
            {
                return absolutePath;
            }

            return null;
        }

        private async void SpawnNormalProps(CustomPose cp)
        {
            if (cp.normalProps == null) return;

            foreach (var np in cp.normalProps)
            {
                string resolvedPath = ResolvePropPath(cp.vpdPath, np.RelativePath, np.Path);
                if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
                {
                    Debug.LogWarning($"[VRMService] Normal prop file not found: relative={np.RelativePath}, path={np.Path}");
                    continue;
                }

                try
                {
                    RuntimeGltfInstance glbInstance = await GltfUtility.LoadAsync(
                        resolvedPath,
                        new RuntimeOnlyAwaitCaller(),
                        new UrpGltfMaterialDescriptorGenerator()
                    );

                    if (glbInstance != null)
                    {
                        GameObject propGo = glbInstance.gameObject;
                        propGo.name = "VpdNormalProp_" + Path.GetFileNameWithoutExtension(resolvedPath);

                        Transform parentTransform = currentAvatarGo != null ? currentAvatarGo.transform : null;
                        if (np.LinkedBone >= 0 && currentAvatarGo != null)
                        {
                            Animator animator = currentAvatarGo.GetComponent<Animator>();
                            if (animator != null && animator.isHuman)
                            {
                                Transform boneTransform = animator.GetBoneTransform((HumanBodyBones)np.LinkedBone);
                                if (boneTransform != null)
                                {
                                    parentTransform = boneTransform;
                                }
                            }
                        }

                        propGo.transform.SetParent(parentTransform, false);
                        propGo.transform.localPosition = np.Position;
                        propGo.transform.localRotation = np.Rotation;
                        propGo.transform.localScale = np.Scale;

                        glbInstance.ShowMeshes();
                        activeNormalProps.Add(propGo);

                        Debug.Log($"[VRMService] Successfully spawned normal prop: {propGo.name} parented to {(parentTransform != null ? parentTransform.name : "null")}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VRMService] Failed to load normal prop '{resolvedPath}': {ex}");
                }
            }
        }

        private void DestroyActiveNormalProps()
        {
            foreach (var go in activeNormalProps)
            {
                if (go != null) Destroy(go);
            }
            activeNormalProps.Clear();
        }

        // Fallback variables for generic/VRM 1.0 blendshape manipulation
        private SkinnedMeshRenderer fallbackFaceRenderer;
        private int fallbackMouthBlendshapeIdx = -1;
        private int fallbackBlinkBlendshapeIdx = -1;
        private int fallbackJoyBlendshapeIdx = -1;
        private int fallbackSorrowBlendshapeIdx = -1;
        private int fallbackAngryBlendshapeIdx = -1;

        // Pose selection is driven entirely by the pose folders.
        private int currentPoseIndex = -1;
        private int TotalPoses => loadedOptionalPoses.Count;

        // Cache humanoid bones and initial rotations for smooth procedural sways
        private Transform hipsBone;
        private Transform spineBone;
        private Transform neckBone;
        private Transform headBone;
        private Transform leftShoulderBone;
        private Transform rightShoulderBone;
        private Transform leftUpperLegBone;
        private Transform rightUpperLegBone;
        private Transform leftLowerLegBone;
        private Transform rightLowerLegBone;
        private Transform leftFootBone;
        private Transform rightFootBone;
        private Transform leftUpperArmBone;
        private Transform rightUpperArmBone;
        private Transform leftLowerArmBone;
        private Transform rightLowerArmBone;

        private Quaternion hipsInitialRot;
        private Quaternion spineInitialRot;
        private Quaternion neckInitialRot;
        private Quaternion headInitialRot;
        private Quaternion leftShoulderInitialRot;
        private Quaternion rightShoulderInitialRot;
        private Quaternion leftUpperLegInitialRot;
        private Quaternion rightUpperLegInitialRot;
        private Quaternion leftLowerLegInitialRot;
        private Quaternion rightLowerLegInitialRot;
        private Quaternion leftFootInitialRot;
        private Quaternion rightFootInitialRot;
        private Quaternion leftUpperArmInitialRot;
        private Quaternion rightUpperArmInitialRot;
        private Quaternion leftLowerArmInitialRot;
        private Quaternion rightLowerArmInitialRot;

        private Vector3 hipsInitialPos;

        private bool bonesCached = false;

        // Procedural Robot fallback elements
        private Transform headTransform;
        private Transform mouthTransform;
        private Transform leftEyeTransform;
        private Transform rightEyeTransform;

        // Idle movements & timers
        private float idleTime;
        private Vector3 startPosition;

        // Click-and-drag state
        private bool isDragging;
        private Plane dragPlane;
        private Vector3 dragOffset;
        private float verticalVelocity;
        private int groundedPoseIndex = -1;
        private float seatedGroundingTimer;
        private bool hasUserPlacedAvatar;

        private const float BlanketSurfaceY = 0.01f;
        private const float GroundSnapDistance = 0.015f;
        private const float AvatarGravity = -18f;
        private const float SeatedHipHeight = 0.1f;
        private const float HipSnapTolerance = 0.005f;
        private const float SeatedGroundingDelay = 0.2f;
        private const float AllowedSeatedPenetration = 0.05f;

        private bool isBlinking;
        private float blinkTimer;
        private float blinkInterval = 4.0f;
        private bool isSpeaking;

        private float vrmBlinkTimer;
        private float vrmBlinkInterval = 3.0f;
        private float vrmBlinkDuration = 0.15f;
        private bool isVrmBlinking;
        private float currentBlinkWeight;
        private float currentLipSyncWeight;
        private ExpressionKey currentExpressionKey = ExpressionKey.Neutral;
        private float currentExpressionWeight = 1f;

        private void Start()
        {
            CreateVisualStudioStage();
            LoadOptionalPoses();

            // Set up default procedural companion on start
            CreateProceduralCompanion();

            // Recover moved VRM Posing Desktop files before falling back to
            // the bundled companion. This keeps a stale saved path from
            // silently replacing the humanoid companion with the robot.
            ServiceConfig config = ServiceConfig.Load();
            string companionVrmPath =
                ResolveCompanionVrmPath(config.customVrmPath);
            if (!string.IsNullOrEmpty(companionVrmPath))
            {
                if (ShouldPersistResolvedCompanionPath(
                        config.customVrmPath,
                        companionVrmPath))
                {
                    config.customVrmPath = companionVrmPath;
                    config.Save();
                    Debug.Log(
                        "[VRMService] Repaired the saved companion " +
                        $"VRM path: {companionVrmPath}");
                }

                LoadVRM(companionVrmPath, null, null);
            }
        }

        private static string ResolveCompanionVrmPath(
            string savedPath)
        {
            if (!string.IsNullOrWhiteSpace(savedPath) &&
                File.Exists(savedPath))
            {
                return savedPath;
            }

            string savedFileName =
                string.IsNullOrWhiteSpace(savedPath)
                    ? string.Empty
                    : Path.GetFileName(savedPath);
            if (!string.IsNullOrWhiteSpace(savedFileName))
            {
                DirectoryInfo companyDirectory =
                    Directory.GetParent(
                        Application.persistentDataPath);
                string appData =
                    companyDirectory?.Parent?.FullName;
                string posingDesktopPath =
                    Path.Combine(
                        appData ?? string.Empty,
                        "com.evelyngamedev.VrmPosingDesktop",
                        savedFileName);
                if (File.Exists(posingDesktopPath))
                {
                    return posingDesktopPath;
                }
            }

            string bundledCompanion =
                Path.Combine(Application.dataPath, "jdog.vrm");
            return File.Exists(bundledCompanion)
                ? bundledCompanion
                : string.Empty;
        }

        private static bool ShouldPersistResolvedCompanionPath(
            string savedPath,
            string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(savedPath) ||
                string.IsNullOrWhiteSpace(resolvedPath) ||
                string.Equals(
                    savedPath,
                    resolvedPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // Only replace the preference when the original file was found
            // at a new location. Do not erase that preference merely because
            // the bundled fallback had to be used temporarily.
            return string.Equals(
                Path.GetFileName(savedPath),
                Path.GetFileName(resolvedPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private void LoadOptionalPoses()
        {
            loadedOptionalPoses.Clear();
            availablePoseNames.Clear();
            LoadFavoritePoses();

            string soloScenesDir = Path.Combine(
                Application.dataPath,
                "OptionalContent/Solo Scenes");

            if (!Directory.Exists(soloScenesDir))
            {
                Debug.LogWarning(
                    $"[VRMService] Optional Poses directory not found at: {soloScenesDir}");
                return;
            }

            string[] subdirs = Directory.GetDirectories(soloScenesDir);
            Array.Sort(subdirs);

            foreach (string dir in subdirs)
            {
                try
                {
                    string dirName = Path.GetFileName(dir);
                    if (availablePoseNames.Exists(
                            poseName =>
                                poseName.Equals(
                                    dirName,
                                    StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    string[] vrmaFiles = Directory.GetFiles(
                        dir, "*.vrma", SearchOption.AllDirectories);
                    string[] vpdFiles = Directory.GetFiles(
                        dir, "*.vpd", SearchOption.AllDirectories);

                    if (vrmaFiles.Length == 0 && vpdFiles.Length == 0)
                    {
                        continue;
                    }

                    CustomPose cp = new CustomPose
                    {
                        name = dirName,
                        vrmaPath = vrmaFiles.Length > 0 ? vrmaFiles[0] : null,
                        bodyRotation = Quaternion.identity,
                        characterRotation = Quaternion.identity,
                        characterScale = Vector3.one,
                        cameraRotation = Quaternion.identity,
                        cameraFov = 60f,
                        lightDirection = Quaternion.identity,
                        lightColor = Color.white,
                        lightProps = new List<LightPropData>()
                    };

                    // Keep VPD parsing only as a fallback for packs without VRMA.
                    if (vpdFiles.Length > 0)
                    {
                        string json = File.ReadAllText(vpdFiles[0]);
                        VpdData vpd = JsonUtility.FromJson<VpdData>(json);

                        if (vpd != null &&
                            vpd.CharacterList != null &&
                            vpd.CharacterList.Count > 0)
                        {
                            VpdCharacter charData = vpd.CharacterList[0];

                            if (!string.IsNullOrEmpty(charData.HumanPoseJson))
                            {
                                HumanPoseData hpd =
                                    JsonUtility.FromJson<HumanPoseData>(
                                        charData.HumanPoseJson);

                                if (hpd != null)
                                {
                                    cp.bodyPosition = hpd.bodyPosition;
                                    cp.bodyRotation = hpd.bodyRotation;
                                    cp.muscles = hpd.muscles;
                                }
                            }

                            cp.characterPosition = charData.Position;
                            cp.characterRotation = charData.Rotation;
                            cp.characterScale = charData.Scale;
                        }

                        cp.hasCameraInfo = vpd != null && vpd.CameraInfo != null;
                        if (cp.hasCameraInfo)
                        {
                            cp.cameraPosition = vpd.CameraInfo.Position;
                            cp.cameraRotation = vpd.CameraInfo.Rotation;
                            cp.cameraFov = vpd.CameraInfo.Fov;
                        }

                        cp.hasLightInfo = vpd != null && vpd.LightInfo != null;
                        if (cp.hasLightInfo)
                        {
                            cp.lightIntensity = vpd.LightInfo.Intensity;
                            cp.lightDirection = vpd.LightInfo.Direction;
                            cp.lightColor = vpd.LightInfo.LightColor;
                        }

                        cp.vpdPath = vpdFiles[0];
                        if (vpd != null && vpd.LightPropsList != null)
                        {
                            cp.lightProps = vpd.LightPropsList;
                        }

                        if (vpd != null && vpd.NormalPropsList != null)
                        {
                            cp.normalProps = vpd.NormalPropsList;
                        }
                    }

                    loadedOptionalPoses.Add(cp);
                    availablePoseNames.Add(dirName);

                    Debug.Log(
                        $"[VRMService] Loaded pose '{dirName}' " +
                        $"(VRMA: {!string.IsNullOrEmpty(cp.vrmaPath)}, " +
                        $"VPD: {cp.muscles != null && cp.muscles.Length > 0})");
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[VRMService] Error loading pose folder '{dir}': {ex}");
                }
            }
        }

        private void LoadFavoritePoses()
        {
            TextAsset manifestAsset =
                Resources.Load<TextAsset>(
                    "CharacterPoses/index");
            PoseLibraryManifest manifest =
                manifestAsset != null
                    ? JsonUtility.FromJson<PoseLibraryManifest>(
                        manifestAsset.text)
                    : null;

            if (manifest?.poses == null)
            {
                Debug.LogWarning(
                    "[VRMService] Character pose manifest was not found.");
                return;
            }

            foreach (PoseLibraryEntry entry in manifest.poses)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.name) ||
                    string.IsNullOrWhiteSpace(entry.resourcePath))
                {
                    continue;
                }

                TextAsset poseAsset =
                    Resources.Load<TextAsset>(
                        entry.resourcePath);
                HumanPoseData pose =
                    poseAsset != null
                        ? JsonUtility.FromJson<HumanPoseData>(
                            poseAsset.text)
                        : null;

                if (pose?.muscles == null ||
                    pose.muscles.Length !=
                    HumanTrait.MuscleCount)
                {
                    Debug.LogWarning(
                        $"[VRMService] Favorite pose " +
                        $"'{entry.name}' is not a valid humanoid pose.");
                    continue;
                }

                loadedOptionalPoses.Add(
                    new CustomPose
                    {
                        name = entry.name,
                        resourcePath = entry.resourcePath,
                        bodyPosition = pose.bodyPosition,
                        bodyRotation = pose.bodyRotation,
                        muscles = pose.muscles,
                        characterRotation = Quaternion.identity,
                        characterScale = Vector3.one,
                        cameraRotation = Quaternion.identity,
                        cameraFov = 60f,
                        lightDirection = Quaternion.identity,
                        lightColor = Color.white,
                        lightProps = new List<LightPropData>()
                    });
                availablePoseNames.Add(entry.name);
            }

            Debug.Log(
                $"[VRMService] Loaded " +
                $"{availablePoseNames.Count} favorite character poses.");
        }

        public List<string> GetAvailablePoses()
        {
            if (availablePoseNames.Count == 0)
            {
                LoadOptionalPoses();
            }
            return availablePoseNames;
        }

        public async void SetPose(int index)
        {
            if (loadedOptionalPoses.Count == 0)
            {
                Debug.LogWarning("[VRMService] No pose folders were loaded.");
                currentPoseIndex = -1;
                return;
            }

            if (selectedCharacterGo != null &&
                selectedCharacterGo != currentAvatarGo)
            {
                SetWorldCharacterPose(
                    selectedCharacterGo,
                    index);
                return;
            }

            if (vrm10Instance == null)
            {
                Debug.LogWarning(
                    "[VRMService] A VRM 1.0 avatar must finish loading before a pose can play.");
                return;
            }

            StopSatelliteArrival(
                currentAvatarGo);
            currentAvatarGo
                .GetComponent<PlayerLocomotionAnimator>()
                ?.SetPlaybackActive(false);
            currentPoseIndex = Mathf.Clamp(index, 0, loadedOptionalPoses.Count - 1);
            appliedVpdPoseIndex = -1;
            hasReportedVrmaRetargetFailure = false;
            groundedPoseIndex = -1;
            seatedGroundingTimer = 0f;
            CustomPose cp = loadedOptionalPoses[currentPoseIndex];

            Debug.Log(
                $"[VRMService] Pose changed to: {cp.name} " +
                $"(Index: {currentPoseIndex})");

            DestroyActiveLightProps();
            DestroyActiveNormalProps();
            DestroyActiveVrma();

            if (!string.IsNullOrEmpty(cp.vrmaPath) &&
                File.Exists(cp.vrmaPath))
            {
                try
                {
                    using GltfData data =
                        new AutoGltfFileParser(cp.vrmaPath).Parse();
                    var vrmaData = new VrmAnimationData(data);
                    using var loader = new VrmAnimationImporter(vrmaData);

                    activeVrmaInstance = await loader.LoadAsync(
                        new RuntimeOnlyAwaitCaller());

                    if (activeVrmaInstance == null)
                    {
                        throw new Exception(
                            "VrmAnimationImporter returned no runtime instance.");
                    }

                    activeVrmaAnimation =
                        activeVrmaInstance.GetComponent<Vrm10AnimationInstance>();

                    if (activeVrmaAnimation == null)
                    {
                        throw new Exception(
                            "The VRMA did not contain a Vrm10AnimationInstance.");
                    }

                    Animation animation =
                        activeVrmaInstance.GetComponent<Animation>();

                    if (animation != null)
                    {
                        animation.Play();
                    }

                    // UniVRM performs humanoid retargeting from the VRMA
                    // onto the currently loaded VRM 1.0 avatar.
                    vrm10Instance.Runtime.VrmAnimation =
                        activeVrmaAnimation;

                    Debug.Log(
                        $"[VRMService] Playing VRMA pose: {cp.vrmaPath}");

                    ApplyPoseSceneContext(cp);
                    EnsureAvatarCollider(
                        currentAvatarGo);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[VRMService] Failed to load VRMA for " +
                        $"'{cp.name}': {ex}");
                }
            }

            // Fall back to the legacy VPD pose only when no compatible native
            // VRMA is available. Reapplying VPD bones every frame can fight
            // spring physics on avatars other than the pose author's model.
            if (poseHandler != null &&
                cp.muscles != null &&
                cp.muscles.Length > 0)
            {
                HumanPose hp = new HumanPose
                {
                    bodyPosition = cp.bodyPosition,
                    bodyRotation = cp.bodyRotation,
                    muscles = cp.muscles
                };

                poseHandler.SetHumanPose(ref hp);
                appliedVpdPoseIndex = currentPoseIndex;

                Debug.Log($"[VRMService] Applied fallback VPD pose: {cp.name}");
                ApplyPoseSceneContext(cp);
                EnsureAvatarCollider(
                    currentAvatarGo);
                return;
            }

            Debug.LogWarning(
                $"[VRMService] Pose '{cp.name}' has no usable VPD data or compatible VRMA animation.");

            ApplyPoseSceneContext(cp);
        }

        private void DestroyActiveVrma()
        {
            if (vrm10Instance != null &&
                vrm10Instance.Runtime != null)
            {
                vrm10Instance.Runtime.VrmAnimation = null;
            }

            activeVrmaAnimation = null;

            if (activeVrmaInstance != null)
            {
                Destroy(activeVrmaInstance.gameObject);
                activeVrmaInstance = null;
            }
        }

        private void ApplyPoseSceneContext(CustomPose cp)
        {
            // Do not apply the pack's camera or avatar root transform.
            // Those values belong to the author's scene and would move the
            // companion away from the user's chosen screen position.

            if (cp.hasLightInfo)
            {
                Light dirLight = GameObject.FindAnyObjectByType<Light>();
                if (dirLight != null &&
                    dirLight.type == LightType.Directional)
                {
                    dirLight.intensity = cp.lightIntensity;
                    dirLight.transform.rotation = cp.lightDirection;
                    dirLight.color = cp.lightColor;
                }
            }

            SpawnLightProps(cp);
            SpawnNormalProps(cp);
        }

        private void Update()
        {
            if (currentAvatarGo == null) return;

            idleTime += Time.deltaTime;

            HandleDragging();
            UpdateSatelliteArrivals();

            // Custom VRMs use runtime LookAt below. Keep the procedural
            // fallback aligned with the same active gaze target.
            Transform activeLookTarget =
                ResolveCompanionLookTarget();
            UpdatePlayerLookTarget();
            if (!isUsingCustomVrm &&
                activeLookTarget != null)
            {
                Vector3 lookPos =
                    activeLookTarget.position;
                lookPos.y = currentAvatarGo.transform.position.y;
                Vector3 direction = lookPos - currentAvatarGo.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    currentAvatarGo.transform.rotation = Quaternion.Slerp(
                        currentAvatarGo.transform.rotation,
                        targetRotation,
                        Time.deltaTime * 6f
                    );
                }
            }

            if (isUsingCustomVrm)
            {
                if (vrm10Instance != null && vrm10Instance.Runtime != null)
                {
                    // 1. Look at the conversation partner in Character Mode,
                    // or at the camera in Explore Mode.
                    Transform lookTarget =
                        ResolveCompanionLookTarget();
                    if (lookTarget != null)
                    {
                        vrm10Instance.Runtime.LookAt.LookAtInput = new LookAtInput
                        {
                            WorldPosition = lookTarget.position
                        };
                    }

                    // 2. Auto-blinking
                    vrmBlinkTimer += Time.deltaTime;
                    if (isVrmBlinking)
                    {
                        float progress = vrmBlinkTimer / vrmBlinkDuration;
                        if (progress >= 1f)
                        {
                            isVrmBlinking = false;
                            vrmBlinkTimer = 0f;
                            vrmBlinkInterval = UnityEngine.Random.Range(2.5f, 5.5f);
                            currentBlinkWeight = 0f;
                        }
                        else
                        {
                            currentBlinkWeight = Mathf.Sin(progress * Mathf.PI);
                        }
                    }
                    else if (vrmBlinkTimer >= vrmBlinkInterval)
                    {
                        isVrmBlinking = true;
                        vrmBlinkTimer = 0f;
                    }
                }

                if (vrmProxy != null)
                {
                    vrmProxy.Apply();
                }
            }
            else
            {
                // Procedural robot idle movements
                float hover = Mathf.Sin(idleTime * 1.5f) * 0.15f;
                currentAvatarGo.transform.localPosition = startPosition + new Vector3(0, hover, 0);

                // Head lookat
                if (headTransform != null &&
                    activeLookTarget != null)
                {
                    Vector3 direction =
                        activeLookTarget.position -
                        headTransform.position;
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    headTransform.rotation = Quaternion.Slerp(headTransform.rotation, lookRotation, Time.deltaTime * 2.0f);
                }

                // Blinking for procedural mesh
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkInterval)
                {
                    isBlinking = true;
                    blinkTimer = 0f;
                    blinkInterval = UnityEngine.Random.Range(3.0f, 6.0f);
                }

                if (isBlinking)
                {
                    Vector3 eyeScale = leftEyeTransform.localScale;
                    eyeScale.y = Mathf.Lerp(eyeScale.y, 0.05f, Time.deltaTime * 25.0f);
                    leftEyeTransform.localScale = eyeScale;
                    rightEyeTransform.localScale = eyeScale;

                    if (eyeScale.y <= 0.06f)
                    {
                        isBlinking = false;
                    }
                }
                else
                {
                    Vector3 eyeScale = leftEyeTransform.localScale;
                    eyeScale.y = Mathf.Lerp(eyeScale.y, 0.2f, Time.deltaTime * 10.0f);
                    leftEyeTransform.localScale = eyeScale;
                    rightEyeTransform.localScale = eyeScale;
                }
            }
        }

        private void HandleDragging()
        {
            if (Camera.main == null || UnityEngine.InputSystem.Mouse.current == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mousePosition = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null &&
                    EventSystem.current
                        .IsPointerOverGameObject())
                {
                    isDragging = false;
                    draggedCharacterGo = null;
                    return;
                }

                Ray ray = Camera.main.ScreenPointToRay(mousePosition);

                if (Physics.Raycast(
                        ray,
                        out RaycastHit hit,
                        Mathf.Infinity,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Collide))
                {
                    GameObject clickedCharacter =
                        ResolveManagedCharacter(hit.transform);
                    if (clickedCharacter == null)
                    {
                        SelectCharacter(null);
                        return;
                    }

                    // First click selects and highlights. A later drag on the
                    // already-selected character moves it.
                    if (clickedCharacter != selectedCharacterGo)
                    {
                        SelectCharacter(clickedCharacter);
                        return;
                    }

                    draggedCharacterGo = clickedCharacter;
                    RememberPlacement(
                        draggedCharacterGo);
                    StopSatelliteArrival(
                        draggedCharacterGo);
                    dragPlane = new Plane(
                        Camera.main.transform.forward,
                        draggedCharacterGo.transform.position);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        dragOffset =
                            draggedCharacterGo.transform.position -
                            ray.GetPoint(enter);
                        isDragging = true;
                        if (draggedCharacterGo == currentAvatarGo)
                        {
                            hasUserPlacedAvatar = true;
                        }
                        verticalVelocity = 0f;
                    }
                }
                else
                {
                    SelectCharacter(null);
                }
            }

            if (isDragging &&
                draggedCharacterGo != null &&
                mouse.leftButton.isPressed)
            {
                Ray ray = Camera.main.ScreenPointToRay(mousePosition);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 newPosition = ray.GetPoint(enter) + dragOffset;
                    draggedCharacterGo.transform.position = newPosition;

                    // Keep procedural hover centered around the dragged location.
                    if (draggedCharacterGo == currentAvatarGo &&
                        !isUsingCustomVrm)
                    {
                        startPosition = newPosition;
                    }
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                bool releasedOverUi =
                    EventSystem.current != null &&
                    EventSystem.current
                        .IsPointerOverGameObject();
                if (!isDragging &&
                    releasedOverUi)
                {
                    SelectCharacter(null);
                }
                if (isDragging &&
                    draggedCharacterGo != null)
                {
                    SnapCharacterToFloor(
                        draggedCharacterGo);
                }
                isDragging = false;
                draggedCharacterGo = null;
                verticalVelocity = 0f;
                groundedPoseIndex = -1;
                seatedGroundingTimer = 0f;
            }
        }

        private GameObject ResolveManagedCharacter(
            Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return null;
            }

            if (currentAvatarGo != null &&
                hitTransform.IsChildOf(
                    currentAvatarGo.transform))
            {
                return currentAvatarGo;
            }

            if (playerAvatarGo != null &&
                IsAvatarVisiblySelectable(
                    playerAvatarGo) &&
                hitTransform.IsChildOf(
                    playerAvatarGo.transform))
            {
                return playerAvatarGo;
            }

            foreach (GameObject avatar in worldAvatars.Values)
            {
                if (avatar != null &&
                    hitTransform.IsChildOf(avatar.transform))
                {
                    return avatar;
                }
            }

            return null;
        }

        private static bool IsAvatarVisiblySelectable(
            GameObject avatar)
        {
            if (avatar == null ||
                !avatar.activeInHierarchy)
            {
                return false;
            }

            foreach (Renderer renderer in
                avatar.GetComponentsInChildren<Renderer>(
                    true))
            {
                if (renderer != null &&
                    renderer.enabled &&
                    renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

        private void SelectCharacter(GameObject character)
        {
            if (selectedCharacterGo == character)
            {
                if (selectedCharacterGo != null)
                {
                    CharacterSelectionHighlight current =
                        selectedCharacterGo.GetComponent<
                            CharacterSelectionHighlight>();
                    if (current == null)
                    {
                        current =
                            selectedCharacterGo.AddComponent<
                                CharacterSelectionHighlight>();
                    }
                    current.SetSelected(true);
                }
                return;
            }

            if (selectedCharacterGo != null)
            {
                CharacterSelectionHighlight previous =
                    selectedCharacterGo.GetComponent<
                        CharacterSelectionHighlight>();
                previous?.SetSelected(false);
            }

            selectedCharacterGo = character;
            if (selectedCharacterGo == null)
            {
                return;
            }

            CharacterSelectionHighlight highlight =
                selectedCharacterGo.GetComponent<
                    CharacterSelectionHighlight>();
            if (highlight == null)
            {
                highlight =
                    selectedCharacterGo.AddComponent<
                        CharacterSelectionHighlight>();
            }
            highlight.SetSelected(true);
        }

        private void LateUpdate()
        {
            if (!isUsingCustomVrm || !isAnimationEnabled)
            {
                ApplyCameraAttention();
                return;
            }

            // 1. Procedural breathing
            if (spineBone != null)
            {
                // Breathing cycle: sine wave with period of ~3.2 seconds
                float breathValue = Mathf.Sin(Time.time * (2f * Mathf.PI / 3.2f));

                // Breath value is -1 to 1. Map to a subtle rotation offset (pitch/tilt)
                float breathPitch = breathValue * 1.0f;
                // Add a very subtle expansion (scale) as well!
                spineBone.localScale = new Vector3(
                    1f + breathValue * 0.005f,
                    1f + breathValue * 0.002f,
                    1f + breathValue * 0.01f
                );

                spineBone.localRotation = spineInitialRot * Quaternion.Euler(breathPitch, 0f, 0f);
            }

            // Very subtle vertical movement of the hips to match breathing
            if (hipsBone != null)
            {
                float breathValue = Mathf.Sin(Time.time * (2f * Mathf.PI / 3.2f));
                hipsBone.localPosition = hipsInitialPos + new Vector3(0f, breathValue * 0.003f, 0f);
            }

            // 2. Let the animated avatar settle naturally onto the blanket.
            // A Rigidbody would fight VRMA root motion and manual dragging, so
            // gravity is applied to the avatar root using its rendered bounds.
            ApplyBlanketGravity();

            if (currentPoseIndex < 0 ||
                currentPoseIndex >= loadedOptionalPoses.Count)
            {
                ProcessVrmRuntime();
                ApplyCameraAttention();
                return;
            }

            // VRMA is retargeted by Vrm10Instance.Runtime during its own update.
            // Never overwrite humanoid bones here while a VRMA is connected.
            if (activeVrmaAnimation != null)
            {
                ProcessVrmRuntime();
                ApplyCameraAttention();
                return;
            }

            CustomPose cp = loadedOptionalPoses[currentPoseIndex];

            // Static VPD fallback must be reapplied after other animation updates.
            if (poseHandler != null &&
                cp.muscles != null &&
                cp.muscles.Length > 0 &&
                appliedVpdPoseIndex != currentPoseIndex)
            {
                HumanPose hp = new HumanPose
                {
                    bodyPosition = cp.bodyPosition,
                    bodyRotation = cp.bodyRotation,
                    muscles = cp.muscles
                };

                poseHandler.SetHumanPose(ref hp);
                appliedVpdPoseIndex = currentPoseIndex;
            }

            ApplyExpressiveLookAndMotion();
            ProcessVrmRuntime();
            ApplyCameraAttention();
        }

        private void ApplyBlanketGravity()
        {
            if (isDragging || currentAvatarGo == null)
            {
                return;
            }

            if (ShouldSnapHipsToBlanket())
            {
                if (groundedPoseIndex == currentPoseIndex)
                {
                    verticalVelocity = 0f;
                    return;
                }

                if (activeVrmaAnimation == null &&
                    appliedVpdPoseIndex != currentPoseIndex)
                {
                    return;
                }

                seatedGroundingTimer += Time.deltaTime;
                if (seatedGroundingTimer < SeatedGroundingDelay)
                {
                    return;
                }

                float surfaceY = GetSeatingSurfaceY();
                float targetHipY = surfaceY + SeatedHipHeight;
                float hipCorrection = targetHipY - hipsBone.position.y;
                Bounds seatedBounds = GetAvatarBounds();
                float penetrationCorrection =
                    surfaceY -
                    AllowedSeatedPenetration -
                    seatedBounds.min.y;

                // Settle once after the pose has taken effect, allowing a
                // small overlap when the authored pose needs it. Repeating
                // this correction every frame fights the animation loop and
                // produces visible jitter.
                float safeCorrection =
                    Mathf.Max(hipCorrection, penetrationCorrection);

                if (Mathf.Abs(safeCorrection) > HipSnapTolerance)
                {
                    currentAvatarGo.transform.position +=
                        Vector3.up * safeCorrection;
                }

                if (seatingAnchor != null &&
                    snapToSeatingAnchorOnLoad &&
                    !hasUserPlacedAvatar)
                {
                    Vector3 anchoredPosition =
                        currentAvatarGo.transform.position;
                    anchoredPosition.x = seatingAnchor.position.x;
                    anchoredPosition.z = seatingAnchor.position.z;
                    currentAvatarGo.transform.position = anchoredPosition;
                }

                groundedPoseIndex = currentPoseIndex;
                seatedGroundingTimer = 0f;
                verticalVelocity = 0f;
                return;
            }

            groundedPoseIndex = -1;
            seatedGroundingTimer = 0f;

            float groundSurfaceY = GetSeatingSurfaceY();
            Bounds bounds = GetAvatarBounds();
            float heightAboveBlanket =
                bounds.min.y - groundSurfaceY;

            if (heightAboveBlanket > GroundSnapDistance)
            {
                verticalVelocity += AvatarGravity * Time.deltaTime;
                float fallDistance = verticalVelocity * Time.deltaTime;

                if (-fallDistance >= heightAboveBlanket)
                {
                    fallDistance = -heightAboveBlanket;
                    verticalVelocity = 0f;
                }

                currentAvatarGo.transform.position +=
                    Vector3.up * fallDistance;
                return;
            }

            if (heightAboveBlanket < -GroundSnapDistance)
            {
                currentAvatarGo.transform.position +=
                    Vector3.up * -heightAboveBlanket;
            }

            verticalVelocity = 0f;
        }

        private float GetSeatingSurfaceY()
        {
            if (seatingAnchor == null)
            {
                return BlanketSurfaceY;
            }

            Renderer anchorRenderer =
                seatingAnchor.GetComponent<Renderer>();
            return anchorRenderer != null
                ? anchorRenderer.bounds.max.y
                : seatingAnchor.position.y;
        }

        private bool ShouldSnapHipsToBlanket()
        {
            if (hipsBone == null ||
                currentPoseIndex < 0 ||
                currentPoseIndex >= loadedOptionalPoses.Count)
            {
                return false;
            }

            string poseName = loadedOptionalPoses[currentPoseIndex].name;
            return poseName.Equals(
                       "feets to the side sit",
                       StringComparison.OrdinalIgnoreCase) ||
                   poseName.Equals(
                       "Feet Focus",
                       StringComparison.OrdinalIgnoreCase) ||
                   poseName.Equals(
                       "Optional Pose C",
                       StringComparison.OrdinalIgnoreCase) ||
                   poseName.Equals(
                       "Holding Legs Up",
                       StringComparison.OrdinalIgnoreCase) ||
                   poseName.Equals(
                       "Optional Pose D",
                       StringComparison.OrdinalIgnoreCase);
        }

        private void ProcessVrmRuntime()
        {
            if (vrm10Instance != null && vrm10Instance.Runtime != null)
            {
                Transform lookTarget =
                    ResolveCompanionLookTarget();
                if (lookTarget != null)
                {
                    vrm10Instance.LookAtTargetType =
                        VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform;
                    vrm10Instance.LookAtTarget = lookTarget;
                }

                try
                {
                    vrm10Instance.Runtime.Process();
                }
                catch (NullReferenceException ex)
                    when (activeVrmaAnimation != null)
                {
                    if (!hasReportedVrmaRetargetFailure)
                    {
                        Debug.LogWarning(
                            "[VRMService] VRMA retargeting failed for this avatar. " +
                            "Falling back to its static VPD pose while preserving " +
                            "face, eye, lip-sync, and spring updates.\n" + ex);
                        hasReportedVrmaRetargetFailure = true;
                    }

                    vrm10Instance.Runtime.VrmAnimation = null;
                    activeVrmaAnimation = null;

                    if (activeVrmaInstance != null)
                    {
                        Destroy(activeVrmaInstance.gameObject);
                        activeVrmaInstance = null;
                    }

                    ApplyCurrentVpdFallback();
                    vrm10Instance.Runtime.Process();
                }

                ApplyFacialOverrides();
            }
        }

        private Transform ResolveCompanionLookTarget()
        {
            if (Camera.main == null)
            {
                return null;
            }

            Transform attentionTarget =
                ResolveAttentionTarget(
                    currentAvatarGo);
            if (attentionTarget != null)
            {
                return attentionTarget;
            }

            CompanionCamera companionCamera =
                Camera.main.GetComponent<CompanionCamera>();
            if (companionCamera == null ||
                companionCamera.ExploreMode)
            {
                return Camera.main.transform;
            }

            GameObject player =
                GameObject.Find("PlayerAvatarVRM");
            if (player == null)
            {
                return Camera.main.transform;
            }

            Animator playerAnimator =
                player.GetComponent<Animator>();
            if (playerAnimator != null &&
                playerAnimator.isHuman)
            {
                Transform head =
                    playerAnimator.GetBoneTransform(
                        HumanBodyBones.Head);
                if (head != null)
                {
                    return head;
                }
            }

            return player.transform;
        }

        private void UpdatePlayerLookTarget()
        {
            if (playerVrm10Instance == null ||
                playerVrm10Instance.Runtime == null ||
                Camera.main == null)
            {
                return;
            }

            Transform lookTarget =
                ResolveAttentionTarget(
                    playerAvatarGo) ??
                Camera.main.transform;
            CompanionCamera companionCamera =
                Camera.main.GetComponent<CompanionCamera>();
            if (!cameraAttentionRequested &&
                conversationFocusAvatar == null &&
                companionCamera != null &&
                !companionCamera.ExploreMode)
            {
                GameObject companion =
                    GameObject.Find("CompanionAvatarVRM");
                if (companion == null)
                {
                    companion =
                        GameObject.Find(
                            "CompanionAvatarProcedural");
                }

                if (companion != null)
                {
                    Animator animator =
                        companion.GetComponent<Animator>();
                    Transform head =
                        animator != null && animator.isHuman
                            ? animator.GetBoneTransform(
                                HumanBodyBones.Head)
                            : null;
                    lookTarget =
                        head != null ? head : companion.transform;
                }
            }

            playerVrm10Instance.LookAtTargetType =
                VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform;
            playerVrm10Instance.LookAtTarget = lookTarget;
            playerVrm10Instance.Runtime.LookAt.LookAtInput =
                new LookAtInput
                {
                    WorldPosition = lookTarget.position
                };
        }

        private void ApplyCurrentVpdFallback()
        {
            if (currentPoseIndex < 0 ||
                currentPoseIndex >= loadedOptionalPoses.Count ||
                poseHandler == null)
            {
                return;
            }

            CustomPose cp = loadedOptionalPoses[currentPoseIndex];
            if (cp.muscles == null || cp.muscles.Length == 0)
            {
                return;
            }

            HumanPose hp = new HumanPose
            {
                bodyPosition = cp.bodyPosition,
                bodyRotation = cp.bodyRotation,
                muscles = cp.muscles
            };

            poseHandler.SetHumanPose(ref hp);
            appliedVpdPoseIndex = currentPoseIndex;
        }

        private void ApplyFacialOverrides()
        {
            if (vrm10Instance == null ||
                vrm10Instance.Runtime == null ||
                vrm10Instance.Runtime.Expression == null)
            {
                return;
            }

            var runtime = vrm10Instance.Runtime;

            runtime.Expression.SetWeight(ExpressionKey.Happy, 0f);
            runtime.Expression.SetWeight(ExpressionKey.Sad, 0f);
            runtime.Expression.SetWeight(ExpressionKey.Angry, 0f);
            runtime.Expression.SetWeight(ExpressionKey.Relaxed, 0f);
            runtime.Expression.SetWeight(ExpressionKey.Surprised, 0f);
            runtime.Expression.SetWeight(ExpressionKey.Neutral, 0f);
            runtime.Expression.SetWeight(
                currentExpressionKey,
                currentExpressionWeight);
            runtime.Expression.SetWeight(
                ExpressionKey.Blink,
                currentBlinkWeight);
            runtime.Expression.SetWeight(
                ExpressionKey.Aa,
                currentLipSyncWeight);

            // Refresh only expression output after the VRMA body pass. UniVRM's
            // Process method is internal, so invoke that face-only method without
            // advancing body retargeting or spring physics a second time.
            System.Reflection.MethodInfo expressionProcess =
                typeof(Vrm10RuntimeExpression).GetMethod(
                    "Process",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            expressionProcess?.Invoke(
                runtime.Expression,
                new object[] { runtime.LookAt.EyeDirection });
        }

        private static void SanitizeSpringBones(Vrm10Instance instance)
        {
            if (instance == null || instance.SpringBone == null)
            {
                return;
            }

            var initialStates = instance.DefaultTransformStates;
            int removedJoints = 0;
            int removedSprings = 0;

            for (int springIndex = instance.SpringBone.Springs.Count - 1;
                 springIndex >= 0;
                 springIndex--)
            {
                Vrm10InstanceSpringBone.Spring spring =
                    instance.SpringBone.Springs[springIndex];

                if (spring == null)
                {
                    instance.SpringBone.Springs.RemoveAt(springIndex);
                    removedSprings++;
                    continue;
                }

                removedJoints += spring.Joints.RemoveAll(joint =>
                    joint == null ||
                    joint.transform == null ||
                    !initialStates.ContainsKey(joint.transform));

                if (spring.Joints.Count < 2)
                {
                    instance.SpringBone.Springs.RemoveAt(springIndex);
                    removedSprings++;
                }
            }

            if (removedJoints > 0 || removedSprings > 0)
            {
                Debug.Log(
                    $"[VRMService] Removed {removedJoints} invalid spring joints " +
                    $"and {removedSprings} unusable spring chains.");
            }

            bool rebuilt = instance.Runtime.SpringBone.ReconstructSpringBone();
            Debug.Log(
                rebuilt
                    ? "[VRMService] Rebuilt spring physics after avatar import."
                    : "[VRMService] Spring physics rebuild was already in progress.");
        }

        public void CreateProceduralCompanion()
        {
            isUsingCustomVrm = false;
            vrmProxy = null;
            hasUserPlacedAvatar = false;

            if (currentAvatarGo != null)
            {
                Destroy(currentAvatarGo);
            }

            // Root
            currentAvatarGo = new GameObject("CompanionAvatarProcedural");
            currentAvatarGo.transform.position = new Vector3(0, 1.2f, 2.5f);
            currentAvatarGo.transform.rotation = Quaternion.identity;
            startPosition = currentAvatarGo.transform.localPosition;

            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null) urpLitShader = Shader.Find("Universal Render Pipeline/Unlit");

            Material bodyMat = new Material(urpLitShader);
            bodyMat.SetColor("_BaseColor", new Color(0.2f, 0.25f, 0.35f));
            bodyMat.SetFloat("_Metallic", 0.8f);
            bodyMat.SetFloat("_Smoothness", 0.8f);

            Material faceMat = new Material(urpLitShader);
            faceMat.SetColor("_BaseColor", new Color(0.1f, 0.12f, 0.18f));

            Material glowMat = new Material(urpLitShader);
            glowMat.SetColor("_BaseColor", new Color(0f, 0.95f, 1f));
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_EmissionColor", new Color(0f, 1.8f, 2.0f));

            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            torso.name = "Torso";
            torso.transform.SetParent(currentAvatarGo.transform, false);
            torso.transform.localPosition = new Vector3(0, -0.6f, 0);
            torso.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
            torso.GetComponent<Renderer>().material = bodyMat;

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            neck.name = "Neck";
            neck.transform.SetParent(currentAvatarGo.transform, false);
            neck.transform.localPosition = new Vector3(0, -0.1f, 0);
            neck.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            neck.GetComponent<Renderer>().material = bodyMat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(currentAvatarGo.transform, false);
            head.transform.localPosition = new Vector3(0, 0.2f, 0);
            head.transform.localScale = new Vector3(0.8f, 0.7f, 0.8f);
            head.GetComponent<Renderer>().material = faceMat;
            headTransform = head.transform;

            GameObject leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftEye.name = "LeftEye";
            leftEye.transform.SetParent(head.transform, false);
            leftEye.transform.localPosition = new Vector3(-0.2f, 0.1f, 0.35f);
            leftEye.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
            leftEye.GetComponent<Renderer>().material = glowMat;
            leftEyeTransform = leftEye.transform;

            GameObject rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightEye.name = "RightEye";
            rightEye.transform.SetParent(head.transform, false);
            rightEye.transform.localPosition = new Vector3(0.2f, 0.1f, 0.35f);
            rightEye.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
            rightEye.GetComponent<Renderer>().material = glowMat;
            rightEyeTransform = rightEye.transform;

            GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouth.name = "MouthPlate";
            mouth.transform.SetParent(head.transform, false);
            mouth.transform.localPosition = new Vector3(0f, -0.18f, 0.38f);
            mouth.transform.localScale = new Vector3(0.22f, 0.04f, 0.05f);
            mouth.GetComponent<Renderer>().material = glowMat;
            mouthTransform = mouth.transform;

            GameObject leftAntenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftAntenna.name = "LeftAntenna";
            leftAntenna.transform.SetParent(head.transform, false);
            leftAntenna.transform.localPosition = new Vector3(-0.45f, 0.35f, 0f);
            leftAntenna.transform.localRotation = Quaternion.Euler(0, 0, 45);
            leftAntenna.transform.localScale = new Vector3(0.06f, 0.25f, 0.06f);
            leftAntenna.GetComponent<Renderer>().material = bodyMat;

            GameObject rightAntenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightAntenna.name = "RightAntenna";
            rightAntenna.transform.SetParent(head.transform, false);
            rightAntenna.transform.localPosition = new Vector3(0.45f, 0.35f, 0f);
            rightAntenna.transform.localRotation = Quaternion.Euler(0, 0, -45);
            rightAntenna.transform.localScale = new Vector3(0.06f, 0.25f, 0.06f);
            rightAntenna.GetComponent<Renderer>().material = bodyMat;

            // Keep primitive colliders so the procedural companion can be clicked and dragged.
        }

        public void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError)
        {
            if (!File.Exists(filePath))
            {
                string errMsg = $"VRM file not found at: {filePath}";
                Debug.LogError(errMsg);
                onError?.Invoke(errMsg);
                return;
            }

            Debug.Log($"VRM load starting for path: {filePath}");
            LoadVRMAsync(filePath, onLoaded, onError);
        }

        public void LoadPlayerVRM(
            string filePath,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            if (!File.Exists(filePath))
            {
                string message =
                    $"Player VRM file not found at: {filePath}";
                Debug.LogError(message);
                onError?.Invoke(message);
                return;
            }

            LoadPlayerVRMAsync(filePath, onLoaded, onError);
        }

        private async void LoadPlayerVRMAsync(
            string filePath,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            try
            {
                primaryPicnicArranged = false;
                playerStartupPoseIndex = -1;
                if (playerAvatarGo != null)
                {
                    Destroy(playerAvatarGo);
                }

                playerVrm10Instance = await Vrm10.LoadPathAsync(
                    filePath,
                    canLoadVrm0X: true,
                    controlRigGenerationOption:
                        ControlRigGenerationOption.None,
                    showMeshes: true,
                    awaitCaller: new RuntimeOnlyAwaitCaller());

                if (playerVrm10Instance == null)
                {
                    throw new Exception(
                        "UniVRM returned no player avatar.");
                }

                playerAvatarGo = playerVrm10Instance.gameObject;
                playerAvatarGo.name = "PlayerAvatarVRM";
                playerAvatarGo.transform.SetParent(transform, true);
                playerAvatarGo.transform.SetPositionAndRotation(
                    new Vector3(0f, 0.02f, -1.5f),
                    Quaternion.identity);
                playerAvatarGo.transform.localScale = Vector3.one;

                Animator animator =
                    playerAvatarGo.GetComponent<Animator>();
                if (animator == null || !animator.isHuman)
                {
                    throw new Exception(
                        "The player VRM needs a Humanoid Animator.");
                }

                SanitizeSpringBones(playerVrm10Instance);
                RuntimeGltfInstance runtimeGltf =
                    playerAvatarGo.GetComponent<RuntimeGltfInstance>();
                if (runtimeGltf != null)
                {
                    runtimeGltf.EnableUpdateWhenOffscreen();
                    runtimeGltf.ShowMeshes();
                }
                EnsureSelectableCharacter(
                    playerAvatarGo);
                RegisterSpeechBubble(
                    playerAvatarGo,
                    string.Empty);
                playerAvatarGo.SetActive(false);

                ServiceConfig config = ServiceConfig.Load();
                if (config.hasPlayerTransform)
                {
                    playerAvatarGo.transform.SetPositionAndRotation(
                        new Vector3(
                            config.playerPosX,
                            config.playerPosY,
                            config.playerPosZ),
                        new Quaternion(
                            config.playerRotX,
                            config.playerRotY,
                            config.playerRotZ,
                            config.playerRotW));
                }
                config.playerVrmPath = filePath;
                config.Save();

                playerStartupPoseIndex =
                    ChooseStartupSeatedPose(
                        companionStartupPoseIndex);
                TryArrangePrimaryPicnicCharacters();

                Debug.Log(
                    "[VRMService] Independent player VRM loaded.");
                onLoaded?.Invoke(playerAvatarGo);
            }
            catch (Exception ex)
            {
                if (playerAvatarGo != null)
                {
                    Destroy(playerAvatarGo);
                    playerAvatarGo = null;
                }

                string message =
                    $"Player VRM Load Error: {ex}";
                Debug.LogError(message);
                onError?.Invoke(message);
            }
        }

        public void LoadWorldVRM(
            string characterId,
            string filePath,
            Vector3 position,
            Quaternion rotation,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                onError?.Invoke(
                    "A world character needs a unique ID.");
                return;
            }

            if (!File.Exists(filePath))
            {
                onError?.Invoke(
                    $"World VRM file not found at: {filePath}");
                return;
            }

            LoadWorldVRMAsync(
                characterId.Trim(),
                filePath,
                position,
                rotation,
                onLoaded,
                onError);
        }

        private void SeatWorldAvatarOnBlanket(
            GameObject avatar,
            int seatIndex)
        {
            if (avatar == null)
            {
                return;
            }

            GameObject blanket =
                GameObject.Find("PicnicStage_Blanket");
            Transform blanketTransform =
                blanket != null
                    ? blanket.transform
                    : seatingAnchor;
            Vector3 center =
                blanketTransform != null
                    ? blanketTransform.position
                    : new Vector3(0f, 0.02f, 2f);
            Renderer blanketRenderer =
                blanket != null
                    ? blanket.GetComponent<Renderer>()
                    : null;
            float surfaceY =
                blanketRenderer != null
                    ? blanketRenderer.bounds.max.y
                    : center.y;

            float angle =
                (seatIndex * 137.5f + 35f) *
                Mathf.Deg2Rad;
            Vector3 seatOffset =
                new Vector3(
                    Mathf.Sin(angle),
                    0f,
                    Mathf.Cos(angle)) * 0.72f;
            Vector3 seatPosition =
                center + seatOffset;
            seatPosition.y = surfaceY;
            Vector3 faceCenter =
                center - seatPosition;
            faceCenter.y = 0f;
            avatar.transform.SetPositionAndRotation(
                seatPosition,
                faceCenter.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(
                        faceCenter.normalized,
                        Vector3.up)
                    : avatar.transform.rotation);

            int poseIndex =
                ChooseStartupSeatedPose(-1);
            if (poseIndex < 0)
            {
                return;
            }

            RuntimeHumanoidPosePlayer player =
                avatar.GetComponent<
                    RuntimeHumanoidPosePlayer>();
            if (player == null)
            {
                player =
                    avatar.AddComponent<
                        RuntimeHumanoidPosePlayer>();
            }

            CustomPose pose = loadedOptionalPoses[poseIndex];
            if (player.TryLoadFavoritePose(
                    pose.resourcePath))
            {
                player.SetPoseActive(
                    true,
                    surfaceY);
                worldPoseIndices[avatar] =
                    poseIndex;
            }
        }

        private int FindPoseIndex(string poseName)
        {
            for (int i = 0;
                 i < loadedOptionalPoses.Count;
                 i++)
            {
                if (loadedOptionalPoses[i].name.Equals(
                        poseName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private int ChooseStartupSeatedPose(
            int excludedIndex)
        {
            List<int> candidates =
                new List<int>();
            foreach (string poseName in
                StartupSeatedPoseNames)
            {
                int index =
                    FindPoseIndex(
                        poseName);
                if (index >= 0 &&
                    index != excludedIndex)
                {
                    candidates.Add(index);
                }
            }

            if (candidates.Count == 0)
            {
                return FindPoseIndex(
                    StartupSeatedPoseNames[0]);
            }
            return candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count)];
        }

        private void TryArrangePrimaryPicnicCharacters()
        {
            if (primaryPicnicArranged ||
                currentAvatarGo == null ||
                playerAvatarGo == null)
            {
                return;
            }

            GameObject blanket =
                GameObject.Find(
                    "PicnicStage_Blanket");
            Transform blanketTransform =
                blanket != null
                    ? blanket.transform
                    : seatingAnchor;
            Vector3 center =
                blanketTransform != null
                    ? blanketTransform.position
                    : new Vector3(
                        0f,
                        BlanketSurfaceY,
                        2f);
            Renderer blanketRenderer =
                blanket != null
                    ? blanket.GetComponent<Renderer>()
                    : null;
            float surfaceY =
                blanketRenderer != null
                    ? blanketRenderer.bounds.max.y
                    : center.y;

            Vector3 companionSeat =
                center +
                new Vector3(-0.58f, 0f, 0.04f);
            Vector3 playerSeat =
                center +
                new Vector3(0.58f, 0f, -0.04f);
            companionSeat.y = surfaceY;
            playerSeat.y = surfaceY;

            currentAvatarGo.transform
                .SetPositionAndRotation(
                    companionSeat,
                    Quaternion.LookRotation(
                        (playerSeat - companionSeat)
                            .normalized,
                        Vector3.up));
            playerAvatarGo.transform
                .SetPositionAndRotation(
                    playerSeat,
                    Quaternion.LookRotation(
                        (companionSeat - playerSeat)
                            .normalized,
                        Vector3.up));

            startPosition = companionSeat;
            hasUserPlacedAvatar = true;
            if (playerStartupPoseIndex < 0)
            {
                playerStartupPoseIndex =
                    ChooseStartupSeatedPose(
                        companionStartupPoseIndex);
            }
            if (playerStartupPoseIndex >= 0)
            {
                SetWorldCharacterPose(
                    playerAvatarGo,
                    playerStartupPoseIndex);
            }
            primaryPicnicArranged = true;
        }

        private static void EnsureAvatarCollider(
            GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            Animator animator =
                avatar.GetComponent<Animator>();
            if (animator != null &&
                animator.isHuman)
            {
                BoxCollider broadCollider =
                    avatar.GetComponent<BoxCollider>();
                if (broadCollider != null)
                {
                    broadCollider.enabled = false;
                }

                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.Head,
                    0.13f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.Chest,
                    0.18f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.Spine,
                    0.16f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.Hips,
                    0.18f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.LeftUpperArm,
                    0.10f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.RightUpperArm,
                    0.10f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.LeftLowerArm,
                    0.085f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.RightLowerArm,
                    0.085f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.LeftUpperLeg,
                    0.12f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.RightUpperLeg,
                    0.12f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.LeftLowerLeg,
                    0.10f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.RightLowerLeg,
                    0.10f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.LeftFoot,
                    0.11f);
                AddHumanoidClickSphere(
                    animator,
                    HumanBodyBones.RightFoot,
                    0.11f);
                return;
            }

            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in
                avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    renderer.transform.name.EndsWith(
                        "_SelectionOutline",
                        StringComparison.Ordinal) ||
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

            BoxCollider collider =
                avatar.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider =
                    avatar.AddComponent<BoxCollider>();
            }

            collider.center =
                avatar.transform.InverseTransformPoint(
                    bounds.center);
            Vector3 localSize =
                avatar.transform.InverseTransformVector(
                    bounds.size);
            collider.size = new Vector3(
                Mathf.Max(0.2f, Mathf.Abs(localSize.x)),
                Mathf.Max(0.4f, Mathf.Abs(localSize.y)),
                Mathf.Max(0.2f, Mathf.Abs(localSize.z)));
        }

        private static void AddHumanoidClickSphere(
            Animator animator,
            HumanBodyBones bone,
            float radius)
        {
            Transform boneTransform =
                animator.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                return;
            }

            string objectName =
                $"JarvisClick_{bone}";
            Transform existing =
                boneTransform.Find(objectName);
            SphereCollider sphere;
            if (existing != null)
            {
                sphere =
                    existing.GetComponent<
                        SphereCollider>();
            }
            else
            {
                GameObject clickTarget =
                    new GameObject(objectName);
                clickTarget.transform.SetParent(
                    boneTransform,
                    false);
                sphere =
                    clickTarget.AddComponent<
                        SphereCollider>();
            }

            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.radius = radius;
                sphere.center = Vector3.zero;
            }
        }

        private static void EnsureSelectableCharacter(
            GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            EnsureAvatarCollider(avatar);
            CharacterSelectionHighlight highlight =
                avatar.GetComponent<
                    CharacterSelectionHighlight>();
            if (highlight == null)
            {
                highlight =
                    avatar.AddComponent<
                        CharacterSelectionHighlight>();
            }
            highlight.SetSelected(false);
        }

        private async void LoadWorldVRMAsync(
            string characterId,
            string filePath,
            Vector3 position,
            Quaternion rotation,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            try
            {
                if (worldAvatars.TryGetValue(
                        characterId,
                        out GameObject previous) &&
                    previous != null)
                {
                    Destroy(previous);
                }

                Vrm10Instance instance =
                    await Vrm10.LoadPathAsync(
                        filePath,
                        canLoadVrm0X: true,
                        controlRigGenerationOption:
                            ControlRigGenerationOption.None,
                        showMeshes: true,
                        awaitCaller:
                            new RuntimeOnlyAwaitCaller());

                if (instance == null)
                {
                    throw new Exception(
                        "UniVRM returned no world avatar.");
                }

                GameObject avatar = instance.gameObject;
                avatar.name =
                    $"WorldAvatarVRM_{characterId}";
                avatar.transform.SetParent(transform, true);
                avatar.transform.SetPositionAndRotation(
                    position,
                    rotation);
                avatar.transform.localScale = Vector3.one;

                Animator animator =
                    avatar.GetComponent<Animator>();
                if (animator == null || !animator.isHuman)
                {
                    Destroy(avatar);
                    throw new Exception(
                        "The world VRM needs a Humanoid Animator.");
                }

                SanitizeSpringBones(instance);
                RuntimeGltfInstance runtimeGltf =
                    avatar.GetComponent<RuntimeGltfInstance>();
                if (runtimeGltf != null)
                {
                    runtimeGltf.EnableUpdateWhenOffscreen();
                    runtimeGltf.ShowMeshes();
                }

                EnsureSelectableCharacter(avatar);
                SeatWorldAvatarOnBlanket(
                    avatar,
                    worldAvatars.Count);
                RegisterSpeechBubble(
                    avatar,
                    string.Empty);
                worldAvatars[characterId] = avatar;
                Debug.Log(
                    $"[VRMService] World character " +
                    $"'{characterId}' loaded.");
                onLoaded?.Invoke(avatar);
            }
            catch (Exception ex)
            {
                string message =
                    $"World VRM Load Error ({characterId}): {ex}";
                Debug.LogError(message);
                onError?.Invoke(message);
            }
        }

        private async void LoadVRMAsync(
            string filePath,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            try
            {
                primaryPicnicArranged = false;
                companionStartupPoseIndex = -1;
                DestroyActiveVrma();

                if (poseHandler != null)
                {
                    poseHandler.Dispose();
                    poseHandler = null;
                }

                if (currentAvatarGo != null)
                {
                    Destroy(currentAvatarGo);
                }

                vrm10Instance = await Vrm10.LoadPathAsync(
                    filePath,
                    canLoadVrm0X: true,
                    controlRigGenerationOption: ControlRigGenerationOption.None,
                    showMeshes: true,
                    awaitCaller: new RuntimeOnlyAwaitCaller());

                if (vrm10Instance == null)
                {
                    throw new Exception(
                        "UniVRM Vrm10.LoadPathAsync returned null.");
                }

                currentAvatarGo = vrm10Instance.gameObject;
                currentAvatarGo.name = "CompanionAvatarVRM";
                DestroyProceduralFallbacks();
                hasUserPlacedAvatar = false;
                vrm10Instance.UpdateType = Vrm10Instance.UpdateTypes.None;
                SanitizeSpringBones(vrm10Instance);

                currentAvatarGo.transform.position = Vector3.zero;
                currentAvatarGo.transform.rotation = Quaternion.identity;
                currentAvatarGo.transform.localScale = Vector3.one;

                Animator animator =
                    currentAvatarGo.GetComponent<Animator>();

                if (animator == null || !animator.isHuman)
                {
                    throw new Exception(
                        "The loaded avatar does not expose a valid Humanoid Animator. " +
                        "VRMA retargeting requires a VRM 1.0 humanoid avatar.");
                }

                // Place her sitting on the picnic blanket (0.02f height is perfect)
                currentAvatarGo.transform.position = new Vector3(0f, 0.02f, 2f);
                startPosition = currentAvatarGo.transform.position;

                // Immediately align the avatar to face the camera upon import
                if (Camera.main != null)
                {
                    Vector3 lookPos = Camera.main.transform.position;
                    lookPos.y = currentAvatarGo.transform.position.y;
                    Vector3 direction = lookPos - currentAvatarGo.transform.position;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        currentAvatarGo.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }

                isUsingCustomVrm = true;

                vrmProxy =
                    currentAvatarGo.GetComponent<VRMBlendShapeProxy>();

                hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
                spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
                neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                leftShoulderBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                rightShoulderBone =
                    animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                leftUpperLegBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightUpperLegBone =
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftLowerLegBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightLowerLegBone =
                    animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                leftFootBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFootBone =
                    animator.GetBoneTransform(HumanBodyBones.RightFoot);
                leftUpperArmBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArmBone =
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftLowerArmBone =
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightLowerArmBone =
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

                if (hipsBone != null)
                {
                    hipsInitialRot = hipsBone.localRotation;
                    hipsInitialPos = hipsBone.localPosition;
                }

                if (spineBone != null) spineInitialRot = spineBone.localRotation;
                if (neckBone != null) neckInitialRot = neckBone.localRotation;
                if (headBone != null) headInitialRot = headBone.localRotation;
                if (leftShoulderBone != null)
                    leftShoulderInitialRot = leftShoulderBone.localRotation;
                if (rightShoulderBone != null)
                    rightShoulderInitialRot = rightShoulderBone.localRotation;
                if (leftUpperLegBone != null)
                    leftUpperLegInitialRot = leftUpperLegBone.localRotation;
                if (rightUpperLegBone != null)
                    rightUpperLegInitialRot = rightUpperLegBone.localRotation;
                if (leftLowerLegBone != null)
                    leftLowerLegInitialRot = leftLowerLegBone.localRotation;
                if (rightLowerLegBone != null)
                    rightLowerLegInitialRot = rightLowerLegBone.localRotation;
                if (leftFootBone != null)
                    leftFootInitialRot = leftFootBone.localRotation;
                if (rightFootBone != null)
                    rightFootInitialRot = rightFootBone.localRotation;
                if (leftUpperArmBone != null)
                    leftUpperArmInitialRot = leftUpperArmBone.localRotation;
                if (rightUpperArmBone != null)
                    rightUpperArmInitialRot = rightUpperArmBone.localRotation;
                if (leftLowerArmBone != null)
                    leftLowerArmInitialRot = leftLowerArmBone.localRotation;
                if (rightLowerArmBone != null)
                    rightLowerArmInitialRot = rightLowerArmBone.localRotation;

                bonesCached = true;
                poseHandler =
                    new HumanPoseHandler(animator.avatar, animator.transform);

                FindFallbackBlendshapes();

                avatarRenderers.Clear();
                avatarRenderers.AddRange(currentAvatarGo.GetComponentsInChildren<Renderer>());

                RuntimeGltfInstance runtimeGltf =
                    currentAvatarGo.GetComponent<RuntimeGltfInstance>();

                if (runtimeGltf != null)
                {
                    runtimeGltf.EnableUpdateWhenOffscreen();
                    runtimeGltf.ShowMeshes();
                }

                Bounds colliderBounds =
                    new Bounds(currentAvatarGo.transform.position, Vector3.zero);
                bool hasColliderBounds = false;

                foreach (Renderer renderer in
                    currentAvatarGo.GetComponentsInChildren<Renderer>())
                {
                    if (!hasColliderBounds)
                    {
                        colliderBounds = renderer.bounds;
                        hasColliderBounds = true;
                    }
                    else
                    {
                        colliderBounds.Encapsulate(renderer.bounds);
                    }
                }

                if (hasColliderBounds)
                {
                    EnsureSelectableCharacter(currentAvatarGo);
                }
                companionSpeechBubble =
                    RegisterSpeechBubble(
                        currentAvatarGo,
                        string.Empty);

                ServiceConfig config = ServiceConfig.Load();
                config.customVrmPath = filePath;
                config.Save();

                Debug.Log(
                    "[VRMService] VRM 1.0 character loaded successfully.");

                companionStartupPoseIndex =
                    ChooseStartupSeatedPose(
                        playerStartupPoseIndex);
                if (companionStartupPoseIndex >= 0)
                {
                    GameObject previousSelection =
                        selectedCharacterGo;
                    selectedCharacterGo = null;
                    SetPose(
                        companionStartupPoseIndex);
                    selectedCharacterGo =
                        previousSelection;
                }
                else
                {
                    currentPoseIndex = -1;
                }
                TryArrangePrimaryPicnicCharacters();
                onLoaded?.Invoke(currentAvatarGo);
            }
            catch (Exception ex)
            {
                string errorMsg = $"VRM 1.0 Load Error: {ex}";
                Debug.LogError(errorMsg);
                onError?.Invoke(errorMsg);
                CreateProceduralCompanion();
            }
        }

        public void SetExpression(string expressionName, float value)
        {
            if (isUsingCustomVrm)
            {
                string expLower = expressionName.ToLower();
                currentExpressionWeight = Mathf.Clamp01(value);

                if (expLower == "happy" || expLower == "joy")
                    currentExpressionKey = ExpressionKey.Happy;
                else if (expLower == "sad" || expLower == "sorrow")
                    currentExpressionKey = ExpressionKey.Sad;
                else if (expLower == "angry")
                    currentExpressionKey = ExpressionKey.Angry;
                else if (expLower == "relaxed" ||
                         expLower == "thinking")
                    currentExpressionKey = ExpressionKey.Relaxed;
                else if (expLower == "surprised")
                    currentExpressionKey = ExpressionKey.Surprised;
                else
                    currentExpressionKey = ExpressionKey.Neutral;

                if (vrm10Instance != null && vrm10Instance.Runtime != null && vrm10Instance.Runtime.Expression != null)
                {
                    // Reset standard emotions to avoid blended overrides
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Happy, 0f);
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Sad, 0f);
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Angry, 0f);
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Relaxed, 0f);
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Surprised, 0f);
                    vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Neutral, 0f);

                    if (expLower == "happy" || expLower == "joy")
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Happy, value);
                    }
                    else if (expLower == "sad" || expLower == "sorrow")
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Sad, value);
                    }
                    else if (expLower == "angry")
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Angry, value);
                    }
                    else if (expLower == "relaxed" ||
                             expLower == "thinking")
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Relaxed, value);
                    }
                    else if (expLower == "surprised")
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Surprised, value);
                    }
                    else
                    {
                        vrm10Instance.Runtime.Expression.SetWeight(ExpressionKey.Neutral, value);
                    }
                }
                else if (vrmProxy != null)
                {
                    // Reset standard emotion blendshapes to clear prior states
                    vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Joy), 0f);
                    vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Sorrow), 0f);
                    vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Angry), 0f);

                    if (expLower == "happy" || expLower == "joy")
                    {
                        vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Joy), value);
                    }
                    else if (expLower == "sad" || expLower == "sorrow")
                    {
                        vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Sorrow), value);
                    }
                    else if (expLower == "angry")
                    {
                        vrmProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Angry), value);
                    }
                }

                if (fallbackFaceRenderer != null)
                {
                    float weightVal = value * 100f;
                    if (fallbackJoyBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackJoyBlendshapeIdx, 0f);
                    if (fallbackSorrowBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackSorrowBlendshapeIdx, 0f);
                    if (fallbackAngryBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackAngryBlendshapeIdx, 0f);

                    if (expLower == "happy" || expLower == "joy")
                    {
                        if (fallbackJoyBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackJoyBlendshapeIdx, weightVal);
                    }
                    else if (expLower == "sad" || expLower == "sorrow")
                    {
                        if (fallbackSorrowBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackSorrowBlendshapeIdx, weightVal);
                    }
                    else if (expLower == "angry")
                    {
                        if (fallbackAngryBlendshapeIdx != -1) fallbackFaceRenderer.SetBlendShapeWeight(fallbackAngryBlendshapeIdx, weightVal);
                    }
                }
            }
            else if (mouthTransform != null)
            {
                if (expressionName.ToLower() == "happy" || expressionName.ToLower() == "joy")
                {
                    mouthTransform.localScale = new Vector3(0.35f, 0.05f, 0.05f);
                }
                else
                {
                    mouthTransform.localScale = new Vector3(0.22f, 0.04f, 0.05f);
                }
            }
        }

        private void DestroyProceduralFallbacks()
        {
            Transform[] sceneTransforms =
                GameObject.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include);
            foreach (Transform candidate in
                sceneTransforms)
            {
                if (candidate == null ||
                    candidate.gameObject ==
                        currentAvatarGo ||
                    candidate.parent != null ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.name.Equals(
                        "CompanionAvatarProcedural",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                Destroy(candidate.gameObject);
            }
        }

        public void SetSpeechBubblesVisible(
            bool visible)
        {
            speechBubblesVisible = visible;
            for (int i = speechBubbles.Count - 1;
                 i >= 0;
                 i--)
            {
                CharacterSpeechBubble bubble =
                    speechBubbles[i];
                if (bubble == null)
                {
                    speechBubbles.RemoveAt(i);
                    continue;
                }
                bubble.SetBubbleVisible(visible);
            }
        }

        public void SetSpeechBubbleMessage(
            string message)
        {
            if (companionSpeechBubble != null)
            {
                companionSpeechBubble.SetMessage(
                    message);
            }
        }

        public List<string> GetAvailableSatellites()
        {
            List<string> satellites =
                new List<string>
                {
                    "charmy jarvis"
                };
            foreach (string characterId in
                worldAvatars.Keys)
            {
                if (!satellites.Exists(
                        name =>
                            name.Equals(
                                characterId,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    satellites.Add(characterId);
                }
            }
            return satellites;
        }

        public bool CallSatellite(string satelliteId)
        {
            if (playerAvatarGo == null ||
                string.IsNullOrWhiteSpace(
                    satelliteId))
            {
                return false;
            }

            GameObject satellite = null;
            string normalized =
                satelliteId.Trim();
            if (normalized.Equals(
                    "charmy jarvis",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(
                    "charmy",
                    StringComparison.OrdinalIgnoreCase))
            {
                satellite = currentAvatarGo;
            }
            else
            {
                worldAvatars.TryGetValue(
                    normalized,
                    out satellite);
            }

            if (satellite == null)
            {
                return false;
            }

            RememberPlacement(satellite);
            satellite.SetActive(true);
            RuntimeHumanoidPosePlayer posePlayer =
                satellite.GetComponent<
                    RuntimeHumanoidPosePlayer>();
            posePlayer?.SetPoseActive(
                false,
                satellite.transform.position.y);

            if (satellite == currentAvatarGo)
            {
                DestroyActiveVrma();
                currentPoseIndex = -1;
                appliedVpdPoseIndex = -1;
            }

            PlayerLocomotionAnimator locomotion =
                satellite.GetComponent<
                    PlayerLocomotionAnimator>();
            if (locomotion == null)
            {
                locomotion =
                    satellite.AddComponent<
                        PlayerLocomotionAnimator>();
            }
            locomotion.SetPlaybackActive(true);

            if (!satelliteCallSlots.TryGetValue(
                    satellite,
                    out int callSlot))
            {
                callSlot =
                    satelliteCallSlots.Count;
                satelliteCallSlots[satellite] =
                    callSlot;
            }
            satelliteArrivals[satellite] =
                new SatelliteArrival
                {
                    avatar = satellite,
                    slot = callSlot,
                    locomotion = locomotion
                };

            if (satellite == currentAvatarGo)
            {
                hasUserPlacedAvatar = true;
                groundedPoseIndex = -1;
                seatedGroundingTimer = 0f;
            }

            CharacterSpeechBubble bubble =
                satellite.GetComponent<
                    CharacterSpeechBubble>();
            bubble?.SetMessage(string.Empty);
            SelectCharacter(satellite);
            return true;
        }

        private void UpdateSatelliteArrivals()
        {
            if (playerAvatarGo == null ||
                satelliteArrivals.Count == 0)
            {
                return;
            }

            List<GameObject> completed =
                null;
            foreach (KeyValuePair<GameObject, SatelliteArrival>
                pair in satelliteArrivals)
            {
                SatelliteArrival arrival =
                    pair.Value;
                if (arrival?.avatar == null)
                {
                    completed ??=
                        new List<GameObject>();
                    completed.Add(pair.Key);
                    continue;
                }

                Vector3 destination =
                    GetSatelliteJoinPosition(
                        arrival.slot);
                Vector3 delta =
                    destination -
                    arrival.avatar.transform.position;
                Vector3 flatDelta =
                    new Vector3(
                        delta.x,
                        0f,
                        delta.z);
                const float StopDistance = 0.12f;
                const float WalkSpeed = 1.75f;

                if (flatDelta.magnitude >
                    StopDistance)
                {
                    Vector3 direction =
                        flatDelta.normalized;
                    Vector3 next =
                        Vector3.MoveTowards(
                            arrival.avatar.transform.position,
                            destination,
                            WalkSpeed *
                            Time.deltaTime);
                    next.y =
                        Mathf.Max(
                            0.02f,
                            next.y);
                    arrival.avatar.transform.position =
                        next;
                    Quaternion facing =
                        Quaternion.LookRotation(
                            direction,
                            Vector3.up);
                    arrival.avatar.transform.rotation =
                        Quaternion.Slerp(
                            arrival.avatar.transform.rotation,
                            facing,
                            Time.deltaTime * 8f);
                    arrival.locomotion?.SetMotion(
                        WalkSpeed,
                        false,
                        false);
                    if (arrival.avatar ==
                        currentAvatarGo)
                    {
                        startPosition = next;
                    }
                    continue;
                }

                arrival.avatar.transform.position =
                    destination;
                Vector3 faceStella =
                    playerAvatarGo.transform.position -
                    destination;
                faceStella.y = 0f;
                if (faceStella.sqrMagnitude >
                    0.001f)
                {
                    arrival.avatar.transform.rotation =
                        Quaternion.LookRotation(
                            faceStella.normalized,
                            Vector3.up);
                }
                arrival.locomotion?.SetMotion(
                    0f,
                    false,
                    false);
                completed ??=
                    new List<GameObject>();
                completed.Add(
                    arrival.avatar);
            }

            if (completed == null)
            {
                return;
            }
            foreach (GameObject avatar in completed)
            {
                satelliteArrivals.Remove(
                    avatar);
            }
        }

        private Vector3 GetSatelliteJoinPosition(
            int slot)
        {
            float side =
                slot % 2 == 0
                    ? 1f
                    : -1f;
            float row =
                Mathf.Floor(
                    slot / 2f);
            Vector3 position =
                playerAvatarGo.transform.position +
                playerAvatarGo.transform.right *
                side *
                (1.05f + row * 0.55f) -
                playerAvatarGo.transform.forward *
                (0.25f + row * 0.2f);
            position.y =
                Mathf.Max(
                    0.02f,
                    playerAvatarGo.transform.position.y);
            return position;
        }

        private void StopSatelliteArrival(
            GameObject avatar)
        {
            if (avatar == null ||
                !satelliteArrivals.TryGetValue(
                    avatar,
                    out SatelliteArrival arrival))
            {
                return;
            }

            arrival.locomotion?.SetMotion(
                0f,
                false,
                false);
            satelliteArrivals.Remove(avatar);
        }

        private void RememberPlacement(
            GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            placementHistory.Push(
                new PlacementState
                {
                    avatar = avatar,
                    position =
                        avatar.transform.position,
                    rotation =
                        avatar.transform.rotation
                });
        }

        public void UndoLastPlacement()
        {
            while (placementHistory.Count > 0)
            {
                PlacementState state =
                    placementHistory.Pop();
                if (state?.avatar == null)
                {
                    continue;
                }

                StopSatelliteArrival(
                    state.avatar);
                state.avatar.transform
                    .SetPositionAndRotation(
                        state.position,
                        state.rotation);
                if (state.avatar ==
                    currentAvatarGo)
                {
                    hasUserPlacedAvatar = true;
                    startPosition =
                        state.position;
                    verticalVelocity = 0f;
                }
                SelectCharacter(
                    state.avatar);
                return;
            }
        }

        private static void SnapCharacterToFloor(
            GameObject avatar)
        {
            if (avatar == null)
            {
                return;
            }

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in
                avatar.GetComponentsInChildren<
                    Renderer>(false))
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.transform.name.EndsWith(
                        "_SelectionOutline",
                        StringComparison.Ordinal) ||
                    renderer.bounds.size.sqrMagnitude <
                    0.0025f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            if (!hasBounds)
            {
                return;
            }

            float surfaceY = 0.02f;
            Vector3 rayOrigin =
                bounds.center +
                Vector3.up *
                (bounds.extents.y + 2f);
            RaycastHit[] hits =
                Physics.RaycastAll(
                    rayOrigin,
                    Vector3.down,
                    bounds.size.y + 8f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == null ||
                    hit.transform.IsChildOf(
                        avatar.transform) ||
                    hit.point.y >
                        bounds.center.y + 0.25f)
                {
                    continue;
                }
                surfaceY =
                    Mathf.Max(
                        surfaceY,
                        hit.point.y);
            }

            Vector3 position =
                avatar.transform.position;
            position.y +=
                surfaceY -
                bounds.min.y +
                0.006f;
            avatar.transform.position =
                position;
        }

        public void FocusSelectedCharacterForConversation()
        {
            if (selectedCharacterGo == null)
            {
                return;
            }
            primaryConversationCharacter =
                GetCharacterId(
                    selectedCharacterGo);
            Camera.main
                ?.GetComponent<CompanionCamera>()
                ?.SetTrackedTarget(
                    selectedCharacterGo.transform);
        }

        public void WhistleForCamera()
        {
            conversationFocusAvatar = null;
            cameraAttentionRequested = true;
            ApplyCameraAttention(true);
        }

        public void SetCameraAttentionLocked(
            bool locked)
        {
            cameraAttentionLocked = locked;
            if (locked)
            {
                conversationFocusAvatar = null;
                cameraAttentionRequested = true;
                ApplyCameraAttention(true);
            }
        }

        private void ApplyCameraAttention(
            bool immediate = false)
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return;
            }

            if (conversationFocusAvatar != null &&
                conversationFocusAvatar
                    .activeInHierarchy)
            {
                Vector3 speakerPosition =
                    GetAvatarLookPosition(
                        conversationFocusAvatar);
                ApplyAttentionToObserver(
                    currentAvatarGo,
                    speakerPosition);
                ApplyAttentionToObserver(
                    playerAvatarGo,
                    speakerPosition);
                foreach (GameObject avatar in
                    worldAvatars.Values)
                {
                    ApplyAttentionToObserver(
                        avatar,
                        speakerPosition);
                }
                return;
            }

            if (!cameraAttentionRequested)
            {
                return;
            }

            ApplyCameraAttention(
                currentAvatarGo,
                activeCamera.transform.position,
                immediate);
            ApplyCameraAttention(
                playerAvatarGo,
                activeCamera.transform.position,
                immediate);
            foreach (GameObject avatar in
                worldAvatars.Values)
            {
                ApplyCameraAttention(
                    avatar,
                    activeCamera.transform.position,
                    immediate);
            }
        }

        private void ApplyAttentionToObserver(
            GameObject observer,
            Vector3 targetPosition)
        {
            if (observer == null ||
                observer ==
                    conversationFocusAvatar)
            {
                return;
            }
            ApplyCameraAttention(
                observer,
                targetPosition);
        }

        private static void ApplyCameraAttention(
            GameObject avatar,
            Vector3 cameraPosition,
            bool immediate = false)
        {
            if (avatar == null ||
                !avatar.activeInHierarchy)
            {
                return;
            }

            Vector3 bodyDirection =
                cameraPosition -
                avatar.transform.position;
            bodyDirection.y = 0f;
            if (bodyDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        bodyDirection.normalized,
                        Vector3.up);
                avatar.transform.rotation =
                    immediate
                        ? targetRotation
                        : Quaternion.RotateTowards(
                            avatar.transform.rotation,
                            targetRotation,
                            360f * Time.unscaledDeltaTime);
            }

            Vrm10Instance instance =
                avatar.GetComponentInChildren<
                    Vrm10Instance>(true);
            if (instance?.Runtime?.LookAt != null)
            {
                instance.Runtime.LookAt.LookAtInput =
                    new LookAtInput
                    {
                        WorldPosition = cameraPosition
                    };
            }
        }

        private Transform ResolveAttentionTarget(
            GameObject observer)
        {
            if (cameraAttentionRequested)
            {
                return Camera.main != null
                    ? Camera.main.transform
                    : null;
            }

            if (conversationFocusAvatar == null ||
                conversationFocusAvatar == observer ||
                !conversationFocusAvatar
                    .activeInHierarchy)
            {
                return null;
            }

            Animator animator =
                conversationFocusAvatar.GetComponent<
                    Animator>();
            Transform head =
                animator != null &&
                animator.isHuman
                    ? animator.GetBoneTransform(
                        HumanBodyBones.Head)
                    : null;
            return head != null
                ? head
                : conversationFocusAvatar.transform;
        }

        private static Vector3 GetAvatarLookPosition(
            GameObject avatar)
        {
            if (avatar == null)
            {
                return Vector3.zero;
            }

            Animator animator =
                avatar.GetComponent<Animator>();
            Transform head =
                animator != null &&
                animator.isHuman
                    ? animator.GetBoneTransform(
                        HumanBodyBones.Head)
                    : null;
            return head != null
                ? head.position
                : avatar.transform.position +
                  Vector3.up * 1.35f;
        }

        public string GetPrimaryConversationCharacter()
        {
            return primaryConversationCharacter;
        }

        public List<string>
            GetConversationCharactersInFrame()
        {
            List<string> visible =
                new List<string>();
            Camera activeCamera =
                Camera.main;
            Plane[] planes =
                activeCamera != null
                    ? GeometryUtility
                        .CalculateFrustumPlanes(
                            activeCamera)
                    : null;

            AddCharacterIfVisible(
                visible,
                currentAvatarGo,
                "charmy",
                planes);
            AddCharacterIfVisible(
                visible,
                playerAvatarGo,
                "jojo",
                planes);
            foreach (KeyValuePair<string, GameObject>
                pair in worldAvatars)
            {
                AddCharacterIfVisible(
                    visible,
                    pair.Value,
                    pair.Key,
                    planes);
            }
            return visible;
        }

        private static void AddCharacterIfVisible(
            List<string> visible,
            GameObject avatar,
            string characterId,
            Plane[] cameraPlanes)
        {
            if (avatar == null ||
                !avatar.activeInHierarchy ||
                string.IsNullOrWhiteSpace(
                    characterId))
            {
                return;
            }

            bool inFrame =
                cameraPlanes == null;
            foreach (Renderer renderer in
                avatar.GetComponentsInChildren<
                    Renderer>(false))
            {
                if (renderer != null &&
                    renderer.enabled &&
                    !renderer.transform.name.EndsWith(
                        "_SelectionOutline",
                        StringComparison.Ordinal) &&
                    (cameraPlanes == null ||
                     GeometryUtility.TestPlanesAABB(
                         cameraPlanes,
                         renderer.bounds)))
                {
                    inFrame = true;
                    break;
                }
            }
            if (inFrame &&
                !visible.Exists(
                    item =>
                        item.Equals(
                            characterId,
                            StringComparison.OrdinalIgnoreCase)))
            {
                visible.Add(characterId);
            }
        }

        public void SetCharacterSpeech(
            string characterId,
            string message,
            bool speaking)
        {
            GameObject avatar =
                ResolveCharacterById(
                    characterId);
            if (avatar == null)
            {
                return;
            }

            if (speaking)
            {
                if (activeSpeakingAvatar != null &&
                    activeSpeakingAvatar != avatar)
                {
                    ApplyMouthWeight(
                        activeSpeakingAvatar,
                        0f);
                }
                activeSpeakingAvatar = avatar;
                if (!cameraAttentionLocked)
                {
                    conversationFocusAvatar = avatar;
                    cameraAttentionRequested = false;
                }
            }
            else if (activeSpeakingAvatar == avatar)
            {
                ApplyMouthWeight(
                    activeSpeakingAvatar,
                    0f);
                activeSpeakingAvatar = null;
            }

            CharacterSpeechBubble bubble =
                avatar.GetComponent<
                    CharacterSpeechBubble>();
            if (bubble == null)
            {
                bubble =
                    RegisterSpeechBubble(
                        avatar,
                        string.Empty);
            }
            bubble.SetMessage(message);
            bubble.SetSpeaking(speaking);
        }

        public string GetWorldStateSummary()
        {
            StringBuilder summary =
                new StringBuilder(
                    "stage=picnic meadow; active area=picnic blanket");
            AppendCharacterState(
                summary,
                "charmy",
                currentAvatarGo,
                currentPoseIndex);
            int playerPoseIndex = -1;
            if (playerAvatarGo != null)
            {
                worldPoseIndices.TryGetValue(
                    playerAvatarGo,
                    out playerPoseIndex);
            }
            AppendCharacterState(
                summary,
                "jojo",
                playerAvatarGo,
                playerPoseIndex);
            foreach (KeyValuePair<string, GameObject>
                pair in worldAvatars)
            {
                AppendCharacterState(
                    summary,
                    pair.Key,
                    pair.Value,
                    worldPoseIndices.TryGetValue(
                        pair.Value,
                        out int poseIndex)
                        ? poseIndex
                        : -1);
            }
            return summary.ToString();
        }

        private void AppendCharacterState(
            StringBuilder summary,
            string characterId,
            GameObject avatar,
            int poseIndex)
        {
            if (summary == null ||
                avatar == null ||
                !avatar.activeInHierarchy)
            {
                return;
            }

            Vector3 position =
                avatar.transform.position;
            string pose =
                poseIndex >= 0 &&
                poseIndex < loadedOptionalPoses.Count
                    ? loadedOptionalPoses[poseIndex].name
                    : "unposed";
            summary.Append(
                $"\n{characterId}: position=({position.x:F1}," +
                $"{position.y:F1},{position.z:F1}); pose={pose}");
        }

        public bool SetCharacterPoseByName(
            string characterId,
            string poseName)
        {
            GameObject avatar =
                ResolveCharacterById(
                    characterId);
            int poseIndex =
                FindPoseIndex(
                    poseName);
            if (avatar == null ||
                poseIndex < 0)
            {
                return false;
            }

            if (avatar == currentAvatarGo)
            {
                GameObject previousSelection =
                    selectedCharacterGo;
                selectedCharacterGo = null;
                SetPose(poseIndex);
                selectedCharacterGo =
                    previousSelection;
                return true;
            }

            SetWorldCharacterPose(
                avatar,
                poseIndex);
            return true;
        }

        private GameObject ResolveCharacterById(
            string characterId)
        {
            if (string.IsNullOrWhiteSpace(
                    characterId))
            {
                return currentAvatarGo;
            }
            if (characterId.Equals(
                    "charmy",
                    StringComparison.OrdinalIgnoreCase) ||
                characterId.Equals(
                    "charmy jarvis",
                    StringComparison.OrdinalIgnoreCase))
            {
                return currentAvatarGo;
            }
            if (characterId.Equals(
                    "jojo",
                    StringComparison.OrdinalIgnoreCase) ||
                characterId.Equals(
                    "jojo jarvis",
                    StringComparison.OrdinalIgnoreCase))
            {
                return playerAvatarGo;
            }
            worldAvatars.TryGetValue(
                characterId,
                out GameObject avatar);
            if (avatar == null)
            {
                string normalized =
                    characterId.Trim()
                        .ToLowerInvariant()
                        .Replace(
                            "_jarvis",
                            string.Empty)
                        .Replace(
                            " jarvis",
                            string.Empty);
                foreach (KeyValuePair<string, GameObject>
                    pair in worldAvatars)
                {
                    string candidate =
                        pair.Key.ToLowerInvariant()
                            .Replace(
                                "_jarvis",
                                string.Empty)
                            .Replace(
                                " jarvis",
                                string.Empty);
                    if (candidate == normalized)
                    {
                        avatar = pair.Value;
                        break;
                    }
                }
            }
            return avatar;
        }

        private string GetCharacterId(
            GameObject avatar)
        {
            if (avatar == currentAvatarGo)
            {
                return "charmy";
            }
            if (avatar == playerAvatarGo)
            {
                return "jojo";
            }
            foreach (KeyValuePair<string, GameObject>
                pair in worldAvatars)
            {
                if (pair.Value == avatar)
                {
                    return pair.Key;
                }
            }
            return avatar != null
                ? avatar.name
                : null;
        }

        private CharacterSpeechBubble RegisterSpeechBubble(
            GameObject avatar,
            string message)
        {
            if (avatar == null)
            {
                return null;
            }

            CharacterSpeechBubble bubble =
                avatar.GetComponent<
                    CharacterSpeechBubble>();
            if (bubble == null)
            {
                bubble =
                    avatar.AddComponent<
                        CharacterSpeechBubble>();
            }
            bubble.SetMessage(message);
            bubble.SetSpeaking(false);
            bubble.SetBubbleVisible(
                speechBubblesVisible);
            if (!speechBubbles.Contains(bubble))
            {
                speechBubbles.Add(bubble);
            }
            return bubble;
        }

        public void PlayMouthVolume(float volume)
        {
            float weight =
                Mathf.Clamp01(volume * 2.0f);
            if (activeSpeakingAvatar != null)
            {
                ApplyMouthWeight(
                    activeSpeakingAvatar,
                    weight);
                return;
            }

            if (!isUsingCustomVrm &&
                mouthTransform != null)
            {
                float scaleY = Mathf.Lerp(0.04f, 0.35f, volume * 3.5f);
                float scaleX = Mathf.Lerp(0.22f, 0.32f, volume * 1.5f);
                mouthTransform.localScale = new Vector3(scaleX, scaleY, 0.05f);
            }
        }

        private void ApplyMouthWeight(
            GameObject avatar,
            float weight)
        {
            if (avatar == null)
            {
                return;
            }

            float clamped =
                Mathf.Clamp01(weight);
            Vrm10Instance instance =
                avatar.GetComponent<Vrm10Instance>();
            if (instance?.Runtime?.Expression != null)
            {
                instance.Runtime.Expression.SetWeight(
                    ExpressionKey.Aa,
                    clamped);
            }

            if (avatar != currentAvatarGo)
            {
                return;
            }

            currentLipSyncWeight = clamped;
            if (instance?.Runtime?.Expression == null &&
                vrmProxy != null)
            {
                vrmProxy.ImmediatelySetValue(
                    BlendShapeKey.CreateFromPreset(
                        BlendShapePreset.A),
                    clamped);
            }
            if (fallbackFaceRenderer != null &&
                fallbackMouthBlendshapeIdx != -1)
            {
                fallbackFaceRenderer
                    .SetBlendShapeWeight(
                        fallbackMouthBlendshapeIdx,
                        clamped * 100f);
            }
        }

        public void SetSpeaking(bool speaking)
        {
            isSpeaking = speaking;
            if (!speaking)
            {
                ApplyMouthWeight(
                    activeSpeakingAvatar,
                    0f);
                SetExpression("neutral", 1f);
            }
        }

        public void SetAnimationEnabled(bool enabled)
        {
            isAnimationEnabled = enabled;
            if (!enabled && isUsingCustomVrm && bonesCached)
            {
                // Reset to initial local rotations and positions when disabled
                if (hipsBone != null)
                {
                    hipsBone.localRotation = hipsInitialRot;
                    hipsBone.localPosition = hipsInitialPos;
                }
                if (spineBone != null) spineBone.localRotation = spineInitialRot;
                if (neckBone != null) neckBone.localRotation = neckInitialRot;
                if (headBone != null) headBone.localRotation = headInitialRot;
                if (leftShoulderBone != null) leftShoulderBone.localRotation = leftShoulderInitialRot;
                if (rightShoulderBone != null) rightShoulderBone.localRotation = rightShoulderInitialRot;
                if (leftUpperLegBone != null) leftUpperLegBone.localRotation = leftUpperLegInitialRot;
                if (rightUpperLegBone != null) rightUpperLegBone.localRotation = rightUpperLegInitialRot;
                if (leftLowerLegBone != null) leftLowerLegBone.localRotation = leftLowerLegInitialRot;
                if (rightLowerLegBone != null) rightLowerLegBone.localRotation = rightLowerLegInitialRot;
                if (leftFootBone != null) leftFootBone.localRotation = leftFootInitialRot;
                if (rightFootBone != null) rightFootBone.localRotation = rightFootInitialRot;
                if (leftUpperArmBone != null) leftUpperArmBone.localRotation = leftUpperArmInitialRot;
                if (rightUpperArmBone != null) rightUpperArmBone.localRotation = rightUpperArmInitialRot;
                if (leftLowerArmBone != null) leftLowerArmBone.localRotation = leftLowerArmInitialRot;
                if (rightLowerArmBone != null) rightLowerArmBone.localRotation = rightLowerArmInitialRot;
            }
        }

        public void CyclePose()
        {
            if (TotalPoses == 0)
            {
                Debug.LogWarning(
                    "[VRMService] Cannot cycle poses: no pose folders loaded.");
                return;
            }

            if (selectedCharacterGo != null &&
                selectedCharacterGo != currentAvatarGo)
            {
                CycleWorldCharacterPose(
                    selectedCharacterGo);
                return;
            }

            int nextIndex =
                currentPoseIndex < 0
                    ? 0
                    : (currentPoseIndex + 1) % TotalPoses;

            SetPose(nextIndex);
        }

        private void CycleWorldCharacterPose(GameObject avatar)
        {
            if (avatar == null || TotalPoses == 0)
            {
                return;
            }

            int currentIndex =
                worldPoseIndices.TryGetValue(
                    avatar,
                    out int storedIndex)
                    ? storedIndex
                    : -1;

            for (int offset = 1;
                 offset <= TotalPoses;
                 offset++)
            {
                int candidate =
                    (currentIndex + offset) %
                    TotalPoses;
                CustomPose pose =
                    loadedOptionalPoses[candidate];
                if (string.IsNullOrWhiteSpace(
                        pose.resourcePath))
                {
                    continue;
                }

                SetWorldCharacterPose(
                    avatar,
                    candidate);
                return;
            }
        }

        private void SetWorldCharacterPose(
            GameObject avatar,
            int requestedIndex)
        {
            if (avatar == null || TotalPoses == 0)
            {
                return;
            }

            StopSatelliteArrival(avatar);
            avatar.GetComponent<
                    PlayerLocomotionAnimator>()
                ?.SetPlaybackActive(false);

            int startIndex =
                Mathf.Clamp(
                    requestedIndex,
                    0,
                    TotalPoses - 1);
            for (int offset = 0;
                 offset < TotalPoses;
                 offset++)
            {
                int candidate =
                    (startIndex + offset) %
                    TotalPoses;
                CustomPose pose =
                    loadedOptionalPoses[candidate];
                if (string.IsNullOrWhiteSpace(
                        pose.resourcePath))
                {
                    continue;
                }

                RuntimeHumanoidPosePlayer player =
                    avatar.GetComponent<
                        RuntimeHumanoidPosePlayer>();
                if (player == null)
                {
                    player =
                        avatar.AddComponent<
                            RuntimeHumanoidPosePlayer>();
                }

                if (player.TryLoadFavoritePose(
                        pose.resourcePath))
                {
                    float poseSurfaceY =
                        avatar == playerAvatarGo
                            ? avatar.transform.position.y
                            : GetSeatingSurfaceY();
                    player.SetPoseActive(
                        true,
                        poseSurfaceY);
                    worldPoseIndices[avatar] =
                        candidate;
                }
                return;
            }
        }

        private void OnDestroy()
        {
            DestroyActiveVrma();

            if (playerAvatarGo != null)
            {
                Destroy(playerAvatarGo);
                playerAvatarGo = null;
            }

            foreach (GameObject worldAvatar in
                worldAvatars.Values)
            {
                if (worldAvatar != null)
                {
                    Destroy(worldAvatar);
                }
            }
            worldAvatars.Clear();

            if (poseHandler != null)
            {
                poseHandler.Dispose();
                poseHandler = null;
            }

            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            DestroyActiveLightProps();
            DestroyActiveNormalProps();
        }

        private Bounds GetAvatarBounds()
        {
            if (currentAvatarGo == null) return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = new Bounds(currentAvatarGo.transform.position, Vector3.zero);
            bool hasBounds = false;

            if (avatarRenderers != null && avatarRenderers.Count > 0)
            {
                foreach (var r in avatarRenderers)
                {
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.magnitude > 0.05f)
                    {
                        if (!hasBounds)
                        {
                            bounds = r.bounds;
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(r.bounds);
                        }
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(currentAvatarGo.transform.position, new Vector3(1f, 1.8f, 1f));
            }

            return bounds;
        }

        private void CreateVisualStudioStage()
        {
            // 1. Floor (Grass Field)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PicnicStage_Grass";
            floor.transform.position = new Vector3(0f, 0f, 2f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f); // Make it a huge grass field!

            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null) urpLitShader = Shader.Find("Universal Render Pipeline/Unlit");

            Material grassMat = new Material(urpLitShader);
            grassMat.SetColor("_BaseColor", new Color(0.18f, 0.48f, 0.20f)); // Rich lush grass green
            grassMat.SetFloat("_Metallic", 0.05f);
            grassMat.SetFloat("_Smoothness", 0.1f);
            floor.GetComponent<Renderer>().material = grassMat;

            // Spawn procedural 3D grass/flowers!
            System.Random rand = new System.Random(42);
            for (int i = 0; i < 60; i++)
            {
                GameObject plant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plant.name = "GrassTuft_" + i;
                Destroy(plant.GetComponent<Collider>()); // Avoid blocking clicks

                // Position randomly around the picnic blanket (blanket is at (0, 0, 2))
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float radius = 1.0f + (float)(rand.NextDouble() * 4.0f);
                float x = Mathf.Sin(angle) * radius;
                float z = 2.0f + Mathf.Cos(angle) * radius;

                plant.transform.position = new Vector3(x, 0.15f, z);
                plant.transform.localScale = new Vector3(0.04f + (float)rand.NextDouble() * 0.04f, 0.1f + (float)rand.NextDouble() * 0.2f, 0.04f + (float)rand.NextDouble() * 0.04f);

                Material plantMat = new Material(urpLitShader);
                // Vary color slightly between lime green and deep forest green
                float hueOffset = (float)rand.NextDouble() * 0.15f;
                plantMat.SetColor("_BaseColor", new Color(0.15f + hueOffset, 0.45f + hueOffset * 0.5f, 0.12f));
                plantMat.SetFloat("_Smoothness", 0.1f);
                plant.GetComponent<Renderer>().material = plantMat;
            }

            // 2. Picnic Blanket
            GameObject blanket = GameObject.CreatePrimitive(PrimitiveType.Plane);
            blanket.name = "PicnicStage_Blanket";
            blanket.transform.position = new Vector3(0f, 0.01f, 2f); // Slightly above grass to avoid z-fighting
            blanket.transform.localScale = new Vector3(0.25f, 1f, 0.25f); // 2.5m x 2.5m blanket
            if (seatingAnchor == null)
            {
                seatingAnchor = blanket.transform;
            }

            // Red-and-White Checkered Pattern Texture
            Texture2D checkeredTex = new Texture2D(2, 2);
            checkeredTex.wrapMode = TextureWrapMode.Repeat;
            checkeredTex.filterMode = FilterMode.Point;
            checkeredTex.SetPixels(new Color[] {
                Color.white, new Color(0.85f, 0.15f, 0.15f), // White, Red
                new Color(0.85f, 0.15f, 0.15f), Color.white  // Red, White
            });
            checkeredTex.Apply();

            Material blanketMat = new Material(urpLitShader);
            blanketMat.SetTexture("_BaseMap", checkeredTex);
            // Tile the texture to look like a checkered blanket
            blanketMat.SetTextureScale("_BaseMap", new Vector2(8f, 8f));
            blanketMat.SetFloat("_Metallic", 0.0f);
            blanketMat.SetFloat("_Smoothness", 0.05f);
            blanket.GetComponent<Renderer>().material = blanketMat;

            // 3. Sunny Warm Directional Light
            // Disable existing directional lights to avoid double-lighting/overexposure
            foreach (var existingLight in GameObject.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (existingLight.type == LightType.Directional)
                {
                    existingLight.gameObject.SetActive(false);
                }
            }

            GameObject keyLightGo = new GameObject("PicnicStage_SunLight");
            Light keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.3f;
            keyLight.color = new Color(1.0f, 0.96f, 0.88f); // Warm sun golden hour / morning
            keyLight.shadows = LightShadows.Soft; // Beautiful soft shadows!
            keyLightGo.transform.rotation = Quaternion.Euler(40f, -40f, 0f);

            // Ambient sky blue light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.75f, 0.95f); // Beautiful sky blue fill
        }

        private Transform FindBoneInHierarchy(Transform current, string[] possibleNames)
        {
            foreach (string name in possibleNames)
            {
                if (current.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }
            }

            foreach (Transform child in current)
            {
                Transform found = FindBoneInHierarchy(child, possibleNames);
                if (found != null) return found;
            }

            return null;
        }

        private void CacheBonesGeneric()
        {
            if (currentAvatarGo == null) return;

            // Search by name recursively if animator is not humanoid
            hipsBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "hips", "pelvis", "b_hips" });
            spineBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "spine", "chest", "b_spine" });
            neckBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "neck", "b_neck" });
            headBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "head", "b_head" });
            leftShoulderBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftshoulder", "l_shoulder", "shoulder_l" });
            rightShoulderBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightshoulder", "r_shoulder", "shoulder_r" });
            leftUpperLegBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftupperleg", "l_upperleg", "l_hip", "thigh_l" });
            rightUpperLegBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightupperleg", "r_upperleg", "r_hip", "thigh_r" });
            leftLowerLegBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftlowerleg", "l_lowerleg", "l_knee", "calf_l" });
            rightLowerLegBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightlowerleg", "r_lowerleg", "r_knee", "calf_r" });
            leftFootBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftfoot", "l_foot", "foot_l" });
            rightFootBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightfoot", "r_foot", "foot_r" });
            leftUpperArmBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftupperarm", "l_upperarm", "arm_l", "shoulder_l" });
            rightUpperArmBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightupperarm", "r_upperarm", "arm_r", "shoulder_r" });
            leftLowerArmBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "leftlowerarm", "l_lowerarm", "elbow_l", "forearm_l" });
            rightLowerArmBone = FindBoneInHierarchy(currentAvatarGo.transform, new string[] { "rightlowerarm", "r_lowerarm", "elbow_r", "forearm_r" });

            if (hipsBone != null) hipsInitialRot = hipsBone.localRotation;
            if (spineBone != null) spineInitialRot = spineBone.localRotation;
            if (neckBone != null) neckInitialRot = neckBone.localRotation;
            if (headBone != null) headInitialRot = headBone.localRotation;
            if (leftShoulderBone != null) leftShoulderInitialRot = leftShoulderBone.localRotation;
            if (rightShoulderBone != null) rightShoulderInitialRot = rightShoulderBone.localRotation;
            if (leftUpperLegBone != null) leftUpperLegInitialRot = leftUpperLegBone.localRotation;
            if (rightUpperLegBone != null) rightUpperLegInitialRot = rightUpperLegBone.localRotation;
            if (leftLowerLegBone != null) leftLowerLegInitialRot = leftLowerLegBone.localRotation;
            if (rightLowerLegBone != null) rightLowerLegInitialRot = rightLowerLegBone.localRotation;
            if (leftFootBone != null) leftFootInitialRot = leftFootBone.localRotation;
            if (rightFootBone != null) rightFootInitialRot = rightFootBone.localRotation;
            if (leftUpperArmBone != null) leftUpperArmInitialRot = leftUpperArmBone.localRotation;
            if (rightUpperArmBone != null) rightUpperArmInitialRot = rightUpperArmBone.localRotation;
            if (leftLowerArmBone != null) leftLowerArmInitialRot = leftLowerArmBone.localRotation;
            if (rightLowerArmBone != null) rightLowerArmInitialRot = rightLowerArmBone.localRotation;

            if (hipsBone != null) hipsInitialPos = hipsBone.localPosition;

            // Mark as cached if at least hips and upper legs are found
            bonesCached = (hipsBone != null && leftUpperLegBone != null && rightUpperLegBone != null);
            Debug.Log($"[VRMService] Generic bone caching result: {bonesCached} (Hips: {hipsBone != null}, LeftUpperLeg: {leftUpperLegBone != null})");
        }

        private void FindFallbackBlendshapes()
        {
            fallbackFaceRenderer = null;
            fallbackMouthBlendshapeIdx = -1;
            fallbackBlinkBlendshapeIdx = -1;
            fallbackJoyBlendshapeIdx = -1;
            fallbackSorrowBlendshapeIdx = -1;
            fallbackAngryBlendshapeIdx = -1;

            if (currentAvatarGo == null) return;

            // Search child renderers for face mesh
            foreach (var smr in currentAvatarGo.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string lowerName = smr.name.ToLower();
                if (lowerName.Contains("face") || lowerName.Contains("head") || lowerName.Contains("skin") || lowerName.Contains("body") || lowerName.Contains("avatar"))
                {
                    fallbackFaceRenderer = smr;
                    break;
                }
            }

            if (fallbackFaceRenderer == null)
            {
                fallbackFaceRenderer = currentAvatarGo.GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (fallbackFaceRenderer != null && fallbackFaceRenderer.sharedMesh != null)
            {
                Debug.Log($"[VRMService] Fallback Blendshape mapping on: {fallbackFaceRenderer.name}");
                for (int i = 0; i < fallbackFaceRenderer.sharedMesh.blendShapeCount; i++)
                {
                    string name = fallbackFaceRenderer.sharedMesh.GetBlendShapeName(i).ToLower();

                    if (fallbackMouthBlendshapeIdx == -1 && (name == "a" || name == "aa" || name == "vowel_a" || name.Contains("preset_a") || name.Contains("mouth_open") || name.Contains("jaw_open") || name.Contains("mouth_a") || name.Contains("ch_a")))
                    {
                        fallbackMouthBlendshapeIdx = i;
                        Debug.Log($"  Mapped Fallback Mouth A Blendshape: index {i} ('{name}')");
                    }

                    if (fallbackBlinkBlendshapeIdx == -1 && (name.Contains("blink") || name.Contains("eye_close") || name.Contains("close_eye") || name == "blink_l" || name == "blink_r"))
                    {
                        fallbackBlinkBlendshapeIdx = i;
                        Debug.Log($"  Mapped Fallback Eye Blink Blendshape: index {i} ('{name}')");
                    }

                    if (fallbackJoyBlendshapeIdx == -1 && (name.Contains("joy") || name.Contains("happy") || name.Contains("smile") || name.Contains("fun")))
                    {
                        fallbackJoyBlendshapeIdx = i;
                        Debug.Log($"  Mapped Fallback Happy Blendshape: index {i} ('{name}')");
                    }

                    if (fallbackSorrowBlendshapeIdx == -1 && (name.Contains("sorrow") || name.Contains("sad") || name.Contains("cry")))
                    {
                        fallbackSorrowBlendshapeIdx = i;
                        Debug.Log($"  Mapped Fallback Sad Blendshape: index {i} ('{name}')");
                    }

                    if (fallbackAngryBlendshapeIdx == -1 && (name.Contains("angry") || name.Contains("mad") || name.Contains("rage")))
                    {
                        fallbackAngryBlendshapeIdx = i;
                        Debug.Log($"  Mapped Fallback Angry Blendshape: index {i} ('{name}')");
                    }
                }
            }
        }

        private void ApplyExpressiveLookAndMotion()
        {
            if (!isUsingCustomVrm || !isAnimationEnabled || Camera.main == null)
            {
                return;
            }

            if (spineBone != null)
            {
                float sway = Mathf.Sin(idleTime * (isSpeaking ? 7.5f : 2.5f)) * (isSpeaking ? 1.8f : 0.55f);
                spineBone.localRotation = Quaternion.Slerp(
                    spineBone.localRotation,
                    spineInitialRot * Quaternion.Euler(0f, sway, 0f),
                    Time.deltaTime * 6f
                );
            }

            if (leftShoulderBone != null && rightShoulderBone != null)
            {
                float shoulder = Mathf.Sin(idleTime * (isSpeaking ? 8f : 3f)) * (isSpeaking ? 2.5f : 0.75f);
                leftShoulderBone.localRotation = Quaternion.Slerp(
                    leftShoulderBone.localRotation,
                    leftShoulderInitialRot * Quaternion.Euler(shoulder, 0f, 0f),
                    Time.deltaTime * 6f
                );
                rightShoulderBone.localRotation = Quaternion.Slerp(
                    rightShoulderBone.localRotation,
                    rightShoulderInitialRot * Quaternion.Euler(-shoulder, 0f, 0f),
                    Time.deltaTime * 6f
                );
            }
        }
    }
}
