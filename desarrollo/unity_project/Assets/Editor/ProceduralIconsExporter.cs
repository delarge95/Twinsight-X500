using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using WebGL.UI.ProceduralIcons;

namespace WebGL.Editor
{
    /// <summary>
    /// Editor utility to discover, simulate interaction states,
    /// and export procedural icons as structured SVG files for Rest, Hover, and Pressed states.
    /// </summary>
    public class ProceduralIconsExporter
    {
        [MenuItem("Tools/UI/Export Procedural Icons to SVG")]
        public static void ExportIconsToSvg()
        {
            string defaultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "Build", "UI_SVG_Exports"));
            string selectedPath = EditorUtility.SaveFolderPanel("Export Procedural Icons to SVG", defaultPath, "UI_SVG_Exports");

            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            if (!Directory.Exists(selectedPath))
            {
                Directory.CreateDirectory(selectedPath);
            }

            var iconTypes = Assembly.GetAssembly(typeof(ProceduralIconBase))
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ProceduralIconBase)))
                .ToList();

            if (iconTypes.Count == 0)
            {
                EditorUtility.DisplayDialog("Export Icons", "No se encontraron clases que hereden de ProceduralIconBase.", "OK");
                return;
            }

            int exportedCount = 0;
            float canvasWidth = 256f;
            float canvasHeight = 256f;

            // Reflect methods from ProceduralIconBase/hijos
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            foreach (var type in iconTypes)
            {
                try
                {
                    var drawMethod = type.GetMethod("DrawIconPath", flags);
                    if (drawMethod == null)
                    {
                        drawMethod = typeof(ProceduralIconBase).GetMethod("DrawIconPath", flags);
                    }

                    if (drawMethod == null)
                    {
                        Debug.LogWarning($"[ProceduralIconsExporter] No se encontró DrawIconPath en {type.Name}");
                        continue;
                    }

                    var hoverMethod = type.GetMethod("OnHoverEnter", flags);
                    var pressedMethod = type.GetMethod("OnPressed", flags);
                    
                    var physicsMethod = type.GetMethod("UpdateCustomPhysics", flags);
                    if (physicsMethod == null)
                    {
                        physicsMethod = typeof(ProceduralIconBase).GetMethod("UpdateCustomPhysics", flags);
                    }

                    // --- 1. ESTADO REST (REPOSO) ---
                    var restInstance = Activator.CreateInstance(type) as ProceduralIconBase;
                    if (restInstance != null)
                    {
                        var exporter = new SvgPainterExporter(canvasWidth, canvasHeight);
                        drawMethod.Invoke(restInstance, new object[] { exporter, canvasWidth, canvasHeight });
                        File.WriteAllText(Path.Combine(selectedPath, $"{type.Name}_01_Rest.svg"), exporter.GetSvgString(), System.Text.Encoding.UTF8);
                        exportedCount++;
                    }

                    // --- 2. ESTADO HOVER (ENFOQUE) ---
                    var hoverInstance = Activator.CreateInstance(type) as ProceduralIconBase;
                    if (hoverInstance != null && hoverMethod != null)
                    {
                        // Simulate hover trigger
                        hoverMethod.Invoke(hoverInstance, null);
                        
                        // Set the field isHovered = true using reflection so DrawIconPath logic matches
                        var isHoveredField = typeof(ProceduralIconBase).GetField("isHovered", flags);
                        if (isHoveredField != null)
                        {
                            isHoveredField.SetValue(hoverInstance, true);
                        }

                        // Run physics simulation for 60 frames (1 second at 60fps) to let springs settle
                        if (physicsMethod != null)
                        {
                            for (int i = 0; i < 60; i++)
                            {
                                physicsMethod.Invoke(hoverInstance, new object[] { 0.016f });
                            }
                        }

                        var exporter = new SvgPainterExporter(canvasWidth, canvasHeight);
                        drawMethod.Invoke(hoverInstance, new object[] { exporter, canvasWidth, canvasHeight });
                        File.WriteAllText(Path.Combine(selectedPath, $"{type.Name}_02_Hover.svg"), exporter.GetSvgString(), System.Text.Encoding.UTF8);
                        exportedCount++;
                    }

                    // --- 3. ESTADO PRESSED (ACCIÓN EN PROGRESO) ---
                    var pressedInstance = Activator.CreateInstance(type) as ProceduralIconBase;
                    if (pressedInstance != null && hoverMethod != null && pressedMethod != null)
                    {
                        // Hover first
                        hoverMethod.Invoke(pressedInstance, null);
                        var isHoveredField = typeof(ProceduralIconBase).GetField("isHovered", flags);
                        if (isHoveredField != null)
                        {
                            isHoveredField.SetValue(pressedInstance, true);
                        }

                        if (physicsMethod != null)
                        {
                            for (int i = 0; i < 60; i++)
                            {
                                physicsMethod.Invoke(pressedInstance, new object[] { 0.016f });
                            }
                        }

                        // Then press
                        pressedMethod.Invoke(pressedInstance, null);
                        var isPressedField = typeof(ProceduralIconBase).GetField("isPressed", flags);
                        if (isPressedField != null)
                        {
                            isPressedField.SetValue(pressedInstance, true);
                        }

                        // Run physics simulation for 12 frames (approx 200ms) to freeze the action at its peak
                        // (e.g. scanner sweep in the middle, pin slammed, explosion spread maximum)
                        if (physicsMethod != null)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                physicsMethod.Invoke(pressedInstance, new object[] { 0.016f });
                            }
                        }

                        var exporter = new SvgPainterExporter(canvasWidth, canvasHeight);
                        drawMethod.Invoke(pressedInstance, new object[] { exporter, canvasWidth, canvasHeight });
                        File.WriteAllText(Path.Combine(selectedPath, $"{type.Name}_03_Pressed.svg"), exporter.GetSvgString(), System.Text.Encoding.UTF8);
                        exportedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ProceduralIconsExporter] Error al exportar {type.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            AssetDatabase.Refresh();

            string message = $"Se exportaron exitosamente {exportedCount} archivos SVG (3 estados clave por cada uno de los {iconTypes.Count} iconos) en:\n{selectedPath}";
            Debug.Log($"[ProceduralIconsExporter] {message}");
            EditorUtility.DisplayDialog("Export SVG Completed", message, "OK");
        }
    }
}
