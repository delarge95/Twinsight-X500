using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using WebGL.Core.Content;

public class DroneSelectionExporter : EditorWindow
{
    private const string RootName = "x500v2_Drone";
    private const string OutputFolder = "Assets/../Reports/selection_exports";
    private const string LatestFileName = "latest_export.json";

    [MenuItem("Tools/Drone Tools/Selection Exporter")]
    public static void ShowWindow()
    {
        GetWindow<DroneSelectionExporter>("Selection Exporter");
    }

    public static void ExportFullHierarchyHeadless()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/MainScene_Final.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError("[DroneSelectionExporter] No se encontro x500v2_Drone en la escena.");
            EditorApplication.Exit(2);
            return;
        }

        List<ExportEntry> entries = new List<ExportEntry>();
        CollectRecursive(root.transform, entries, new HashSet<Transform>());
        WriteExport(entries, "full_hierarchy");
        Debug.Log($"[DroneSelectionExporter] Headless export completo: {entries.Count} entradas.");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Selecciona piezas en la jerarquia y pulsa 'Exportar Seleccion'.\n" +
            "2. O exporta la jerarquia completa del drone.\n" +
            "Salida: Reports/selection_exports/latest_export.json", MessageType.Info);

        if (GUILayout.Button("Exportar Seleccion", GUILayout.Height(32)))
        {
            ExportSelection();
        }

        if (GUILayout.Button("Exportar Jerarquia Completa (x500v2_Drone)", GUILayout.Height(32)))
        {
            ExportFullHierarchy();
        }

        if (GUILayout.Button("Abrir Carpeta de Salida"))
        {
            Directory.CreateDirectory(OutputFolder);
            EditorUtility.RevealInFinder(Path.Combine(OutputFolder, LatestFileName));
        }
    }

    private static void ExportSelection()
    {
        List<Transform> transforms = new List<Transform>();
        foreach (UnityEngine.Object obj in Selection.objects)
        {
            if (obj is Transform t)
            {
                transforms.Add(t);
            }
            else if (obj is GameObject go)
            {
                transforms.Add(go.transform);
            }
        }

        if (transforms.Count == 0)
        {
            EditorUtility.DisplayDialog("Selection Exporter", "No hay objetos seleccionados.", "OK");
            return;
        }

        List<ExportEntry> entries = new List<ExportEntry>();
        HashSet<Transform> visited = new HashSet<Transform>();
        foreach (Transform t in transforms)
        {
            CollectRecursive(t, entries, visited);
        }

        WriteExport(entries, "selection");
        EditorUtility.DisplayDialog("Selection Exporter",
            $"Se exportaron {entries.Count} objetos a latest_export.json", "OK");
    }

    private static void ExportFullHierarchy()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Selection Exporter", "No se encontro x500v2_Drone en la escena activa.", "OK");
            return;
        }

        List<ExportEntry> entries = new List<ExportEntry>();
        CollectRecursive(root.transform, entries, new HashSet<Transform>());
        WriteExport(entries, "full_hierarchy");
        EditorUtility.DisplayDialog("Selection Exporter",
            $"Se exportaron {entries.Count} objetos de {RootName} a latest_export.json", "OK");
    }

    private static void CollectRecursive(Transform transform, List<ExportEntry> entries, HashSet<Transform> visited)
    {
        if (transform == null || !visited.Add(transform))
        {
            return;
        }

        entries.Add(BuildEntry(transform));
        for (int i = 0; i < transform.childCount; i++)
        {
            CollectRecursive(transform.GetChild(i), entries, visited);
        }
    }

    private static ExportEntry BuildEntry(Transform transform)
    {
        ExportEntry entry = new ExportEntry();
        entry.objectName = transform.name;
        entry.path = GetHierarchyPath(transform);
        entry.parentPath = transform.parent != null ? GetHierarchyPath(transform.parent) : string.Empty;
        entry.layer = LayerMask.LayerToName(transform.gameObject.layer);
        entry.worldPosition = Round(transform.position);
        entry.worldRotation = Round(transform.eulerAngles);

        MeshFilter[] meshFilters = transform.GetComponentsInChildren<MeshFilter>();
        if (transform.GetComponent<MeshFilter>() != null)
        {
            List<string> meshNames = new List<string>();
            foreach (MeshFilter mf in transform.GetComponents<MeshFilter>())
            {
                if (mf != null && mf.sharedMesh != null)
                {
                    meshNames.Add(mf.sharedMesh.name);
                }
            }
            entry.meshNames = meshNames.ToArray();
            entry.hasRenderer = transform.GetComponent<Renderer>() != null;
        }
        else
        {
            entry.meshNames = Array.Empty<string>();
            entry.hasRenderer = transform.GetComponent<Renderer>() != null;
        }

        ExplodablePart explodable = transform.GetComponentInParent<ExplodablePart>();
        if (explodable != null)
        {
            entry.explodablePartPath = GetHierarchyPath(explodable.transform);
            if (explodable.Data != null)
            {
                entry.explodablePartId = explodable.Data.id ?? string.Empty;
            }
        }

        PartRenderCategory category = transform.GetComponent<PartRenderCategory>();
        if (category != null)
        {
            entry.canonicalPartId = category.CanonicalPartId ?? string.Empty;
            entry.subpieceId = category.SubpieceId ?? string.Empty;
            entry.primaryCategory = category.PrimaryCategory ?? string.Empty;
            entry.auxiliaryCategory = category.AuxiliaryCategory ?? string.Empty;
            entry.thermalSourcePartId = category.ThermalSourcePartId ?? string.Empty;
        }

        FastenerRuntimeMarker marker = transform.GetComponent<FastenerRuntimeMarker>();
        if (marker == null)
        {
            marker = transform.GetComponentInParent<FastenerRuntimeMarker>();
        }
        if (marker != null)
        {
            entry.fastenerFamilyId = marker.FastenerFamilyId ?? string.Empty;
            entry.fastenerInstanceId = marker.FastenerInstanceId ?? string.Empty;
            entry.fastenerTypeKey = marker.SceneTypeKey ?? string.Empty;
            entry.fastenerParentId = marker.ParentCanonicalPartId ?? string.Empty;
        }

        return entry;
    }

    private static void WriteExport(List<ExportEntry> entries, string kind)
    {
        Directory.CreateDirectory(OutputFolder);
        ExportWrapper wrapper = new ExportWrapper();
        wrapper.exportedAtUtc = DateTime.UtcNow.ToString("o");
        wrapper.kind = kind;
        wrapper.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        wrapper.entryCount = entries.Count;
        wrapper.entries = entries.ToArray();

        string json = JsonUtility.ToJson(wrapper, true);
        string latestPath = Path.Combine(OutputFolder, LatestFileName);
        File.WriteAllText(latestPath, json, new UTF8Encoding(false));

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.WriteAllText(Path.Combine(OutputFolder, $"{kind}_{stamp}.json"), json, new UTF8Encoding(false));
        AssetDatabase.Refresh();
    }

    private static string GetHierarchyPath(Transform transform)
    {
        StringBuilder sb = new StringBuilder();
        Transform current = transform;
        while (current != null)
        {
            if (sb.Length > 0)
            {
                sb.Insert(0, "/");
            }
            sb.Insert(0, current.name);
            current = current.parent;
        }
        return sb.ToString();
    }

    private static Vector3 Round(Vector3 v)
    {
        return new Vector3(
            (float)Math.Round(v.x, 4),
            (float)Math.Round(v.y, 4),
            (float)Math.Round(v.z, 4));
    }

    [Serializable]
    private class ExportEntry
    {
        public string objectName;
        public string path;
        public string parentPath;
        public string layer;
        public Vector3 worldPosition;
        public Vector3 worldRotation;
        public bool hasRenderer;
        public string[] meshNames;
        public string explodablePartId;
        public string explodablePartPath;
        public string canonicalPartId;
        public string subpieceId;
        public string primaryCategory;
        public string auxiliaryCategory;
        public string thermalSourcePartId;
        public string fastenerFamilyId;
        public string fastenerInstanceId;
        public string fastenerTypeKey;
        public string fastenerParentId;
    }

    [Serializable]
    private class ExportWrapper
    {
        public string exportedAtUtc;
        public string kind;
        public string scene;
        public int entryCount;
        public ExportEntry[] entries;
    }
}
