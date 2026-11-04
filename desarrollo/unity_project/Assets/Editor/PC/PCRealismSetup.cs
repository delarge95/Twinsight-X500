using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WebGL.Editor.PC
{
    /// <summary>
    /// Fase 3A (PC): genera (idempotente) HDRI importado como cubemap, la normal de trenzado de carbono,
    /// los materiales PC (shader TwinSight/DroneLitPC), el skybox HDRI y la feature SSAO de PC_Renderer.
    /// Todo lo de runtime vive en Assets/Resources/PC y solo se asigna en Standalone (PCMaterialSwapper).
    /// En WebGL las texturas se reducen a 16-32 px (override del importer) y el shader se elimina.
    /// </summary>
    public static class PCRealismSetup
    {
        public const string HdriPath = "Assets/Settings/PC/HDRI/studio_small_09_2k.hdr";
        public const string ResFolder = "Assets/Resources/PC";
        public const string MatFolder = ResFolder + "/Materials";
        public const string TexFolder = ResFolder + "/Textures";
        public const string WeavePath = TexFolder + "/pc_carbon_weave_n.png";
        public const string SkyMatPath = ResFolder + "/PC_HDRISky.mat";
        private const string SourceMatFolder = "Assets/Models/Materials";

        [MenuItem("TwinSight/PC/Build PC Realism (fase 3A)")]
        public static void BuildAll()
        {
            EnsureFolders();
            ImportHdri();
            GenerateWeave();
            BuildSkyMaterial();
            BuildMaterials();
            BuildSsao();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PCRealismSetup] Listo: HDRI, trenzado, skybox, materiales PC y SSAO.");
        }

        private static void EnsureFolders()
        {
            foreach (string f in new[] { "Assets/Resources", ResFolder, MatFolder, TexFolder })
            {
                if (!AssetDatabase.IsValidFolder(f))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(f).Replace('\\', '/'), Path.GetFileName(f));
                }
            }
        }

        // ── HDRI ──
        private static void ImportHdri()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(HdriPath);
            if (imp == null) { Debug.LogError("[PCRealismSetup] Falta " + HdriPath); return; }
            imp.textureShape = TextureImporterShape.TextureCube;
            imp.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = true;
            imp.isReadable = false;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.filterMode = FilterMode.Trilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            var web = imp.GetPlatformTextureSettings("WebGL");
            web.overridden = true; web.maxTextureSize = 16;
            imp.SetPlatformTextureSettings(web);
            imp.SaveAndReimport();
        }

        // ── Trenzado de carbono (sarga 2x2, tileable) ──
        private static void GenerateWeave()
        {
            const int N = 512, cell = 32;
            float[] h = new float[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int i = x / cell, j = y / cell;
                    float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
                    bool warpOver = ((i + j) & 3) < 2;
                    float across = warpOver ? fx : fy;
                    float along = warpOver ? fy : fx;
                    float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * across), 0.8f);
                    float arch = 0.8f + 0.2f * Mathf.Sin(Mathf.PI * along);
                    float level = warpOver ? 1f : 0.55f;
                    h[y * N + x] = profile * arch * level;
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
            var px = new Color32[N * N];
            const float strength = 5f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float l = h[y * N + (x + N - 1) % N], r = h[y * N + (x + 1) % N];
                    float d = h[((y + N - 1) % N) * N + x], u = h[((y + 1) % N) * N + x];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(WeavePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WeavePath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(WeavePath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 512;
            var web = imp.GetPlatformTextureSettings("WebGL");
            web.overridden = true; web.maxTextureSize = 32;
            imp.SetPlatformTextureSettings(web);
            imp.SaveAndReimport();
        }

        private static void BuildSkyMaterial()
        {
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(HdriPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(mat, SkyMatPath);
            }
            mat.SetTexture("_Tex", cube);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            mat.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(mat);
        }

        // ── Materiales PC ──
        private struct Def
        {
            public string name, source;
            public Color color;
            public float metallic, smooth, bump;
            public bool atlas;                 // usa las texturas del atlas del dron (la fuente)
            public float clearCoat, coatSmooth;
            public float detail, detailTiling;
            public bool translucent;
            public bool rough;                 // conecta final_curve_roughness.png
        }

        private static Def D(string name, string source, bool atlas, float metallic, float smooth)
        {
            return new Def { name = name, source = source, atlas = atlas, color = Color.white, metallic = metallic, smooth = smooth, bump = 1f };
        }

        private static List<Def> Defs()
        {
            var l = new List<Def>();
            Def d;
            d = D("PC_CarbonPlate", "carbon_fiber_01", true, 0.0f, 0.42f); d.bump = 0.25f; d.clearCoat = 1f; d.coatSmooth = 0.7f; d.detail = 0f; d.detailTiling = 28f; l.Add(d);
            d = D("PC_CarbonTube", "MAT_Plastic_Black", true, 0.0f, 0.4f); d.bump = 0.25f; d.clearCoat = 1f; d.coatSmooth = 0.7f; d.detail = 0f; d.detailTiling = 22f; l.Add(d);
            d = D("PC_AnodizedBlue", "MAT_Plastic_Blue", true, 0.85f, 0.58f); d.bump = 0.6f; l.Add(d);
            d = D("PC_AnodizedBlack", "MAT_Alum_Black", true, 0.9f, 0.5f); d.bump = 0f; l.Add(d);
            d = D("PC_Motor", "BAKE_PREVIEW.007", true, 0.8f, 0.55f); l.Add(d);
            d = D("PC_Prop", "MAT_Plastic_Black", true, 0.0f, 0.36f); d.translucent = true; l.Add(d);
            d = D("PC_PlasticBlack", "MAT_Plastic_Black", true, 0.0f, 0.4f); l.Add(d);
            d = D("PC_Rubber", "MAT_Rubber", true, 0.0f, 0.15f); l.Add(d);
            d = D("PC_PCB", "MAT_Electronics", true, 0.1f, 0.42f); l.Add(d);
            d = D("PC_Steel", "MAT_Steel", true, 1.0f, 0.7f); l.Add(d);
            d = D("PC_ScrewZinc", "GRAY63", false, 1.0f, 0.62f); d.color = new Color(0.80f, 0.82f, 0.84f); l.Add(d);
            d = D("PC_NutZinc", "GRAY63", false, 1.0f, 0.5f); d.color = new Color(0.70f, 0.72f, 0.74f); l.Add(d);
            d = D("PC_Nylon", "GRAY63", false, 0.0f, 0.3f); d.color = new Color(0.86f, 0.86f, 0.82f); l.Add(d);
            // Resto de BAKE_PREVIEW (carcasas, bateria, GPS, radio): mismo atlas, metal atenuado.
            foreach (string n in new[] { "BAKE_PREVIEW.002", "BAKE_PREVIEW.004", "BAKE_PREVIEW.005", "BAKE_PREVIEW.006", "BAKE_PREVIEW.008" })
            {
                d = D("PC_" + n.Replace("BAKE_PREVIEW", "Housing"), n, true, -1f, -1f); l.Add(d);
            }
            return l;
        }

        private static void BuildMaterials()
        {
            Shader shader = Shader.Find("TwinSight/DroneLitPC");
            var weave = AssetDatabase.LoadAssetAtPath<Texture2D>(WeavePath);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_roughness.png");
            foreach (Def d in Defs())
            {
                string path = MatFolder + "/" + d.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                var src = AssetDatabase.LoadAssetAtPath<Material>(SourceMatFolder + "/" + d.source + ".mat");
                if (src != null && d.atlas)
                {
                    CopyTex(src, mat, "_BaseMap"); CopyTex(src, mat, "_BumpMap");
                    CopyTex(src, mat, "_CurvatureMap"); CopyTex(src, mat, "_OcclusionMap");
                    mat.SetFloat("_CurvatureStrength", src.GetFloat("_CurvatureStrength"));
                    mat.SetFloat("_CurvatureCavityStrength", src.GetFloat("_CurvatureCavityStrength"));
                }
                else
                {
                    mat.SetTexture("_BaseMap", null);
                    mat.SetFloat("_CurvatureStrength", 0f); mat.SetFloat("_CurvatureCavityStrength", 0f);
                    mat.SetTexture("_CurvatureMap", null); mat.SetTexture("_OcclusionMap", null);
                    mat.SetTexture("_BumpMap", null);
                }
                float metallic = d.metallic, smooth = d.smooth;
                if (metallic < 0f && src != null)
                {
                    metallic = Mathf.Min(0.3f, src.GetFloat("_Metallic") * 0.4f);
                    smooth = Mathf.Clamp(src.GetFloat("_Smoothness"), 0.25f, 0.7f);
                }
                mat.SetColor("_BaseColor", d.color);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smooth);
                mat.SetFloat("_BumpScale", d.bump);
                mat.SetFloat("_OcclusionStrength", 1f);

                SetKw(mat, "_CLEARCOAT", "_UseClearCoat", d.clearCoat > 0f);
                mat.SetFloat("_ClearCoat", d.clearCoat); mat.SetFloat("_ClearCoatSmoothness", d.coatSmooth);
                bool det = d.detail > 0f && weave != null;
                SetKw(mat, "_PC_DETAIL", "_UseDetail", det);
                if (det) { mat.SetTexture("_DetailNormalMap", weave); mat.SetFloat("_DetailNormalScale", d.detail); mat.SetFloat("_DetailTiling", d.detailTiling); }
                SetKw(mat, "_PC_TRANSLUCENT", "_UseTranslucent", d.translucent);
                SetKw(mat, "_PC_ROUGHNESSMAP", "_UseRoughnessMap", d.rough && rough != null);
                if (d.rough && rough != null) mat.SetTexture("_RoughnessMap", rough);
                SetKw(mat, "_PC_EMISSION", "_UseEmission", false);
                SetKw(mat, "_PC_METALLICMAP", "_UseMetallicMap", false);
                EditorUtility.SetDirty(mat);
            }
        }

        private static void CopyTex(Material src, Material dst, string prop)
        {
            if (src.HasProperty(prop)) dst.SetTexture(prop, src.GetTexture(prop));
        }

        private static void SetKw(Material m, string keyword, string toggle, bool on)
        {
            m.SetFloat(toggle, on ? 1f : 0f);
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }

        // ── SSAO (G9) ──
        public static void BuildSsao()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PCGraphicsSetup.RendererPath);
            if (data == null) { Debug.LogError("[PCRealismSetup] Falta PC_Renderer."); return; }
            ScreenSpaceAmbientOcclusion f = null;
            foreach (var feat in data.rendererFeatures) if (feat is ScreenSpaceAmbientOcclusion s) f = s;
            if (f == null)
            {
                f = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                f.name = "PC_SSAO";
                AssetDatabase.AddObjectToAsset(f, data);
                var so = new SerializedObject(data);
                var feats = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                feats.arraySize++;
                feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = f;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Parametros finos para piezas pequenas (dron ~10 m, tornillos 0,15-0,7 m).
            var fo = new SerializedObject(f);
            SetS(fo, "m_Settings.AOMethod", 0);          // Interleaved Gradient (estable; BlueNoise = 1)
            SetS(fo, "m_Settings.Source", 1);            // Depth Normals
            SetS(fo, "m_Settings.AfterOpaque", false);   // false: el shader recibe _SCREEN_SPACE_OCCLUSION
            SetS(fo, "m_Settings.Downsample", false);
            SetS(fo, "m_Settings.Intensity", 1.2f);
            SetS(fo, "m_Settings.Radius", 0.22f);
            SetS(fo, "m_Settings.DirectLightingStrength", 0.2f);
            SetS(fo, "m_Settings.Falloff", 100f);
            fo.ApplyModifiedPropertiesWithoutUndo();
            f.SetActive(true);
            EditorUtility.SetDirty(f);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private static void SetS(SerializedObject so, string path, object v)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[PCRealismSetup] SSAO sin campo " + path); return; }
            if (v is int i) p.intValue = i; else if (v is float fl) p.floatValue = fl; else if (v is bool b) p.boolValue = b;
        }
    }
}
