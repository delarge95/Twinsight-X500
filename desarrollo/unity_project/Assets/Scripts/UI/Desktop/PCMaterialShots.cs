#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Ayuda de verificacion del pulido de materiales de PC (solo con <c>-desk-matshots &lt;carpeta&gt; &lt;antes|despues&gt;</c>,
    /// o anadiendo el componente a mano en Play). Guarda las mismas vistas siempre: placa y tubos de carbono con luz
    /// rasante, tornillos y tuerca, brazo y motor, Pixhawk y radio, vista general y vista general con Day y Sunset.
    /// </summary>
    public sealed class PCMaterialShots : MonoBehaviour
    {
        public string OutputDirectory;
        public string Suffix = "despues";
        public bool QuitWhenDone = true;
        public bool SkipStartup;
        public string Filter = "";   // lista de prefijos separados por coma (p. ej. "01,04"); vacio = todas
        private Transform _anchor;
        private Light _sun;
        private Vector3 _sunEuler;

        private void Start() { StartCoroutine(Run()); }

        private IEnumerator Shot(string name)
        {
            if (!string.IsNullOrEmpty(Filter))
            {
                bool ok = false;
                foreach (string f in Filter.Split(',')) if (name.StartsWith(f.Trim())) ok = true;
                if (!ok) yield break;
            }
            yield return new WaitForSecondsRealtime(0.7f);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + "_" + Suffix + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        private static Transform Find(string n)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == n) return t;
            return null;
        }

        private void Sun(float pitch, float yaw)
        {
            if (_sun == null) return;
            _sun.transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            EnvironmentController.Instance.SetLightRotation(yaw);
        }

        private void SunRestore()
        {
            if (_sun == null) return;
            _sun.transform.eulerAngles = _sunEuler;
            EnvironmentController.Instance.SetLightRotation(_sunEuler.y);
        }

        private IEnumerator FramePoint(Vector3 p, float az, float el, float dist)
        {
            // Camara directa (sin OrbitCameraController): posicion reproducible y sin limite de zoom.
            var cam = Camera.main;
            float ca = Mathf.Cos(el * Mathf.Deg2Rad), sa = Mathf.Sin(el * Mathf.Deg2Rad);
            Vector3 dir = new Vector3(ca * Mathf.Sin(az * Mathf.Deg2Rad), sa, ca * Mathf.Cos(az * Mathf.Deg2Rad));
            cam.transform.position = p + dir * dist;
            cam.transform.LookAt(p, Vector3.up);
            yield return null;
        }

        private IEnumerator Frame(Transform t, float az, float el, float dist)
        {
            Vector3 c = t.position;
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); c = b.center; }
            return FramePoint(c, az, el, dist);
        }

        private IEnumerator ProbeTop()
        {
                var tp = Find("TOP-PLATE-X500-V5_001_low");
                var rr = tp != null ? tp.GetComponent<Renderer>() : null;
                if (rr != null)
                {
                    var mesh = rr.GetComponent<MeshFilter>().sharedMesh;
                    Vector3 ls = rr.transform.lossyScale;
                    var ns = mesh.normals; Vector3 avg = Vector3.zero; foreach (var n in ns) avg += n; avg /= Mathf.Max(1, ns.Length);
                    var tr = mesh.triangles; var vs = mesh.vertices; int agree = 0, total = 0;
                    for (int i = 0; i + 2 < tr.Length && total < 3000; i += 3, total++)
                    {
                        Vector3 fn = Vector3.Cross(vs[tr[i + 1]] - vs[tr[i]], vs[tr[i + 2]] - vs[tr[i]]);
                        Vector3 vn = ns[tr[i]] + ns[tr[i + 1]] + ns[tr[i + 2]];
                        if (Vector3.Dot(fn, vn) > 0) agree++;
                    }
                    Debug.Log($"[PCMaterialShots] TOP scale={ls} avgNormalLocal={avg} avgNormalWorld={rr.transform.TransformDirection(avg)} normalsAgreeWithWinding={agree}/{total} submeshes={mesh.subMeshCount} mats={rr.sharedMaterials.Length} verts={mesh.vertexCount} lightmap={rr.lightmapIndex} shadows={rr.shadowCastingMode}");
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        Debug.Log($"[PCMaterialShots] light {l.name} {l.type} int={l.intensity:F2} range={l.range:F1} pos={l.transform.position} fwd={l.transform.forward} enabled={l.enabled}");
                    foreach (var o in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (o == rr || !o.enabled || !o.gameObject.activeInHierarchy || !(o is MeshRenderer)) continue;
                        if (o.bounds.Intersects(rr.bounds) && o.bounds.size.x > 0.5f) Debug.Log($"[PCMaterialShots] OVERLAP {o.name} size={o.bounds.size} mat={(o.sharedMaterial != null ? o.sharedMaterial.name : "null")}");
                    }
                    var saved = rr.sharedMaterials;
                    var mi = new Material(saved[0]);
                    var cases = new (string, Action<Material>)[]
                    {
                        ("probeA_noclearcoat", m => m.DisableKeyword("_CLEARCOAT")),
                        ("probeB_noclearcoat_rough", m => { m.DisableKeyword("_CLEARCOAT"); m.SetFloat("_Smoothness", 0f); m.SetFloat("_SmoothnessMax", 0f); m.SetFloat("_Metallic", 0f); }),
                        ("probeC_black_flat", m => { m.DisableKeyword("_CLEARCOAT"); m.SetColor("_BaseColor", Color.black); m.SetFloat("_Smoothness", 0f); m.SetFloat("_SmoothnessMax", 0f); m.SetFloat("_SpecDark", 0f); }),
                    };
                    foreach (var (nm, apply) in cases)
                    {
                        mi = new Material(saved[0]); apply(mi);
                        rr.sharedMaterials = new[] { mi };
                        yield return new WaitForSecondsRealtime(0.6f);
                        yield return new WaitForEndOfFrame();
                        var cur = rr.sharedMaterial;
                        Debug.Log($"[PCMaterialShots] {nm}: sharedMaterial={cur.name} same={ReferenceEquals(cur, mi)} shader={cur.shader.name} clearcoat={cur.IsKeywordEnabled("_CLEARCOAT")} smooth={cur.GetFloat("_Smoothness")} base={cur.GetColor("_BaseColor")}");
                        yield return Shot("17_" + nm);
                    }
                    var red = new Material(Shader.Find("Universal Render Pipeline/Lit")); red.SetColor("_BaseColor", Color.red);
                    rr.sharedMaterials = new[] { red };
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return new WaitForEndOfFrame();
                    Debug.Log($"[PCMaterialShots] red: sharedMaterial={rr.sharedMaterial.name} shader={rr.sharedMaterial.shader.name}");
                    yield return Shot("17_probeD_red_lit");
                    rr.sharedMaterials = saved;
                }
                    }

        private IEnumerator ProbeLights()
        {
            var env = EnvironmentController.Instance;
            if (env != null) env.enabled = false;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var en = new bool[lights.Length]; for (int i = 0; i < lights.Length; i++) en[i] = lights[i].enabled;
            float ai = UnityEngine.RenderSettings.ambientIntensity, ri = UnityEngine.RenderSettings.reflectionIntensity;
            var amode = UnityEngine.RenderSettings.ambientMode;
            // S1: todo apagado
            foreach (var l in lights) l.enabled = false;
            UnityEngine.RenderSettings.ambientIntensity = 0f; UnityEngine.RenderSettings.reflectionIntensity = 0f;
            yield return Shot("17_L1_todo_apagado");
            // S2: solo ambiente
            UnityEngine.RenderSettings.ambientIntensity = ai;
            yield return Shot("17_L2_solo_ambiente");
            UnityEngine.RenderSettings.ambientIntensity = 0f;
            // S3: solo reflexion
            UnityEngine.RenderSettings.reflectionIntensity = ri;
            yield return Shot("17_L3_solo_reflexion");
            UnityEngine.RenderSettings.reflectionIntensity = 0f;
            // S4..: luces una a una
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = true;
                yield return Shot("17_L4_solo_" + lights[i].name.Replace(" ", "_"));
                lights[i].enabled = false;
            }
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = en[i];
            UnityEngine.RenderSettings.ambientIntensity = ai; UnityEngine.RenderSettings.reflectionIntensity = ri;
            if (env != null) env.enabled = true;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            if (!SkipStartup)
            {
                PlayerPrefs.SetInt("Onboarding_Seen", 1);
                yield return new WaitForSecondsRealtime(6f);
                UIManager ui = UIManager.Instance;
                ui.PrepareViewerForDeepLink();
                yield return new WaitForSecondsRealtime(3f);
                if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            }
            // Sin interfaz en las capturas: solo el dron.
            foreach (var doc in FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (doc.rootVisualElement != null) doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && (_sun == null || l.intensity > _sun.intensity)) _sun = l;
            if (_sun != null) _sunEuler = _sun.transform.eulerAngles;
            var oc = OrbitCameraController.Instance;
            oc.enabled = false;
            var env = EnvironmentController.Instance;
            Debug.Log($"[PCMaterialShots] tier={RenderQualityController.ResolvedTier} swapped={PCMaterialSwapper.SwappedCount} screen={Screen.width}x{Screen.height}");

            foreach (var rr in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!rr.name.ToUpperInvariant().Contains("PLATE")) continue;
                var names = new System.Text.StringBuilder();
                foreach (var sm in rr.sharedMaterials) names.Append(sm != null ? sm.name + "[" + sm.shader.name + "] " : "null ");
                var mpb = new MaterialPropertyBlock(); rr.GetPropertyBlock(mpb);
                Debug.Log($"[PCMaterialShots] PLATE {rr.name} type={rr.GetType().Name} mats={names} mpbEmpty={mpb.isEmpty} layer={rr.gameObject.layer} lightmap={rr.lightmapIndex}");
            }

            // a) placa y tubos de carbono en primer plano, luz rasante
            Sun(14f, 300f);
            yield return FramePoint(new Vector3(-0.8f, 1.0f, 0.1f), -20f, 38f, 1.2f);
            yield return Shot("01_carbono_placa");
            yield return Frame(Find("GUAN-CHENG.002_low"), 35f, 25f, 1.8f);
            yield return Shot("02_carbono_tubo");
            yield return Frame(Find("BOTTOM-PLATE-X500-V5_001_low"), 200f, -35f, 1.7f);
            yield return Shot("03_carbono_placa_inferior");
            SunRestore();

            // b) tornillos y tuerca
            yield return FramePoint(new Vector3(0.9f, 1.03f, 0.8f), -25f, 52f, 0.6f);
            yield return Shot("04_tornillos_cabeza");
            yield return FramePoint(new Vector3(0.94f, 0.43f, 0.68f), 150f, -50f, 0.6f);
            yield return Shot("05_tuerca");

            // c) brazo de aluminio y motor
            yield return Frame(Find("HMX5V-ZUO-DJ-MUJU.003_low"), 30f, 22f, 1.6f);
            yield return Shot("06_aluminio_motor");
            yield return Frame(Find("DJ-2216-KV880.003_low"), -35f, 15f, 1.3f);
            yield return Shot("07_motor");

            // d) Pixhawk y radio
            yield return Frame(Find("PCB-PIXHAWK6C-F1_001_low"), -20f, 62f, 1.5f);
            yield return Shot("08_pixhawk");
            yield return Frame(Find("x500v2_telemetry_radio_PROXY_low"), -60f, 20f, 1.4f);
            yield return Shot("09_radio");
            yield return Frame(Find("x500v2_battery_PROXY_low"), 30f, 25f, 2.2f);
            yield return Shot("10_bateria");
            yield return Frame(Find("JIAO-EVA.002_low"), 40f, 15f, 1.6f);
            yield return Shot("11_patas_gomas");

            // e) vista general
            yield return FramePoint(new Vector3(0f, 0.4f, 0f), 215f, 20f, 9.5f);
            yield return Shot("12_vista_general");
            yield return FramePoint(new Vector3(0f, 0.6f, 0f), 215f, 22f, 6.0f);
            yield return Shot("13_vista_media");
            yield return FramePoint(new Vector3(0f, 0.95f, 0f), 200f, 82f, 3.6f);
            yield return Shot("17_vista_cenital");
            {
                var tpl = Find("TOP-PLATE-X500-V5_001_low");
                var hs = tpl != null ? tpl.GetComponentInParent<WebGL.Core.Content.HighlightSystem>() : null;
                if (hs != null)
                {
                    hs.OnHoverEnter();
                    yield return Shot("19_hover_placa_cenital");
                    yield return FramePoint(new Vector3(0f, 0.95f, 0f), 215f, 35f, 3.2f);
                    yield return Shot("19_hover_placa_media");
                    hs.OnHoverExit();
                    yield return Shot("19_sin_hover_placa_media");
                }
                else Debug.Log("[PCMaterialShots] sin HighlightSystem en la placa superior");
            }
            yield return FramePoint(new Vector3(0f, 0.95f, 0f), 215f, 35f, 3.2f);
            yield return Shot("18_placa_superior_media");

            // f) otros entornos
            foreach (string p in new[] { "Day", "Sunset" })
            {
                env.ApplyPreset(p);
                yield return FramePoint(new Vector3(0f, 0.4f, 0f), 215f, 20f, 9.5f);
                yield return Shot("14_entorno_" + p + "_general");
                yield return FramePoint(new Vector3(-0.8f, 1.0f, 0.1f), -20f, 38f, 1.5f);
                yield return Shot("15_entorno_" + p + "_carbono");
                yield return FramePoint(new Vector3(0.9f, 1.03f, 0.8f), -25f, 52f, 0.7f);
                yield return Shot("16_entorno_" + p + "_tornillos");
            }
            env.ApplyPreset("Studio");
            Debug.Log("[PCMaterialShots] listo");
            if (QuitWhenDone) Application.Quit();
        }

        internal static void TryAttach(GameObject host)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!args[i].Equals("-desk-matshots", StringComparison.OrdinalIgnoreCase)) continue;
                var c = host.AddComponent<PCMaterialShots>();
                c.OutputDirectory = args[i + 1];
                if (i + 2 < args.Length && !args[i + 2].StartsWith("-")) c.Suffix = args[i + 2];
                return;
            }
        }
    }
}
#endif
