using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.Core.Utils;

namespace WebGL.Editor
{
    public static class FixDroneFullClean
    {
        private const string RootName = "x500v2_Drone";
        private const string FbxPath = "Assets/Models/x500v2_runtime_low_final.fbx";
        private static readonly Quaternion RootRotation = Quaternion.Euler(-90f, 90f, 0f);

        [MenuItem("Tools/Cleanup/🧹 FULL CLEAN & REBUILD DRONE (Zero Duplicates)", priority = 0)]
        public static void FullCleanAndRebuild()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            // 1. Delete ALL old drones and reference garbage in the scene
            List<GameObject> rootsToDelete = new List<GameObject>();
            foreach (GameObject rootObj in activeScene.GetRootGameObjects())
            {
                string name = rootObj.name;
                if (name == RootName ||
                    name.StartsWith("x500v2_Drone_REFERENCE_") ||
                    name == "__X500V2_PRESERVED_DRONES__" ||
                    name.StartsWith("x500v2_Drone_"))
                {
                    rootsToDelete.Add(rootObj);
                }
            }

            foreach (GameObject go in rootsToDelete)
            {
                if (go != null) Undo.DestroyObjectImmediate(go);
            }
            Debug.Log($"[FixDroneFullClean] Cleaned {rootsToDelete.Count} old/stale drone roots from scene.");

            // 2. Load and instantiate fresh FBX
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not load FBX at {FbxPath}", "OK");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, activeScene);
            instance.name = RootName;
            instance.transform.SetPositionAndRotation(Vector3.zero, RootRotation);
            instance.transform.localScale = Vector3.one;
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 3. Keep all 252 meshes intact from clean FBX (no deletions)
            Debug.Log($"[FixDroneFullClean] Using all {instance.transform.childCount} meshes directly from clean FBX.");

            // 4. Assign unified baked textures to all materials
            AssignDroneTextures.ApplyTexturesToAllDroneMaterials();

            // 5. Run runtime hierarchy normalization (groups fasteners into x500v2_fastener_group, sets up propellers)
            ImportDroneModel.NormalizeRuntimeHierarchy(instance.transform);

            // 6. Run thermal & modular fastener catalog setup
            SetupImportedDroneThermalTest.PrepareImportedDroneHeadless();

            // 7. Remove any unwanted synthetic proxy cubes (e.g. *runtime_proxy)
            Transform[] afterSetupChildren = instance.GetComponentsInChildren<Transform>(true);
            int proxiesRemoved = 0;
            foreach (Transform t in afterSetupChildren)
            {
                if (t != null && t.name.EndsWith("_runtime_proxy", StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                    proxiesRemoved++;
                }
            }
            if (proxiesRemoved > 0)
            {
                Debug.Log($"[FixDroneFullClean] Removed {proxiesRemoved} synthetic runtime proxy cubes.");
            }

            // 6. Bind camera target to the clean root
            OrbitCameraController[] controllers = UnityEngine.Object.FindObjectsByType<OrbitCameraController>(FindObjectsInactive.Include);
            foreach (OrbitCameraController controller in controllers)
            {
                if (controller != null)
                {
                    SerializedObject serializedController = new SerializedObject(controller);
                    SerializedProperty targetProperty = serializedController.FindProperty("target");
                    if (targetProperty != null)
                    {
                        targetProperty.objectReferenceValue = instance.transform;
                        serializedController.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(controller);
                    }
                }
            }

            // 7. Save scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            string successMsg = $"¡Dron completamente limpio y reconstruido!\n\n- Raíces viejas eliminadas: {rootsToDelete.Count}\n- Mallas de FBX conservadas: {instance.transform.childCount}\n- Texturas horneadas asignadas\n- Rotación corregida a plano horizontal.";
            Debug.Log($"<color=green>[FixDroneFullClean] {successMsg}</color>");
            EditorUtility.DisplayDialog("Clean & Rebuild Drone", successMsg, "OK");
        }
    }
}
