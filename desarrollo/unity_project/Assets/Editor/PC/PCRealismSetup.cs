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
        public const string BrushedPath = TexFolder + "/pc_brushed_n.png";
        public const string SkyMatPath = ResFolder + "/PC_HDRISky.mat";
        private const string SourceMatFolder = "Assets/Models/Materials";

        [MenuItem("TwinSight/PC/Build PC Realism (fase 3A)")]
        public static void BuildAll()
        {
            EnsureFolders();
            ImportHdri();
            GenerateWeave();
            GenerateBrushed();
            BuildSkyMaterial();
            BuildMaterials();
            BuildSsao();
            BuildViewModeMaterials();
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

        // ── Mecanizado / cepillado fino (ranuras paralelas, tileable) para el aluminio anodizado y la campana del motor ──
        private static void GenerateBrushed()
        {
            const int N = 256;
            var rnd = new System.Random(1234);
            float[] col = new float[N];
            // ruido 1D tileable: suma de 3 octavas de valores aleatorios interpolados
            foreach (int cells in new[] { 16, 48, 128 })
            {
                float[] v = new float[cells];
                for (int i = 0; i < cells; i++) v[i] = (float)rnd.NextDouble() - 0.5f;
                float amp = cells == 16 ? 0.5f : (cells == 48 ? 0.35f : 0.25f);
                for (int x = 0; x < N; x++)
                {
                    float f = x * cells / (float)N; int i0 = (int)f % cells, i1 = (i0 + 1) % cells; float t = f - Mathf.Floor(f); t = t * t * (3f - 2f * t);
                    col[x] += Mathf.Lerp(v[i0], v[i1], t) * amp;
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float slope = (col[(x + 1) % N] - col[(x + N - 1) % N]) * 3.0f;
                    Vector3 n = new Vector3(-slope, 0f, 1f).normalized;
                    px[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)128, (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(BrushedPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BrushedPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(BrushedPath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 8; imp.maxTextureSize = 256;
            var web = imp.GetPlatformTextureSettings("WebGL");
            web.overridden = true; web.maxTextureSize = 32;
            imp.SetPlatformTextureSettings(web);
            imp.SaveAndReimport();
        }

        // Fase 3B (G11): materiales de modos de vista de PC (ViewModeManager los carga de Resources/PC/Materials).
        private static void BuildViewModeMaterials()
        {
            foreach (var pair in new[] { ("PC_XRay", "TwinSight/XRayPC"), ("PC_Wireframe", "TwinSight/WireframePC") })
            {
                string path = MatFolder + "/" + pair.Item1 + ".mat";
                Shader sh = Shader.Find(pair.Item2);
                if (sh == null) { Debug.LogWarning("[PCRealismSetup] Falta el shader " + pair.Item2); continue; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
                else mat.shader = sh;
                EditorUtility.SetDirty(mat);
            }
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
            public float roughStrength, roughScale;
            public bool hex;                   // tornilleria: Hex_AO / Hex_Normal_Atlas (UV propia de la cabeza)
            public float curvEdge, curvCavity; // -1 = copiar de la fuente
            public float albedoBump, albedoStep, albedoSpec;
            public float albedoMax, smoothMax, specAA, specDark, finalDark;
            public float aniso, anisoShin;
            public Color anisoColor;
            public float occlusion;
            public bool brushed;
        }

        private static Def D(string name, string source, bool atlas, float metallic, float smooth)
        {
            return new Def { name = name, source = source, atlas = atlas, color = Color.white, metallic = metallic, smooth = smooth, bump = 1f, curvEdge = -1f, curvCavity = -1f, albedoStep = 1f, anisoShin = 90f, anisoColor = new Color(0.7f, 0.75f, 0.9f), roughStrength = 1f, roughScale = 1f, occlusion = 1f, albedoMax = 1f, smoothMax = 1f, specDark = 1f, finalDark = 1f };
        }

        private static List<Def> Defs()
        {
            var l = new List<Def>();
            Def d;
            d = D("PC_CarbonPlate", "carbon_fiber_01", true, 0.0f, 0.30f); d.bump = 0.35f; d.clearCoat = 0.85f; d.coatSmooth = 0.62f; d.albedoBump = 1.0f; d.albedoStep = 1.5f; d.albedoSpec = 0.3f; d.aniso = 0.07f; d.anisoShin = 150f; d.albedoMax = 0.045f; d.smoothMax = 0.45f; d.specAA = 2.0f; d.specDark = 0.45f; d.finalDark = 0.5f; l.Add(d);
            d = D("PC_CarbonTube", "MAT_Plastic_Black", true, 0.0f, 0.30f); d.bump = 0.35f; d.clearCoat = 0.85f; d.coatSmooth = 0.62f; d.albedoBump = 1.0f; d.albedoStep = 1.5f; d.albedoSpec = 0.3f; d.aniso = 0.07f; d.anisoShin = 150f; d.albedoMax = 0.045f; d.smoothMax = 0.45f; d.specAA = 2.0f; d.specDark = 0.45f; d.finalDark = 0.75f; l.Add(d);
            d = D("PC_AnodizedBlue", "MAT_Plastic_Blue", true, 0.85f, 0.58f); d.bump = 0.6f; d.brushed = true; d.detail = 0.15f; d.detailTiling = 14f; d.rough = true; d.roughStrength = 0.25f; l.Add(d);
            d = D("PC_AnodizedBlack", "MAT_Alum_Black", true, 0.9f, 0.5f); d.bump = 0f; d.brushed = true; d.detail = 0.1f; d.detailTiling = 14f; d.rough = true; d.roughStrength = 0.25f; l.Add(d);
            d = D("PC_Motor", "BAKE_PREVIEW.007", true, 0.9f, 0.5f); d.brushed = true; d.detail = 0.12f; d.detailTiling = 14f; d.rough = true; d.roughStrength = 0.25f; l.Add(d);
            d = D("PC_Prop", "MAT_Plastic_Black", true, 0.0f, 0.36f); d.translucent = true; l.Add(d);
            d = D("PC_PlasticBlack", "MAT_Plastic_Black", true, 0.0f, 0.4f); l.Add(d);
            d = D("PC_Rubber", "MAT_Rubber", true, 0.0f, 0.15f); l.Add(d);
            d = D("PC_PCB", "MAT_Electronics", true, 0.1f, 0.42f); l.Add(d);
            d = D("PC_Steel", "MAT_Steel", true, 1.0f, 0.7f); d.brushed = true; d.detail = 0.1f; d.detailTiling = 14f; d.rough = true; d.roughStrength = 0.2f; l.Add(d);
            // Tornilleria temporal (GRAY63): acero pavonado / oxido negro, con la cabeza hexagonal de Hex_AO + Hex_Normal_Atlas.
            d = D("PC_ScrewZinc", "GRAY63", false, 1.0f, 0.55f); d.hex = true; d.color = new Color(0.04f, 0.042f, 0.048f); d.metallic = 0.85f; d.smooth = 0.36f; d.bump = 1.6f; d.curvEdge = 0.1f; d.curvCavity = 0.3f; l.Add(d);
            d = D("PC_NutZinc", "GRAY63", false, 1.0f, 0.5f); d.hex = true; d.color = new Color(0.04f, 0.042f, 0.048f); d.metallic = 0.85f; d.smooth = 0.34f; d.bump = 1.6f; d.curvEdge = 0.1f; d.curvCavity = 0.3f; l.Add(d);
            d = D("PC_Nylon", "GRAY63", false, 0.0f, 0.18f); d.hex = true; d.color = new Color(0.86f, 0.86f, 0.82f); d.bump = 1f; d.curvEdge = 0f; d.curvCavity = 0.2f; l.Add(d);
            // Resto de BAKE_PREVIEW (carcasas, bateria, GPS, radio): mismo atlas, metal atenuado.
            foreach (string n in new[] { "BAKE_PREVIEW.002", "BAKE_PREVIEW.004", "BAKE_PREVIEW.005", "BAKE_PREVIEW.006", "BAKE_PREVIEW.008" })
            {
                d = D("PC_" + n.Replace("BAKE_PREVIEW", "Housing"), n, true, -1f, -1f);
                if (n == "BAKE_PREVIEW.008") { d.metallic = 0.7f; d.smooth = 0.45f; }   // bobinado de cobre del motor
                l.Add(d);
            }
            return l;
        }

        private static void BuildMaterials()
        {
            Shader shader = Shader.Find("TwinSight/DroneLitPC");
            var weave = AssetDatabase.LoadAssetAtPath<Texture2D>(WeavePath);
            var brushed = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushedPath);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/final_curve_roughness.png");
            foreach (Def d in Defs())
            {
                string path = MatFolder + "/" + d.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                var src = AssetDatabase.LoadAssetAtPath<Material>(SourceMatFolder + "/" + d.source + ".mat");
                if (d.hex)
                {
                    // GRAY63 original: Hex_AO (albedo/AO/curvatura) y Hex_Normal_Atlas. La 3A los puso a null
                    // (atlas=false) y de ahi se perdio el hexagono de la cabeza.
                    var hexAo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Hex_AO.png");
                    var hexN = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Hex_Normal_Atlas.png");
                    mat.SetTexture("_BaseMap", hexAo); mat.SetTexture("_OcclusionMap", hexAo);
                    mat.SetTexture("_CurvatureMap", hexAo); mat.SetTexture("_BumpMap", hexN);
                    mat.SetFloat("_CurvatureStrength", Mathf.Max(0f, d.curvEdge));
                    mat.SetFloat("_CurvatureCavityStrength", Mathf.Max(0f, d.curvCavity));
                }
                else if (src != null && d.atlas)
                {
                    CopyTex(src, mat, "_BaseMap"); CopyTex(src, mat, "_BumpMap");
                    CopyTex(src, mat, "_CurvatureMap"); CopyTex(src, mat, "_OcclusionMap");
                    mat.SetFloat("_CurvatureStrength", d.curvEdge >= 0f ? d.curvEdge : src.GetFloat("_CurvatureStrength"));
                    mat.SetFloat("_CurvatureCavityStrength", d.curvCavity >= 0f ? d.curvCavity : src.GetFloat("_CurvatureCavityStrength"));
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
                mat.SetFloat("_OcclusionStrength", d.occlusion);
                SetKw(mat, "_PC_ALBEDOBUMP", "_UseAlbedoBump", d.albedoBump > 0f);
                mat.SetFloat("_AlbedoBumpScale", d.albedoBump); mat.SetFloat("_AlbedoBumpStep", d.albedoStep); mat.SetFloat("_AlbedoBumpSpec", d.albedoSpec);
                mat.SetFloat("_AlbedoMax", d.albedoMax > 0f ? d.albedoMax : 1f); mat.SetFloat("_SmoothnessMax", d.smoothMax > 0f ? d.smoothMax : 1f); mat.SetFloat("_SpecAA", d.specAA); mat.SetFloat("_SpecDark", d.specDark > 0f ? d.specDark : 1f); mat.SetFloat("_FinalDark", d.finalDark > 0f ? d.finalDark : 1f);
                SetKw(mat, "_PC_ANISO", "_UseAniso", d.aniso > 0f);
                mat.SetFloat("_AnisoStrength", d.aniso); mat.SetFloat("_AnisoShininess", d.anisoShin); mat.SetColor("_AnisoColor", d.anisoColor);

                SetKw(mat, "_CLEARCOAT", "_UseClearCoat", d.clearCoat > 0f);
                mat.SetFloat("_ClearCoat", d.clearCoat); mat.SetFloat("_ClearCoatSmoothness", d.coatSmooth);
                Texture2D detTex = d.brushed ? brushed : weave;
                bool det = d.detail > 0f && detTex != null;
                SetKw(mat, "_PC_DETAIL", "_UseDetail", det);
                if (det) { mat.SetTexture("_DetailNormalMap", detTex); mat.SetFloat("_DetailNormalScale", d.detail); mat.SetFloat("_DetailTiling", d.detailTiling); }
                SetKw(mat, "_PC_TRANSLUCENT", "_UseTranslucent", d.translucent);
                SetKw(mat, "_PC_ROUGHNESSMAP", "_UseRoughnessMap", d.rough && rough != null);
                if (d.rough && rough != null) { mat.SetTexture("_RoughnessMap", rough); mat.SetFloat("_RoughnessMapStrength", d.roughStrength); mat.SetFloat("_RoughnessMapScale", d.roughScale); }
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
            SetS(fo, "m_Settings.AOMethod", 1);          // Interleaved Gradient (enum: BlueNoise = 0, InterleavedGradient = 1)
            SetS(fo, "m_Settings.Samples", 0);           // High (12 muestras): menos grano en los contactos
            SetS(fo, "m_Settings.NormalSamples", 2);     // High
            SetS(fo, "m_Settings.BlurQuality", 0);       // High (bilateral)
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
