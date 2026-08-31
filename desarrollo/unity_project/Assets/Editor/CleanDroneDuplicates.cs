using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WebGL.Editor
{
    public static class CleanDroneDuplicates
    {
        [MenuItem("Tools/Cleanup/Clean Drone Duplicate Meshes", priority = 5)]
        public static void CleanDuplicates()
        {
            GameObject drone = GameObject.Find("x500v2_Drone");
            if (drone == null)
            {
                Debug.LogError("[CleanDroneDuplicates] 'x500v2_Drone' not found in current scene!");
                EditorUtility.DisplayDialog("Clean Drone Duplicates", "'x500v2_Drone' not found in the active scene.", "OK");
                return;
            }

            // 1. Remove __X500V2_PRESERVED_DRONES__ if present
            GameObject preserved = GameObject.Find("__X500V2_PRESERVED_DRONES__");
            if (preserved != null)
            {
                Undo.DestroyObjectImmediate(preserved);
                Debug.Log("[CleanDroneDuplicates] Removed '__X500V2_PRESERVED_DRONES__'.");
            }

            Transform droneTransform = drone.transform;
            HashSet<GameObject> toDelete = new HashSet<GameObject>();

            // Los sufijos .001/_001 de Blender son instancias legítimas, NO duplicados.
            // Solo se eliminan superposiciones exactas: misma malla y misma posición mundial.
            Transform[] allTransforms = droneTransform.GetComponentsInChildren<Transform>(true);
            HashSet<string> seenMeshSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform t in allTransforms)
            {
                if (t == null || t == droneTransform) continue;

                MeshFilter meshFilter = t.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh == null || t.GetComponent<Renderer>() == null) continue;

                string signature = $"{mesh.name}@{t.position.ToString("F5")}";
                if (!seenMeshSignatures.Add(signature))
                {
                    toDelete.Add(t.gameObject);
                    Debug.Log($"[CleanDroneDuplicates] Superposicion exacta eliminada: '{t.name}' (malla {mesh.name} en {t.position})");
                }
            }

            int deletedCount = 0;
            foreach (GameObject obj in toDelete)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                    deletedCount++;
                }
            }

            // 4. Re-run setup for thermal & runtime binders
            SetupImportedDroneThermalTest.PrepareImportedDroneHeadless();

            // 5. Mark dirty and save scene
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[CleanDroneDuplicates] Successfully removed {deletedCount} duplicate objects from x500v2_Drone!</color>");
            EditorUtility.DisplayDialog("Clean Drone Duplicates", $"Successfully removed {deletedCount} duplicate objects and updated the scene!", "OK");
        }
    }
}
