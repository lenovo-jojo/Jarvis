using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AICompanion.Services
{
    /// <summary>
    /// Builds a lightweight, mobile-friendly rolling-hill backdrop at runtime.
    /// The environment is intentionally isolated from avatar, camera, pose, and UI
    /// systems so it can be removed without affecting companion behavior.
    /// </summary>
    public sealed class FrutigerEnvironment : MonoBehaviour
    {
        private struct CloudMotion
        {
            public Transform transform;
            public Vector3 startPosition;
            public float distance;
            public float speed;
            public float phase;
        }

        private readonly List<Material> runtimeMaterials =
            new List<Material>();
        private readonly List<CloudMotion> clouds =
            new List<CloudMotion>();
        private bool hasBuiltEnvironment;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForScene()
        {
            Scene activeScene =
                SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                activeScene.name.Equals(
                    "PossumSprings",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Object.FindAnyObjectByType<FrutigerEnvironment>() != null)
            {
                return;
            }

            GameObject environmentObject =
                new GameObject("FrutigerAeroEnvironment");
            environmentObject.AddComponent<FrutigerEnvironment>();
        }

        private IEnumerator Start()
        {
            Camera sceneCamera = null;
            GameObject avatar = null;

            // The avatar may be loaded asynchronously from a VRM file.
            for (int frame = 0; frame < 600; frame++)
            {
                sceneCamera = Camera.main;
                avatar = FindAvatar();
                if (sceneCamera != null && avatar != null)
                {
                    break;
                }
                yield return null;
            }

            if (sceneCamera == null)
            {
                Debug.LogWarning(
                    "[FrutigerEnvironment] No main camera was found.");
                yield break;
            }

            Vector3 focusPosition = avatar != null
                ? avatar.transform.position
                : new Vector3(0f, 0f, 2f);
            BuildEnvironment(sceneCamera, focusPosition);
        }

        private void Update()
        {
            if (!hasBuiltEnvironment)
            {
                return;
            }

            float time = Time.unscaledTime;
            for (int i = 0; i < clouds.Count; i++)
            {
                CloudMotion cloud = clouds[i];
                if (cloud.transform == null)
                {
                    continue;
                }

                float drift =
                    Mathf.Sin((time * cloud.speed) + cloud.phase) *
                    cloud.distance;
                cloud.transform.localPosition =
                    cloud.startPosition + (Vector3.right * drift);
            }
        }

        private void BuildEnvironment(
            Camera sceneCamera,
            Vector3 focusPosition)
        {
            Vector3 viewDirection =
                focusPosition - sceneCamera.transform.position;
            viewDirection.y = 0f;
            if (viewDirection.sqrMagnitude < 0.001f)
            {
                viewDirection = Vector3.forward;
            }
            viewDirection.Normalize();

            transform.SetPositionAndRotation(
                new Vector3(
                    focusPosition.x,
                    0f,
                    focusPosition.z),
                Quaternion.LookRotation(viewDirection, Vector3.up));

            Material nearGrass = CreateMaterial(
                "FrutigerNearGrass",
                new Color32(79, 184, 86, 255),
                false);
            Material middleGrass = CreateMaterial(
                "FrutigerMiddleGrass",
                new Color32(105, 205, 106, 255),
                false);
            Material farGrass = CreateMaterial(
                "FrutigerFarGrass",
                new Color32(132, 218, 126, 255),
                false);
            Material cloudMaterial = CreateMaterial(
                "FrutigerCloud",
                new Color32(247, 255, 253, 255),
                true);
            Material sunMaterial = CreateMaterial(
                "FrutigerSun",
                new Color32(255, 218, 94, 255),
                true);

            Transform hillRoot = CreateRoot("RollingHills");
            CreateEllipsoid(
                "FarHill",
                hillRoot,
                new Vector3(4.8f, -1.05f, 14f),
                new Vector3(22f, 5.6f, 8f),
                farGrass);
            CreateEllipsoid(
                "LeftHill",
                hillRoot,
                new Vector3(-5.3f, -0.85f, 10.5f),
                new Vector3(15f, 4.8f, 6.5f),
                middleGrass);
            CreateEllipsoid(
                "RightHill",
                hillRoot,
                new Vector3(3.8f, -0.95f, 8.5f),
                new Vector3(17f, 4.6f, 6f),
                nearGrass);

            Transform skyRoot = CreateRoot("HappySkyDetails");
            CreateEllipsoid(
                "Sun",
                skyRoot,
                new Vector3(-5.6f, 5.8f, 18f),
                new Vector3(3.2f, 3.2f, 3.2f),
                sunMaterial);

            CreateCloud(
                skyRoot,
                "LeftCloud",
                new Vector3(-3.5f, 4.6f, 13f),
                0.24f,
                0.11f,
                0.2f,
                cloudMaterial);
            CreateCloud(
                skyRoot,
                "RightCloud",
                new Vector3(5.4f, 5.1f, 16f),
                0.32f,
                0.08f,
                2.1f,
                cloudMaterial);

            hasBuiltEnvironment = true;
            Debug.Log(
                "[FrutigerEnvironment] Built rolling hills, clouds, and sun.");
        }

        private Transform CreateRoot(string objectName)
        {
            GameObject rootObject = new GameObject(objectName);
            rootObject.transform.SetParent(transform, false);
            return rootObject.transform;
        }

        private void CreateCloud(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            float driftDistance,
            float driftSpeed,
            float phase,
            Material material)
        {
            Transform cloudRoot = new GameObject(objectName).transform;
            cloudRoot.SetParent(parent, false);
            cloudRoot.localPosition = localPosition;

            CreateEllipsoid(
                "CloudLeft",
                cloudRoot,
                new Vector3(-0.95f, -0.05f, 0f),
                new Vector3(2.2f, 1.05f, 0.9f),
                material);
            CreateEllipsoid(
                "CloudCenter",
                cloudRoot,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(2.8f, 1.45f, 1.05f),
                material);
            CreateEllipsoid(
                "CloudRight",
                cloudRoot,
                new Vector3(1.1f, -0.03f, 0f),
                new Vector3(2.25f, 1f, 0.9f),
                material);
            CreateEllipsoid(
                "CloudBase",
                cloudRoot,
                new Vector3(0.12f, -0.28f, 0.04f),
                new Vector3(3.8f, 0.75f, 0.85f),
                material);

            clouds.Add(new CloudMotion
            {
                transform = cloudRoot,
                startPosition = localPosition,
                distance = driftDistance,
                speed = driftSpeed,
                phase = phase
            });
        }

        private GameObject CreateEllipsoid(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject shape =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shape.name = objectName;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localRotation = Quaternion.identity;
            shape.transform.localScale = localScale;

            Collider shapeCollider = shape.GetComponent<Collider>();
            if (shapeCollider != null)
            {
                Destroy(shapeCollider);
            }

            Renderer shapeRenderer = shape.GetComponent<Renderer>();
            if (shapeRenderer != null)
            {
                shapeRenderer.sharedMaterial = material;
                shapeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shapeRenderer.receiveShadows = false;
                shapeRenderer.lightProbeUsage = LightProbeUsage.Off;
                shapeRenderer.reflectionProbeUsage =
                    ReflectionProbeUsage.Off;
            }
            return shape;
        }

        private Material CreateMaterial(
            string materialName,
            Color color,
            bool unlit)
        {
            Shader shader = Shader.Find(
                unlit
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", unlit ? 0f : 0.18f);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            runtimeMaterials.Add(material);
            return material;
        }

        private static GameObject FindAvatar()
        {
            GameObject avatar = GameObject.Find("CompanionAvatarVRM");
            return avatar != null
                ? avatar
                : GameObject.Find("CompanionAvatarProcedural");
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                {
                    Destroy(runtimeMaterials[i]);
                }
            }
            runtimeMaterials.Clear();
        }
    }
}
