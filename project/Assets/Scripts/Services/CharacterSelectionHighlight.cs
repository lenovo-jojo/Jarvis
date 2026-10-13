using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Services
{
    /// <summary>
    /// Adds a soft, body-following silhouette to selected characters. The
    /// source VRM materials are never modified.
    /// </summary>
    public sealed class CharacterSelectionHighlight : MonoBehaviour
    {
        private sealed class OutlinePair
        {
            public Renderer source;
            public Renderer outline;
        }

        private static readonly Color HighlightColor =
            new Color32(255, 108, 174, 190);

        private readonly List<OutlinePair> outlines =
            new List<OutlinePair>();
        private Material outlineMaterial;
        private bool selected;
        private bool initialized;

        public void SetSelected(bool value)
        {
            selected = value;
            if (selected)
            {
                EnsureOutlines();
            }
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (selected)
            {
                RefreshVisibility();
            }
        }

        private void EnsureOutlines()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            Shader shader =
                Resources.Load<Shader>(
                    "Shaders/" +
                    "JarvisCharacterSelectionOutline");
            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Jarvis/Character Selection Outline");
            }
            if (shader == null)
            {
                Debug.LogWarning(
                    "[CharacterSelection] Outline shader was not found.");
                return;
            }

            outlineMaterial = new Material(shader);
            outlineMaterial.SetColor(
                "_OutlineColor",
                HighlightColor);
            bool isSpike =
                gameObject.name.IndexOf(
                    "spike",
                    System.StringComparison
                        .OrdinalIgnoreCase) >= 0;
            outlineMaterial.SetFloat(
                "_OutlineWidth",
                isSpike ? 0.005f : 0.009f);

            SkinnedMeshRenderer[] skinned =
                GetComponentsInChildren<
                    SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer source in skinned)
            {
                CreateSkinnedOutline(source);
            }

            MeshRenderer[] meshes =
                GetComponentsInChildren<
                    MeshRenderer>(true);
            foreach (MeshRenderer source in meshes)
            {
                if (source.transform.name.EndsWith(
                        "_SelectionOutline",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }
                CreateMeshOutline(source);
            }
        }

        private void CreateSkinnedOutline(
            SkinnedMeshRenderer source)
        {
            if (source == null ||
                source.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject =
                new GameObject(
                    source.gameObject.name +
                    "_SelectionOutline");
            CopyRendererTransform(
                source.transform,
                outlineObject.transform);
            SkinnedMeshRenderer outline =
                outlineObject.AddComponent<
                    SkinnedMeshRenderer>();
            outline.sharedMesh = source.sharedMesh;
            outline.bones = source.bones;
            outline.rootBone = source.rootBone;
            outline.localBounds = source.localBounds;
            outline.quality = source.quality;
            outline.updateWhenOffscreen = true;
            outline.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.sharedMaterials =
                BuildMaterialArray(
                    source.sharedMaterials.Length);
            outline.enabled = false;
            outlines.Add(
                new OutlinePair
                {
                    source = source,
                    outline = outline
                });
        }

        private void CreateMeshOutline(
            MeshRenderer source)
        {
            if (source == null)
            {
                return;
            }
            MeshFilter sourceFilter =
                source.GetComponent<MeshFilter>();
            if (sourceFilter == null ||
                sourceFilter.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject =
                new GameObject(
                    source.gameObject.name +
                    "_SelectionOutline");
            CopyRendererTransform(
                source.transform,
                outlineObject.transform);
            MeshFilter filter =
                outlineObject.AddComponent<MeshFilter>();
            filter.sharedMesh =
                sourceFilter.sharedMesh;
            MeshRenderer outline =
                outlineObject.AddComponent<MeshRenderer>();
            outline.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.sharedMaterials =
                BuildMaterialArray(
                    source.sharedMaterials.Length);
            outline.enabled = false;
            outlines.Add(
                new OutlinePair
                {
                    source = source,
                    outline = outline
                });
        }

        private static void CopyRendererTransform(
            Transform source,
            Transform destination)
        {
            destination.SetParent(
                source.parent,
                false);
            destination.localPosition =
                source.localPosition;
            destination.localRotation =
                source.localRotation;
            destination.localScale =
                source.localScale;
        }

        private Material[] BuildMaterialArray(int count)
        {
            int safeCount = Mathf.Max(1, count);
            Material[] materials =
                new Material[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                materials[i] = outlineMaterial;
            }
            return materials;
        }

        private void RefreshVisibility()
        {
            foreach (OutlinePair pair in outlines)
            {
                if (pair?.outline == null)
                {
                    continue;
                }
                pair.outline.enabled =
                    selected &&
                    pair.source != null &&
                    pair.source.enabled &&
                    pair.source.gameObject
                        .activeInHierarchy;
            }
        }

        private void OnDestroy()
        {
            foreach (OutlinePair pair in outlines)
            {
                if (pair?.outline != null)
                {
                    Destroy(
                        pair.outline.gameObject);
                }
            }
            outlines.Clear();

            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }
    }
}
