#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Entorno de estudio procedimental SOLO PARA PC (UNITY_STANDALONE) - fase 1, G3.
    ///
    /// Problema: el modo de reflexion por defecto de la escena es Custom con cubemap nulo y no hay
    /// Reflection Probes, asi que metales y fibra de carbono salen casi negros.
    ///
    /// Solucion sin descargar nada: unos paneles emisivos tipo softbox (HDR, intensidad &gt; 1) en una
    /// capa que la camara principal NO renderiza ("PCReflectionOnly", capa 31) pero si un
    /// Reflection Probe (Realtime, On-Demand, via script) centrado en el dron. El fondo del probe
    /// es el color ambiente del preset actual (el skybox degradado usa coordenadas de pantalla y
    /// no sirve para un cubemap). El probe se vuelve a renderizar cuando EnvironmentController
    /// cambia de preset, rotacion o intensidad de luz, o fondo, y cuando cambia el tier de calidad.
    ///
    /// Se crea por codigo (RenderQualityController.Bootstrap): no hay nada en la escena ni en los
    /// assets de WebGL, y en WebGL este archivo compila vacio.
    /// </summary>
    public class PCStudioReflections : MonoBehaviour
    {
        public const string LayerName = "PCReflectionOnly";
        private const int FallbackLayer = 31;

        private struct PanelDef
        {
            public string name;
            public float yaw;        // grados respecto a la direccion de la luz principal (0 = hacia la luz)
            public float elevation;  // grados sobre el horizonte
            public float widthDeg;   // tamano angular
            public float heightDeg;
            public float intensity;  // multiplicador HDR
            public Color tint;       // 0 = usa el color de la luz principal
            public bool useKeyColor;
        }

        private static readonly PanelDef[] Defs =
        {
            new PanelDef { name = "Key",     yaw =    0f, elevation = 38f, widthDeg = 60f,  heightDeg = 45f, intensity = 3.0f, useKeyColor = true },
            new PanelDef { name = "FillStrip", yaw = -105f, elevation = 12f, widthDeg = 14f, heightDeg = 70f, intensity = 3.6f, tint = new Color(0.74f, 0.85f, 1f) },
            new PanelDef { name = "RimStrip", yaw =  160f, elevation = 18f, widthDeg = 12f, heightDeg = 65f, intensity = 4.5f, tint = new Color(0.90f, 0.95f, 1f) },
            new PanelDef { name = "Top",     yaw =    0f, elevation = 82f, widthDeg = 70f,  heightDeg = 70f, intensity = 0.8f, tint = new Color(1f, 0.96f, 0.90f) },
            new PanelDef { name = "Horizon", yaw =   45f, elevation =  4f, widthDeg = 140f, heightDeg = 4f,  intensity = 1.8f, tint = new Color(0.95f, 0.97f, 1f) },
            new PanelDef { name = "Floor",   yaw =    0f, elevation = -55f, widthDeg = 140f, heightDeg = 90f, intensity = 0.25f, tint = new Color(0.62f, 0.72f, 0.90f) },
        };

        private static PCStudioReflections _instance;
        public static PCStudioReflections Instance => _instance;

        private ReflectionProbe _probe;
        private Transform _panelRoot;
        private Renderer[] _panelRenderers;
        private Material[] _panelMaterials;
        private EnvironmentController _env;
        private Light _keyLight;
        private int _layer = FallbackLayer;
        private float _refreshAt = -1f;
        private float _nextEnvSearch;
        private Vector3 _center;
        private float _radius = 5f;
        private bool _radiusFromDrone;
        private int _renders;

        public int RenderCount => _renders;
        public string ShaderUsed { get; private set; }

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;

            var go = new GameObject("PC Studio Reflections");
            _instance = go.AddComponent<PCStudioReflections>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            int named = LayerMask.NameToLayer(LayerName);
            _layer = named >= 0 ? named : FallbackLayer;

            BuildProbe();
            BuildPanels();
            RenderQualityController.OnTierChanged += OnTierChanged;
            ScheduleRefresh(0.6f);
        }

        private void OnDestroy()
        {
            RenderQualityController.OnTierChanged -= OnTierChanged;
            if (_env != null) _env.OnEnvironmentChanged -= OnEnvironmentChanged;
            if (_instance == this) _instance = null;
            if (_panelMaterials != null)
            {
                for (int i = 0; i < _panelMaterials.Length; i++)
                {
                    if (_panelMaterials[i] != null) Destroy(_panelMaterials[i]);
                }
            }
        }

        private void OnTierChanged(QualityTier tier)
        {
            ScheduleRefresh(0.1f);
        }

        private void OnEnvironmentChanged()
        {
            ScheduleRefresh(0.15f);
        }

        /// <summary>Pide un re-render del probe (con un pequeno retardo para agrupar cambios).</summary>
        public void ScheduleRefresh(float delay = 0.1f)
        {
            _refreshAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        private void Update()
        {
            if (_env == null && Time.unscaledTime >= _nextEnvSearch)
            {
                _nextEnvSearch = Time.unscaledTime + 0.5f;
                _env = FindAnyObjectByType<EnvironmentController>();
                if (_env != null)
                {
                    _env.OnEnvironmentChanged += OnEnvironmentChanged;
                    ScheduleRefresh(0.7f);
                }
            }

            if (_refreshAt >= 0f && Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = -1f;
                Refresh();
            }
        }

        private void BuildProbe()
        {
            var probeGo = new GameObject("Studio Reflection Probe");
            probeGo.transform.SetParent(transform, false);
            _probe = probeGo.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            _probe.hdr = true;
            _probe.boxProjection = false;
            _probe.intensity = 1f;
            _probe.importance = 1;
            _probe.blendDistance = 0f;
            _probe.nearClipPlane = 0.3f;
            _probe.farClipPlane = 600f;
            _probe.shadowDistance = 0f;
            _probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            _probe.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
            _probe.cullingMask = 1 << _layer;
            _probe.resolution = 256;
            _probe.size = new Vector3(400f, 400f, 400f);
        }

        private Shader FindPanelShader()
        {
            string[] candidates =
            {
                "Universal Render Pipeline/Unlit",
                "Sprites/Default",
                "Unlit/Color",
                "Hidden/Internal-Colored"
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                Shader s = Shader.Find(candidates[i]);
                if (s != null)
                {
                    ShaderUsed = candidates[i];
                    return s;
                }
            }
            return null;
        }

        private void BuildPanels()
        {
            _panelRoot = new GameObject("Studio Panels (reflection only)").transform;
            _panelRoot.SetParent(transform, false);
            _panelRoot.gameObject.layer = _layer;

            Shader shader = FindPanelShader();
            if (shader == null)
            {
                Debug.LogWarning("[PCStudioReflections] No se encontro ningun shader sin iluminacion: los paneles no se crean.");
                _panelRenderers = new Renderer[0];
                _panelMaterials = new Material[0];
                return;
            }

            _panelRenderers = new Renderer[Defs.Length];
            _panelMaterials = new Material[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Panel_" + Defs[i].name;
                var collider = quad.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                quad.layer = _layer;
                quad.transform.SetParent(_panelRoot, false);

                var mat = new Material(shader) { name = "PCReflectionPanel_" + Defs[i].name };
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // doble cara
                mat.renderQueue = 2000;

                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.allowOcclusionWhenDynamic = false;

                _panelRenderers[i] = r;
                _panelMaterials[i] = mat;
            }
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_RendererColor")) mat.SetColor("_RendererColor", Color.white);
        }

        private void ResolveDroneBounds()
        {
            GameObject drone = GameObject.Find("x500v2_Drone");
            if (drone == null) return;

            Renderer[] renderers = drone.GetComponentsInChildren<Renderer>(false);
            bool has = false;
            Bounds b = new Bounds(drone.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || r.gameObject.layer == _layer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has) return;

            _center = b.center;
            if (!_radiusFromDrone)
            {
                // El radio solo se fija una vez (con el dron ensamblado): evita que la vista
                // explosionada mueva los paneles.
                _radius = Mathf.Clamp(b.extents.magnitude, 1f, 30f);
                _radiusFromDrone = true;
            }
        }

        private static Vector3 Dir(float yawDeg, float elevDeg)
        {
            float y = yawDeg * Mathf.Deg2Rad;
            float e = elevDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(y) * Mathf.Cos(e));
        }

        /// <summary>Recoloca los paneles segun el entorno actual y re-renderiza el probe.</summary>
        public void Refresh()
        {
            if (_probe == null) return;

            ResolveDroneBounds();

            if (_keyLight == null && _env != null) _keyLight = _env.KeyLight;
            Color keyColor = Color.white;
            float keyIntensity = 2.4f;
            float keyPosYaw = 36f + 180f;
            if (_keyLight != null)
            {
                keyColor = _keyLight.color;
                keyIntensity = _keyLight.intensity;
                // La luz viaja a lo largo de su forward: el softbox esta en el lado opuesto.
                Vector3 toLight = -_keyLight.transform.forward;
                keyPosYaw = Mathf.Atan2(toLight.x, toLight.z) * Mathf.Rad2Deg;
            }
            float exposure = Mathf.Clamp(keyIntensity / 2.4f, 0.45f, 1.6f);

            float distance = Mathf.Max(_radius * 4f, 14f);
            for (int i = 0; i < Defs.Length && _panelRenderers != null && i < _panelRenderers.Length; i++)
            {
                PanelDef d = Defs[i];
                Transform t = _panelRenderers[i].transform;
                Vector3 dir = Dir(keyPosYaw + d.yaw, d.elevation);
                t.position = _center + dir * distance;
                t.rotation = Quaternion.LookRotation(_center - t.position, Mathf.Abs(d.elevation) > 70f ? Vector3.forward : Vector3.up);
                float w = 2f * distance * Mathf.Tan(Mathf.Clamp(d.widthDeg, 1f, 160f) * 0.5f * Mathf.Deg2Rad);
                float h = 2f * distance * Mathf.Tan(Mathf.Clamp(d.heightDeg, 1f, 160f) * 0.5f * Mathf.Deg2Rad);
                t.localScale = new Vector3(w, h, 1f);

                Color baseColor = d.useKeyColor ? Color.Lerp(Color.white, keyColor, 0.7f) : Color.Lerp(d.tint, keyColor, 0.18f);
                Color hdr = baseColor * (d.intensity * exposure);
                hdr.a = 1f;
                SetMaterialColor(_panelMaterials[i], hdr);
                _panelRenderers[i].gameObject.layer = _layer;
            }

            _probe.transform.position = _center;
            _probe.size = new Vector3(distance * 4f, distance * 4f, distance * 4f) * 3f;
            _probe.cullingMask = 1 << _layer;
            _probe.resolution = ResolutionForTier(RenderQualityController.ResolvedTier);

            // Fondo del probe = color ambiente actual (equator) atenuado: sustituye al skybox degradado.
            Color bg = RenderSettings.ambientEquatorColor * 0.55f;
            bg.a = 1f;
            _probe.backgroundColor = bg;

            // La camara principal no renderiza los paneles.
            Camera main = Camera.main;
            if (main != null) main.cullingMask &= ~(1 << _layer);

            _probe.RenderProbe();
            _renders++;

            // Cualquier superficie fuera del probe (no deberia haber) usa el mismo cubemap.
            Texture tex = _probe.realtimeTexture != null ? (Texture)_probe.realtimeTexture : _probe.texture;
            if (tex != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = tex;
                RenderSettings.reflectionIntensity = 1f;
            }
        }

        private static int ResolutionForTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return 64;
                case QualityTier.Medium: return 128;
                case QualityTier.High: return 256;
                default: return 512;
            }
        }
    }
}
#endif
