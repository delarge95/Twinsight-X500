using UnityEngine;
using UnityEditor;
using System.IO;

namespace WebGL.Editor
{
    public static class AssignDroneTextures
    {
        [MenuItem("Tools/Materials/Assign Drone Baked Textures", priority = 10)]
        public static void ApplyTexturesToAllDroneMaterials()
        {
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_diffuse_union.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_normal_resultante.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_ao.png");
            Texture2D curve = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_curve.png");
            Shader clippableShader = Shader.Find("WebGL/ClippableLit");

            if (diffuse == null)
            {
                Debug.LogError("[AssignDroneTextures] final_curve_diffuse_union.png not found in Assets/Materials!");
                return;
            }

            int count = 0;
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Models/Materials", "Assets/Materials" });

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (mat.name.StartsWith("EdgeDetection") || mat.name.StartsWith("Ghosted") || mat.name.StartsWith("Thermal") || mat.name.StartsWith("Wireframe") || mat.name.StartsWith("XRay") || mat.name.StartsWith("SolidColor"))
                {
                    continue;
                }

                if (clippableShader != null && mat.shader != clippableShader)
                {
                    mat.shader = clippableShader;
                }

                // Assign textures
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
                if (mat.HasProperty("_BumpMap") && normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (mat.HasProperty("_OcclusionMap") && ao != null)
                {
                    mat.SetTexture("_OcclusionMap", ao);
                    mat.SetFloat("_OcclusionStrength", 1.0f);
                }
                if (mat.HasProperty("_CurvatureMap") && curve != null)
                {
                    mat.SetTexture("_CurvatureMap", curve);
                }

                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_Color", Color.white);

                EditorUtility.SetDirty(mat);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[AssignDroneTextures] Successfully assigned baked textures to {count} materials!</color>");
        }
    }
}
