#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICompanion.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICompanion.Editor
{
    public static class PossumSpringsSceneBuilder
    {
        private const int BuilderVersion = 9;
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
                root.AddComponent<
                    PossumSpringsBootstrap>();
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

                Vector3 spawnPosition =
                    FindParkingLotSpawn(town, bounds);
                AddSpawnMarker(
                    root,
                    "ParkingLotSpawn",
                    spawnPosition);
                AddSpawnMarker(
                    root,
                    "PlayerSpawn",
                    spawnPosition);
                AddLighting(root);
                AddOverviewCamera(
                    root,
                    bounds,
                    spawnPosition);

                GameObject savedPrefab =
                    PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath);
                Scene openTargetScene =
                    SceneManager.GetSceneByPath(
                        ScenePath);
                if (openTargetScene.IsValid() &&
                    openTargetScene.isLoaded &&
                    openTargetScene != scene)
                {
                    ReplaceOpenGeneratedScene(
                        openTargetScene,
                        savedPrefab);
                }
                else
                {
                    EditorSceneManager.SaveScene(
                        scene,
                        ScenePath);
                }
                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorPrefs.SetInt(
                    BuilderVersionKey,
                    BuilderVersion);

                Debug.Log(
                    $"[PossumSprings] Built scene and prefab. " +
                    $"Walking colliders: {colliderCount}. " +
                    $"Bounds: {bounds.size}. " +
                    $"Spawn: {spawnPosition}.");
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

                Shader worldShader =
                    Shader.Find(
                        "JARVIS/Possum Springs Vertex Color");
                if (worldShader != null)
                {
                    material.shader = worldShader;
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
                else
                {
                    ConfigureAtlasMaterial(material);
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

        private static void ConfigureAtlasMaterial(
            Material material)
        {
            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", 0.1f);
            }
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 1f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }
            material.SetOverrideTag(
                "RenderType",
                "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
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

        private static Vector3 FindParkingLotSpawn(
            GameObject town,
            Bounds bounds)
        {
            Physics.SyncTransforms();

            Vector3 center =
                new Vector3(
                    bounds.center.x,
                    0f,
                    bounds.center.z);
            float searchRadius =
                Mathf.Clamp(
                    Mathf.Min(
                        bounds.size.x,
                        bounds.size.z) * 0.08f,
                    30f,
                    180f);
            float rayStartY =
                bounds.max.y + 20f;
            float rayDistance =
                bounds.size.y + 80f;
            bool found = false;
            Vector3 bestPoint =
                new Vector3(
                    center.x,
                    bounds.min.y,
                    center.z);
            float bestScore = float.MaxValue;

            const int gridRadius = 5;
            for (int xIndex = -gridRadius;
                 xIndex <= gridRadius;
                 xIndex++)
            {
                for (int zIndex = -gridRadius;
                     zIndex <= gridRadius;
                     zIndex++)
                {
                    float x =
                        center.x +
                        xIndex *
                        searchRadius /
                        gridRadius;
                    float z =
                        center.z +
                        zIndex *
                        searchRadius /
                        gridRadius;
                    RaycastHit[] hits =
                        Physics.RaycastAll(
                            new Vector3(
                                x,
                                rayStartY,
                                z),
                            Vector3.down,
                            rayDistance);
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.collider == null ||
                            !hit.collider.transform.IsChildOf(
                                town.transform) ||
                            hit.normal.y < 0.85f ||
                            !HasStandingClearance(
                                hit.point,
                                town))
                        {
                            continue;
                        }

                        int openAreaSamples =
                            CountFlatAreaSamples(
                                hit.point,
                                town,
                                rayStartY,
                                rayDistance);
                        float horizontalDistance =
                            Vector2.Distance(
                                new Vector2(x, z),
                                new Vector2(
                                    center.x,
                                    center.z));
                        float score =
                            -openAreaSamples * 1000f +
                            (hit.point.y - bounds.min.y) * 20f +
                            horizontalDistance * 0.03f;
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestPoint = hit.point;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[PossumSprings] No open parking-lot " +
                    "surface was found; using the bounds center.");
            }

            Vector3 insetPoint =
                bestPoint +
                Vector3.forward * 6f;
            if (TryFindSurfaceNearHeight(
                    insetPoint,
                    bestPoint.y,
                    town,
                    rayStartY,
                    rayDistance,
                    out Vector3 insetSurface))
            {
                bestPoint = insetSurface;
            }
            else
            {
                bestPoint = insetPoint;
            }

            return bestPoint + Vector3.up * 0.08f;
        }

        private static bool TryFindSurfaceNearHeight(
            Vector3 position,
            float expectedHeight,
            GameObject town,
            float rayStartY,
            float rayDistance,
            out Vector3 surface)
        {
            surface = position;
            RaycastHit[] hits =
                Physics.RaycastAll(
                    new Vector3(
                        position.x,
                        rayStartY,
                        position.z),
                    Vector3.down,
                    rayDistance);
            float bestDifference = float.MaxValue;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    !hit.collider.transform.IsChildOf(
                        town.transform) ||
                    hit.normal.y < 0.85f ||
                    !HasStandingClearance(
                        hit.point,
                        town))
                {
                    continue;
                }

                float difference =
                    Mathf.Abs(
                        hit.point.y -
                        expectedHeight);
                if (difference >= bestDifference ||
                    difference > 0.6f)
                {
                    continue;
                }

                bestDifference = difference;
                surface = hit.point;
                found = true;
            }
            return found;
        }

        private static bool HasStandingClearance(
            Vector3 point,
            GameObject town)
        {
            RaycastHit[] hits =
                Physics.RaycastAll(
                    point + Vector3.up * 0.15f,
                    Vector3.up,
                    2.2f);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    hit.collider.transform.IsChildOf(
                        town.transform))
                {
                    return false;
                }
            }
            return true;
        }

        private static int CountFlatAreaSamples(
            Vector3 center,
            GameObject town,
            float rayStartY,
            float rayDistance)
        {
            int matches = 0;
            const int sampleRadius = 2;
            const float spacing = 5f;
            for (int xIndex = -sampleRadius;
                 xIndex <= sampleRadius;
                 xIndex++)
            {
                for (int zIndex = -sampleRadius;
                     zIndex <= sampleRadius;
                     zIndex++)
                {
                    float x =
                        center.x + xIndex * spacing;
                    float z =
                        center.z + zIndex * spacing;
                    RaycastHit[] hits =
                        Physics.RaycastAll(
                            new Vector3(
                                x,
                                rayStartY,
                                z),
                            Vector3.down,
                            rayDistance);
                    bool sampleMatched = false;
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.collider == null ||
                            !hit.collider.transform.IsChildOf(
                                town.transform) ||
                            hit.normal.y < 0.85f ||
                            Mathf.Abs(
                                hit.point.y -
                                center.y) > 0.45f ||
                            !HasStandingClearance(
                                hit.point,
                                town))
                        {
                            continue;
                        }

                        sampleMatched = true;
                        break;
                    }

                    if (sampleMatched)
                    {
                        matches++;
                    }
                }
            }
            return matches;
        }

        private static void AddSpawnMarker(
            GameObject root,
            string markerName,
            Vector3 spawnPosition)
        {
            GameObject spawn =
                new GameObject(markerName);
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = spawnPosition;
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
            Bounds bounds,
            Vector3 spawnPosition)
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
            Vector3 target =
                spawnPosition +
                new Vector3(0f, 1.55f, 12f);
            cameraObject.transform.position =
                spawnPosition +
                new Vector3(0f, 1.65f, -0.25f);
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

        private static void ReplaceOpenGeneratedScene(
            Scene targetScene,
            GameObject prefab)
        {
            foreach (GameObject root in
                targetScene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(
                    root);
            }

            PrefabUtility.InstantiatePrefab(
                prefab,
                targetScene);
            EditorSceneManager.MarkSceneDirty(
                targetScene);
            EditorSceneManager.SaveScene(
                targetScene);
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
