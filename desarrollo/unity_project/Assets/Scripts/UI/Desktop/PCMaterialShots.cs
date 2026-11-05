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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mat-probe") >= 0)
            {
                var tp = Find("TOP-PLATE-X500-V5_001_low");
                var rr = tp != null ? tp.GetComponent<Renderer>() : null;
                if (rr != null)
                {
                    var tex0 = rr.sharedMaterial.GetTexture("_BaseMap");
                    Debug.Log($"[PCMaterialShots] top plate baseMap={(tex0 != null ? tex0.name + " " + tex0.width + "x" + tex0.height + " mips=" + (tex0 as Texture2D)?.mipmapCount + " fmt=" + (tex0 as Texture2D)?.format : "null")} mesh={rr.GetComponent<MeshFilter>()?.sharedMesh?.name} uvOK={rr.GetComponent<MeshFilter>()?.sharedMesh?.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)} tan={rr.GetComponent<MeshFilter>()?.sharedMesh?.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent)} static={rr.gameObject.isStatic} instancing={rr.sharedMaterial.enableInstancing}");
                    rr.enabled = false;
                    yield return Shot("01_probe_topplate_off");
                    rr.enabled = true;
                    var mi = rr.material;
                    mi.SetColor("_BaseColor", Color.black);
                    yield return Shot("01_probe_instance_black");
                    mi.SetColor("_BaseColor", Color.white);
                    mi.SetFloat("_Metallic", 0f); mi.SetFloat("_Smoothness", 0.05f); mi.DisableKeyword("_CLEARCOAT"); mi.DisableKeyword("_PC_ANISO"); mi.DisableKeyword("_PC_ALBEDOBUMP");
                    yield return Shot("01_probe_instance_plain");
                }
            }
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
