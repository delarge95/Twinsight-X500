using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DroneTextureQualityImporter
{
    private const string TexturesFolder = "Assets/Materials";

    [MenuItem("Tools/Drone Tools/Set Drone Textures Ultra Quality")]
    public static void SetUltraQuality()
    {
        ForceWebGLDxtSubtarget();
        RunImport();
    }

    [MenuItem("Tools/Drone Tools/Force WebGL DXT Texture Subtarget")]
    public static void ForceWebGLDxtSubtarget()
    {
        // El valor de Build Settings (Library, fuera de version control) pisa el de
        // Player Settings. Si quedo en ASTC, Safari no soporta ASTC y el build rompe.
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
        Debug.Log("[DroneTextureQualityImporter] WebGL texture subtarget forzado a DXT.");
    }

    [MenuItem("Tools/Drone Tools/Clean Old Drone Reference Models")]
    public static void CleanReferenceModels()
    {
        string[] rootNames =
        {
            "x500v2_ReferenceModels",
            "x500v2_Drone_REFERENCE",
            "DroneAssembler",
            "__X500V2_PRESERVED_DRONES__"
        };

        int removed = 0;
        foreach (string rootName in rootNames)
        {
            GameObject[] candidates;
            if (rootName == "x500v2_Drone_REFERENCE")
            {
                candidates = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                candidates = System.Array.FindAll(candidates, g => g.name.StartsWith("x500v2_Drone_REFERENCE", System.StringComparison.Ordinal));
            }
            else
            {
                candidates = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                candidates = System.Array.FindAll(candidates, g => g.name == rootName);
            }

            foreach (GameObject candidate in candidates)
            {
                Undo.DestroyObjectImmediate(candidate);
                removed++;
            }
        }

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Clean References", $"Eliminados {removed} objetos de referencia viejos. Escena guardada.", "OK");
        Debug.Log($"[DroneTextureQualityImporter] Referencias viejas eliminadas: {removed}");
    }

    private static void RunImport()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Texture Quality", $"No se encontraron texturas en {TexturesFolder}.", "OK");
            return;
        }

        int updated = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                // Solo el diffuse necesita 4096 (letras/serigrafia). El resto a 2048:
                // 13 texturas a 4096 superan los ~200MB de payload en WebGL.
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool isDiffuse = fileName.Contains("diffuse") || fileName.Contains("albedo") || fileName.Contains("basecolor");
                int targetMaxSize = isDiffuse ? 4096 : 2048;

                bool changed = false;
                if (importer.maxTextureSize != targetMaxSize)
                {
                    importer.maxTextureSize = targetMaxSize;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Compressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    changed = true;
                }

                if (importer.compressionQuality != 100)
                {
                    importer.compressionQuality = 100;
                    changed = true;
                }

                TextureImporterPlatformSettings webglSettings = importer.GetPlatformTextureSettings("WebGL");
                if (!webglSettings.overridden || webglSettings.maxTextureSize != targetMaxSize || webglSettings.compressionQuality != 100)
                {
                    TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings
                    {
                        name = "WebGL",
                        overridden = true,
                        maxTextureSize = targetMaxSize,
                        format = TextureImporterFormat.Automatic,
                        textureCompression = TextureImporterCompression.Compressed,
                        compressionQuality = 100
                    };
                    importer.SetPlatformTextureSettings(settings);
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    updated++;
                }

                if (i % 4 == 0)
                {
                    float progress = (float)i / guids.Length;
                    if (EditorUtility.DisplayCancelableProgressBar("Texture Quality", path, progress))
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Texture Quality", $"Texturas actualizadas a 4096px / calidad maxima: {updated}.", "OK");
        Debug.Log($"[DroneTextureQualityImporter] {updated} texturas reimportadas en ultra calidad.");
    }
}
