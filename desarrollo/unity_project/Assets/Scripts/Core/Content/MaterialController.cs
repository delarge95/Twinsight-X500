using UnityEngine;
using System.Collections.Generic;

namespace WebGL.Core.Content
{
    public class MaterialController : MonoBehaviour
    {
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor"); // URP Lit standard property
        private static readonly int LegacyColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int HighlightColorPropertyId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightIntensityPropertyId = Shader.PropertyToID("_HighlightIntensity");
        private readonly Dictionary<Renderer, Material[]> _originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();

        public Renderer[] Renderers => _renderers;

        private void Awake()
        {
            CacheRenderers();
            _propBlock = new MaterialPropertyBlock();
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void RefreshRenderers()
        {
            CacheRenderers();
        }

        public void SetColor(Color color)
        {
            SetColor(color, Mathf.Clamp01(color.a * 2.5f));
        }

        public void SetColor(Color color, float highlightIntensity)
        {
            CacheRenderers();
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            Color tint = Color.Lerp(Color.white, color, 0.5f);
            tint.a = 1.0f;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(HighlightColorPropertyId, color);
                _propBlock.SetFloat(HighlightIntensityPropertyId, Mathf.Clamp01(highlightIntensity));
                _propBlock.SetColor(ColorPropertyId, tint);
                _propBlock.SetColor(LegacyColorPropertyId, tint);
                renderer.SetPropertyBlock(_propBlock);
            }
        }

        public void ResetProperties()
        {
            CacheRenderers();

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null) continue;
                renderer.SetPropertyBlock(null);
            }
        }

        // X-Ray Support
        public void ToggleXRay(bool enable, Material xrayMat)
        {
            if (_renderers == null || _renderers.Length == 0) CacheRenderers();

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null) continue;

                if (enable)
                {
                    if (!_originalMaterialsByRenderer.ContainsKey(renderer))
                    {
                        _originalMaterialsByRenderer[renderer] = renderer.sharedMaterials;
                    }

                    Material[] replacement = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < replacement.Length; i++)
                    {
                        replacement[i] = xrayMat;
                    }

                    renderer.materials = replacement;
                }
                else if (_originalMaterialsByRenderer.TryGetValue(renderer, out Material[] originalMaterials))
                {
                    renderer.materials = originalMaterials;
                }
            }
        }
    }
}
