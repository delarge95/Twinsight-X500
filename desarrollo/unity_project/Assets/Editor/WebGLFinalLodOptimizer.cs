using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time editor cleanup that removes every LODGroup and generated LOD
/// child container (__LOD_GENERATED) from the active scene, then deletes
/// the Assets/Generated/LOD folder.  Run once and delete this file afterwards.
/// </summary>
public static class WebGLFinalLodRollback
{
    private const string GeneratedAssetFolder = "Assets/Generated/LOD";
    private const string GeneratedObjectName  = "__LOD_GENERATED";

    [MenuItem("Tools/X500V2/Optimization/Remove ALL LODs (Full Rollback)")]
    public static void RemoveAllLods()
    {
        if (!EditorUtility.DisplayDialog(
                "Remove ALL LODs — Full Rollback",
                "This will:\n" +
                "• Destroy every LODGroup component in the active scene.\n" +
                "• Destroy every child GameObject named __LOD_GENERATED.\n" +
                "• Delete Assets/Generated/LOD and its contents.\n\n" +
                "Source meshes, authored content and original renderers are preserved.",
                "Remove ALL LODs",
                "Cancel"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        int removedGroups   = 0;
        int removedObjects  = 0;

        // --- Pass 1: destroy every LODGroup component ---
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            foreach (LODGroup group in groups)
            {
                if (group != null)
                {
                    Object.DestroyImmediate(group);
                    removedGroups++;
                }
            }
        }

        // --- Pass 2: destroy every __LOD_GENERATED container ---
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            List<Transform> generatedContainers = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == GeneratedObjectName)
                    generatedContainers.Add(t);
            }

            // Destroy bottom-up to avoid parent-child issues
            for (int i = generatedContainers.Count - 1; i >= 0; i--)
            {
                if (generatedContainers[i] != null)
                {
                    Object.DestroyImmediate(generatedContainers[i].gameObject);
                    removedObjects++;
                }
            }
        }

        // --- Pass 3: delete generated mesh assets ---
        if (AssetDatabase.IsValidFolder(GeneratedAssetFolder))
        {
            AssetDatabase.DeleteAsset(GeneratedAssetFolder);
            Debug.Log($"[WebGLFinalLodRollback] Deleted asset folder: {GeneratedAssetFolder}");
        }

        // Also clean up the parent Generated folder if empty
        if (AssetDatabase.IsValidFolder("Assets/Generated"))
        {
            string[] remaining = AssetDatabase.FindAssets("", new[] { "Assets/Generated" });
            if (remaining.Length == 0)
            {
                AssetDatabase.DeleteAsset("Assets/Generated");
                Debug.Log("[WebGLFinalLodRollback] Deleted empty Assets/Generated folder.");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WebGLFinalLodRollback] Complete. Removed {removedGroups} LODGroup components and {removedObjects} generated containers.");

        EditorUtility.DisplayDialog(
            "LOD Rollback Complete",
            $"Removed {removedGroups} LODGroup components.\n" +
            $"Removed {removedObjects} __LOD_GENERATED containers.\n\n" +
            "The scene has been saved. You can now delete:\n" +
            "• Assets/Editor/WebGLFinalLodOptimizer.cs\n" +
            "• Assets/Scripts/Core/Content/LodGeneratedRenderer.cs\n\n" +
            "and their .meta files.",
            "OK");
    }
}
