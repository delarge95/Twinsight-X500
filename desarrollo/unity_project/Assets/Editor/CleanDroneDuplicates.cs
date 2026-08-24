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

            // 2. Name-based check (.001, (1), etc.)
            Transform[] allTransforms = droneTransform.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t == null || t == droneTransform) continue;
                string n = t.name.Trim();

                if (n.EndsWith(".001", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith("_001", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(" (1)", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains(".001_low") ||
                    n.Contains(".001.") ||
                    n.Contains("_low.001"))
                {
                    toDelete.Add(t.gameObject);
                }
            }

            // 3. Spatial Coincidence Check: Find renderers sharing identical position & mesh
            Renderer[] allRenderers = droneTransform.GetComponentsInChildren<Renderer>(true);
            List<Renderer> validRenderers = new List<Renderer>();
            foreach (Renderer r in allRenderers)
            {
                if (r == null || toDelete.Contains(r.gameObject)) continue;
                validRenderers.Add(r);
            }

            for (int i = 0; i < validRenderers.Count; i++)
            {
                Renderer rA = validRenderers[i];
                if (rA == null || toDelete.Contains(rA.gameObject)) continue;

                Vector3 posA = rA.transform.position;
                MeshFilter mfA = rA.GetComponent<MeshFilter>();
                Mesh meshA = mfA != null ? mfA.sharedMesh : null;

                for (int j = i + 1; j < validRenderers.Count; j++)
                {
                    Renderer rB = validRenderers[j];
                    if (rB == null || toDelete.Contains(rB.gameObject)) continue;

                    Vector3 posB = rB.transform.position;
                    MeshFilter mfB = rB.GetComponent<MeshFilter>();
                    Mesh meshB = mfB != null ? mfB.sharedMesh : null;

                    // If positions are virtually identical (within 1mm)
                    if (Vector3.Distance(posA, posB) < 0.002f)
                    {
                        // Same mesh or same vertex count
                        if (meshA != null && meshB != null && (meshA == meshB || meshA.vertexCount == meshB.vertexCount))
                        {
                            // Mark rB for deletion (keep rA)
                            toDelete.Add(rB.gameObject);
                            Debug.Log($"[CleanDroneDuplicates] Spatial duplicate found: '{rB.name}' duplicate of '{rA.name}' at {posA}");
                        }
                    }
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
