using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AICompanion.UI;

namespace AICompanion.Editor
{
    public static class SetupPoseDropdown
    {
        private static GameObject FindGameObjectInScene(string name)
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                var found = FindRecursive(r.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                var found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        [MenuItem("Tools/Setup Pose Dropdown")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[SetupPoseDropdown] Operating on active scene: {scene.path}");

            var mainPanelGo = FindGameObjectInScene("MainPanel");
            if (mainPanelGo == null)
            {
                Debug.LogError("[SetupPoseDropdown] MainPanel not found in scene!");
                return;
            }

            var cycleButtonGo = FindGameObjectInScene("CyclePoseButton");
            if (cycleButtonGo == null)
            {
                Debug.LogError("[SetupPoseDropdown] CyclePoseButton not found in scene!");
                return;
            }

            // Find or create PoseDropdown
            var dropdownGo = FindGameObjectInScene("PoseDropdown");
            if (dropdownGo == null)
            {
                // Create a standard TextMeshPro Dropdown programmatically
                var resources = new TMPro.TMP_DefaultControls.Resources();
                dropdownGo = TMPro.TMP_DefaultControls.CreateDropdown(resources);
                dropdownGo.name = "PoseDropdown";
            }

            // Configure RectTransform to match the position and size of CyclePoseButton
            var rectTransform = dropdownGo.GetComponent<RectTransform>();
            rectTransform.SetParent(mainPanelGo.transform, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(410f, -450f);
            rectTransform.sizeDelta = new Vector2(250f, 40f);

            // Configure text alignment on the dropdown's label
            var label = dropdownGo.transform.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            if (label != null)
            {
                label.fontSize = 14f;
                label.alignment = TMPro.TextAlignmentOptions.Center;
            }

            // Deactivate original CyclePoseButton
            cycleButtonGo.SetActive(false);

            // Bind the Dropdown component to the CompanionUI script
            var uiComp = Object.FindAnyObjectByType<CompanionUI>();
            if (uiComp != null)
            {
                var dropdownComp = dropdownGo.GetComponent<TMPro.TMP_Dropdown>();
                
                SerializedObject serializedUI = new SerializedObject(uiComp);
                SerializedProperty prop = serializedUI.FindProperty("poseDropdown");
                if (prop != null)
                {
                    prop.objectReferenceValue = dropdownComp;
                    serializedUI.ApplyModifiedProperties();
                    Debug.Log("[SetupPoseDropdown] Successfully bound PoseDropdown to CompanionUI.");
                }
                else
                {
                    Debug.LogError("[SetupPoseDropdown] Failed to find serialized property 'poseDropdown' on CompanionUI.");
                }
            }
            else
            {
                Debug.LogError("[SetupPoseDropdown] CompanionUI component not found in scene!");
            }

            // Save and mark scene dirty
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupPoseDropdown] Scene setup completed successfully and saved.");
        }
    }
}
