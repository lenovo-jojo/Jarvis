#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICompanion.Editor
{
    public static class PossumSpringsSceneBuilder
    {
        private const int BuilderVersion = 3;
        private const string BuilderVersionKey =
            "AICompanion.PossumSpringsBuilderVersion";
        private const string ModelPath =
            "Assets/Environments/PossumSprings/Models/NITWworld.fbx";
        private const string AtlasPath =
            "Assets/Environments/PossumSprings/Textures/Atlas.png";
        private const string LeavesPath =
            "Assets/Environments/PossumSprings/Textures/Leaves.png";
        private const string MaterialsFolder =
            "Assets/Environments/PossumSprings/Materials";
        private const string PrefabsFolder =
            "Assets/Environments/PossumSprings/Prefabs";
        private const string PrefabPath =
            PrefabsFolder + "/PossumSpringsTown.prefab";
        private const string ScenePath =
            "Assets/Scenes/PossumSprings.unity";

        [InitializeOnLoadMethod]
        private static void ScheduleInitialBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ScenePath) != null &&
                EditorPrefs.GetInt(
                    BuilderVersionKey,
                    0) >= BuilderVersion)
            {
                return;
            }

            EditorApplication.delayCall +=
                TryInitialBuild;
        }

        private static void TryInitialBuild()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall +=
                    TryInitialBuild;
                return;
            }

            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PossumSprings] Initial scene build failed: " +
                    exception);
            }
        }

        [MenuItem("JARVIS/worlds/build possum springs scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            GameObject model =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    ModelPath);
            if (model == null)
            {
                throw new FileNotFoundException(
                    "Possum Springs FBX was not imported.",
                    ModelPath);
            }

            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            Dictionary<string, Material> materials =
                BuildMaterials();

            Scene previousScene =
                SceneManager.GetActiveScene();
            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject root =
                    new GameObject("PossumSpringsScene");
                GameObject town =
                    PrefabUtility.InstantiatePrefab(
                        model,
                        scene) as GameObject;
                if (town == null)
                {
                    throw new InvalidOperationException(
                        "Unity could not instantiate NITWworld.fbx.");
                }

                town.name = "PossumSpringsTown";
                town.transform.SetParent(
                    root.transform,
                    true);
                town.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                town.transform.localScale = Vector3.one;

                ApplyMaterials(town, materials);
                Bounds bounds = CalculateBounds(town);
                town.transform.position +=
                    Vector3.up * -bounds.min.y;
                bounds = CalculateBounds(town);

                int colliderCount =
                    AddWalkingSurfaceColliders(town);
                if (colliderCount == 0)
                {
                    AddFallbackGround(root, bounds);
                }

                AddSpawnMarker(root);
                AddLighting(root);
                AddOverviewCamera(root, bounds);

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath);
                EditorSceneManager.SaveScene(
                    scene,
                    ScenePath);
                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorPrefs.SetInt(
                    BuilderVersionKey,
                    BuilderVersion);

                Debug.Log(
                    $"[PossumSprings] Built scene and prefab. " +
                    $"Walking colliders: {colliderCount}. " +
                    $"Bounds: {bounds.size}.");
            }
            finally
            {
                if (previousScene.IsValid())
                {
                    SceneManager.SetActiveScene(
                        previousScene);
                }
                if (scene.IsValid() &&
                    scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(
                        scene,
                        true);
                }
            }
        }

        private static Dictionary<string, Material>
            BuildMaterials()
        {
            Texture2D atlas =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AtlasPath);
            Texture2D leaves =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    LeavesPath);
            Material[] embeddedMaterials =
                AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                    .OfType<Material>()
                    .ToArray();
            Dictionary<string, Material> result =
                new Dictionary<string, Material>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (Material embedded in embeddedMaterials)
            {
                if (embedded == null)
                {
                    continue;
                }

                string safeName =
                    MakeSafeAssetName(embedded.name);
                string path =
                    $"{MaterialsFolder}/{safeName}.mat";
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        path);
                if (material == null)
                {
                    material = new Material(embedded)
                    {
                        name = embedded.name
                    };
                    AssetDatabase.CreateAsset(
                        material,
                        path);
                }
                else
                {
                    material.CopyPropertiesFromMaterial(
                        embedded);
                }

                bool isLeaves =
                    embedded.name.IndexOf(
                        "leave",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    embedded.name.IndexOf(
                        "foliage",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    embedded.name.IndexOf(
                        "triplanar",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                Texture2D texture =
                    isLeaves ? leaves : atlas;
                if (texture != null)
                {
                    material.mainTexture = texture;
                    if (material.HasProperty("_BaseMap"))
                    {
                        material.SetTexture(
                            "_BaseMap",
                            texture);
                    }
                }

                if (isLeaves)
                {
                    ConfigureLeafMaterial(material);
                }

                EditorUtility.SetDirty(material);
                result[embedded.name] = material;
            }

            return result;
        }

        private static void ConfigureLeafMaterial(
            Material material)
        {
            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", 0.35f);
            }
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 1f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }
            material.EnableKeyword("_ALPHATEST_ON");
        }

        private static void ApplyMaterials(
            GameObject town,
            IReadOnlyDictionary<string, Material> materials)
        {
            foreach (Renderer renderer in
                town.GetComponentsInChildren<Renderer>(true))
            {
                Material[] assigned =
                    renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0;
                     index < assigned.Length;
                     index++)
                {
                    Material source = assigned[index];
                    if (source != null &&
                        materials.TryGetValue(
                            source.name,
                            out Material replacement))
                    {
                        assigned[index] = replacement;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = assigned;
                }
            }
        }

        private static Bounds CalculateBounds(
            GameObject root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(
                    root.transform.position,
                    Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1;
                 index < renderers.Length;
                 index++)
            {
                bounds.Encapsulate(
                    renderers[index].bounds);
            }
            return bounds;
        }

        private static int AddWalkingSurfaceColliders(
            GameObject town)
        {
            int count = 0;
            foreach (MeshFilter filter in
                town.GetComponentsInChildren<MeshFilter>(
                    true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                filter.gameObject.isStatic = true;
                MeshCollider collider =
                    filter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider =
                        filter.gameObject.AddComponent<
                            MeshCollider>();
                }
                collider.sharedMesh = filter.sharedMesh;
                count++;
            }
            return count;
        }

        private static void AddFallbackGround(
            GameObject root,
            Bounds bounds)
        {
            GameObject ground =
                new GameObject("PossumSpringsFallbackGround");
            ground.transform.SetParent(root.transform, false);
            ground.transform.position =
                new Vector3(
                    bounds.center.x,
                    0f,
                    bounds.center.z);
            BoxCollider collider =
                ground.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size =
                new Vector3(
                    Mathf.Max(10f, bounds.size.x),
                    0.2f,
                    Mathf.Max(10f, bounds.size.z));
        }

        private static void AddSpawnMarker(
            GameObject root)
        {
            GameObject spawn =
                new GameObject("PlayerSpawn");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position =
                new Vector3(0f, 0.1f, 0f);
        }

        private static void AddLighting(
            GameObject root)
        {
            GameObject lightObject =
                new GameObject("PossumSpringsSun");
            lightObject.transform.SetParent(
                root.transform,
                false);
            lightObject.transform.rotation =
                Quaternion.Euler(42f, -32f, 0f);
            Light light =
                lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        private static void AddOverviewCamera(
            GameObject root,
            Bounds bounds)
        {
            GameObject cameraObject =
                new GameObject("PossumSpringsOverviewCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(
                root.transform,
                false);
            Camera camera =
                cameraObject.AddComponent<Camera>();
            camera.clearFlags =
                CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane =
                Mathf.Max(
                    1000f,
                    bounds.size.magnitude * 4f);

            float distance =
                Mathf.Max(
                    15f,
                    bounds.size.magnitude * 0.65f);
            Vector3 target =
                bounds.center +
                Vector3.up * bounds.extents.y * 0.15f;
            cameraObject.transform.position =
                target +
                new Vector3(
                    distance * 0.55f,
                    distance * 0.35f,
                    -distance);
            cameraObject.transform.LookAt(target);
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();
            int existingIndex =
                scenes.FindIndex(
                    item =>
                        item.path.Equals(
                            ScenePath,
                            StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                EditorBuildSettingsScene existing =
                    scenes[existingIndex];
                if (!existing.enabled)
                {
                    scenes[existingIndex] =
                        new EditorBuildSettingsScene(
                            ScenePath,
                            true);
                }
            }
            else
            {
                scenes.Add(
                    new EditorBuildSettingsScene(
                        ScenePath,
                        true));
            }

            EditorBuildSettings.scenes =
                scenes.ToArray();
        }

        private static string MakeSafeAssetName(
            string value)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();
            return new string(
                value.Select(
                    character =>
                        invalid.Contains(character)
                            ? '_'
                            : character)
                    .ToArray());
        }

        private static void EnsureFolder(
            string path)
        {
            string current = "Assets";
            foreach (string part in
                path.Split('/').Skip(1))
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        part);
                }
                current = next;
            }
        }
    }
}
#endif
