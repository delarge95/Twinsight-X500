#if UNITY_STANDALONE
using UnityEngine.UIElements;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>PC convenience wrapper over the shared OutlineIcon: fixed pixel size (16 / 20 / 24 / 40).</summary>
    internal sealed class DesktopVectorIcon : OutlineIcon
    {
        public DesktopVectorIcon(string shape, float size = 16f)
        {
            Shape = shape;
            style.width = size;
            style.height = size;
            style.minWidth = size;
            style.minHeight = size;
        }
    }
}
#endif
