using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI
{
    /// <summary>
    /// Deep link from the web twin: https://twinsight.alexwoodcock.me/?part=&lt;entryId&gt;
    /// where entryId is an entry of Resources/x500v2_info_panel_bilingual_catalog.json.
    ///
    /// Once the scene, catalog, UI and selection systems are ready it selects that part
    /// through the SAME entry points the UI already uses (no selection logic is duplicated):
    ///   - mother parts / subpieces / fastener families -> SelectionManager.SelectObject(Transform)
    ///   - hotspot groups                              -> HotspotManager.TrySelectGroupById (pin click path)
    /// and then opens the details sheet (UIDetailsSheet.ShowInfo), which also frames the
    /// camera on the selection (OrbitCameraController.FocusOnObject).
    ///
    /// Bootstrapped by [RuntimeInitializeOnLoadMethod]; creates nothing if there is no ?part=.
    /// Lives in the UI assembly (Core cannot reference UI types).
    /// </summary>
    public sealed class DeepLinkSelector : MonoBehaviour
    {
        private const string QueryKey = "part";
        private const string CatalogResourceName = "x500v2_info_panel_bilingual_catalog";
        private const string HotspotEntryType = "hotspot_group";
        private const string FastenerEntryType = "fastener_family";
        private const string BridgeObjectName = "X500V2DeepLinkSelector";
        private const string EditorTestPrefKey = "DeepLinkSelector.TestPart";
        private const int MaxIdLength = 128;

        private const float PollIntervalSeconds = 0.25f;
        private const float ReadyTimeoutSeconds = 90f;
        private const int StablePollsRequired = 3;

        [Header("Editor testing (ignored in WebGL builds when the URL has ?part=)")]
        [SerializeField] private string editorTestPart = string.Empty;

        private static string pendingPartId;

        // ── Bootstrap ───────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string partId = ReadRequestedPartId(string.Empty);
            if (string.IsNullOrEmpty(partId))
            {
                return;
            }

            if (FindAnyObjectByType<DeepLinkSelector>() != null || GameObject.Find(BridgeObjectName) != null)
            {
                return;
            }

            pendingPartId = partId;
            GameObject host = new GameObject(BridgeObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<DeepLinkSelector>();
        }

        private void Start()
        {
            string partId = !string.IsNullOrEmpty(pendingPartId)
                ? pendingPartId
                : ReadRequestedPartId(editorTestPart);
            pendingPartId = null;

            if (string.IsNullOrEmpty(partId))
            {
                Destroy(gameObject);
                return;
            }

            StartCoroutine(RunDeepLink(partId));
        }

        // ── Main flow ───────────────────────────────────────────────

        private IEnumerator RunDeepLink(string requestedId)
        {
            Debug.Log($"[DeepLink] Requested part '{requestedId}'.");
            InfoPanelCatalogEntry entry = FindCatalogEntry(requestedId);
            if (entry == null)
            {
                Debug.LogWarning($"[DeepLink] Unknown part id '{requestedId}' (not an entryId of {CatalogResourceName}). Ignored.");
                Destroy(gameObject);
                yield break;
            }

            bool isHotspot = string.Equals(entry.entryType, HotspotEntryType, StringComparison.OrdinalIgnoreCase);

            // 1) Wait until scene + UI + selection systems are ready (timeout, realtime).
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            int lastPartCount = -1;
            int stablePolls = 0;
            while (true)
            {
                int partCount = FindObjectsByType<ExplodablePart>(FindObjectsSortMode.None).Length;
                stablePolls = (partCount > 0 && partCount == lastPartCount) ? stablePolls + 1 : 0;
                lastPartCount = partCount;

                bool systemsReady = UIManager.Instance != null
                    && UIManager.Instance.IsReadyForDeepLink
                    && SelectionManager.Instance != null
                    && (!isHotspot || HotspotManager.Instance != null);

                if (systemsReady && stablePolls >= StablePollsRequired)
                {
                    break;
                }

                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogWarning($"[DeepLink] Timed out waiting for the scene to be ready; '{entry.entryId}' not selected.");
                    Destroy(gameObject);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(PollIntervalSeconds);
            }

            // 2) Leave the hero screen like EXPLORE does (this also builds the hotspot pins).
            UIManager.Instance.PrepareViewerForDeepLink();
            yield return null;
            yield return null;

            // 3) Select through the existing entry points, replaying the clicks a user would make.
            bool selected;
            bool isolateAfterSelect = false;
            if (isHotspot)
            {
                selected = HotspotManager.Instance != null && HotspotManager.Instance.TrySelectGroupById(entry.entryId);
                if (selected)
                {
                    Debug.Log($"[DeepLink] Selected hotspot group '{entry.entryId}' (same call as a pin click).");
                }
                else
                {
                    Debug.LogWarning($"[DeepLink] Hotspot group '{entry.entryId}' is not available in this scene.");
                }
            }
            else
            {
                Found found = FindSelectableTransform(entry);
                selected = found.Target != null;
                if (selected)
                {
                    SelectionManager selection = SelectionManager.Instance;
                    Transform target = found.Target;
                    switch (found.Kind)
                    {
                        case TargetKind.Fastener:
                        {
                            // App flow: 1st click on a fastener selects its mother part, a 2nd click inside
                            // that context selects the fastener (SelectObject with contextFullSelection).
                            FastenerRuntimeMarker marker = target.GetComponent<FastenerRuntimeMarker>();
                            Transform parent = marker != null ? FindPartTransformById(marker.ParentCanonicalPartId) : null;
                            if (parent != null)
                            {
                                selection.SelectObject(parent);
                                yield return null;
                                selection.SelectObject(target, contextFullSelection: selection.CurrentFullSelection);
                            }
                            else
                            {
                                selection.SelectObject(target);
                            }

                            // Same as the Isolate button: shows only the fastener and frames the camera on it.
                            isolateAfterSelect = true;
                            break;
                        }

                        case TargetKind.Subpiece:
                        {
                            Transform full = UIManager.Instance.ResolveFullSelectionForDeepLink(target);
                            if (full == null || full == target || IsWholePartSubpiece(entry))
                            {
                                // The subpiece is the whole part (same title as its mother part) or has no
                                // distinct layer: a user click selects the part root, never a raw CAD child.
                                selection.SelectObject(full != null ? full : target);
                            }
                            else if (!target.IsChildOf(full))
                            {
                                // The app only treats descendants of the full selection as subpieces
                                // (SelectionManager.HandleClick), so this one cannot be picked in the app either.
                                Debug.LogWarning($"[DeepLink] '{entry.entryId}' is not a descendant of '{full.name}', so the app cannot select it alone. Selecting the parent part instead.");
                                selection.SelectObject(full);
                            }
                            else
                            {
                                // App flow: 1st click selects the full part, 2nd click on the subpiece selects it.
                                selection.SelectObject(full);
                                yield return null;
                                selection.SelectObject(target);
                            }

                            break;
                        }

                        default:
                            selection.SelectObject(target);
                            break;
                    }

                    Transform finalSelection = selection.CurrentSelection;
                    Debug.Log($"[DeepLink] Selected '{entry.entryId}' ({found.Kind}) -> {(finalSelection != null ? GetHierarchyPath(finalSelection) : "nothing")}");
                }
                else
                {
                    Debug.LogWarning($"[DeepLink] No scene object found for catalog entry '{entry.entryId}' ({entry.entryType}).");
                }
            }

            if (selected && isolateAfterSelect)
            {
                yield return null;
                UIManager.Instance.IsolateCurrentSelectionForDeepLink();
            }

            // 4) Open the info panel (also frames the camera on CurrentSelection).
            if (selected)
            {
                yield return null;
                UIManager.Instance.OpenDetailsForDeepLink();
            }

            Destroy(gameObject);
        }

        // ── URL parsing ─────────────────────────────────────────────

        private static string ReadRequestedPartId(string editorFallback)
        {
            string fromUrl = ExtractQueryParameter(Application.absoluteURL, QueryKey);
            if (!string.IsNullOrEmpty(fromUrl))
            {
                return fromUrl;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorFallback))
            {
                return Sanitize(editorFallback);
            }

            return Sanitize(UnityEditor.EditorPrefs.GetString(EditorTestPrefKey, string.Empty));
#else
            return string.Empty;
#endif
        }

        private static string ExtractQueryParameter(string url, string key)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            int hash = url.IndexOf('#');
            if (hash >= 0)
            {
                url = url.Substring(0, hash);
            }

            int question = url.IndexOf('?');
            if (question < 0 || question == url.Length - 1)
            {
                return string.Empty;
            }

            string[] pairs = url.Substring(question + 1).Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i];
                int eq = pair.IndexOf('=');
                string name = Decode(eq < 0 ? pair : pair.Substring(0, eq));
                if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Sanitize(eq < 0 ? string.Empty : Decode(pair.Substring(eq + 1)));
            }

            return string.Empty;
        }

        private static string Decode(string raw)
        {
            try
            {
                return Uri.UnescapeDataString(raw.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                return raw;
            }
        }

        private static string Sanitize(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            return trimmed.Length == 0 || trimmed.Length > MaxIdLength ? string.Empty : trimmed;
        }

        // ── Catalog + scene lookup ──────────────────────────────────

        private static InfoPanelCatalogRoot cachedCatalog;

        private static InfoPanelCatalogEntry FindCatalogEntry(string entryId)
        {
            if (cachedCatalog == null)
            {
                TextAsset asset = Resources.Load<TextAsset>(CatalogResourceName);
                if (asset == null)
                {
                    return null;
                }

                cachedCatalog = JsonUtility.FromJson<InfoPanelCatalogRoot>(asset.text);
            }

            InfoPanelCatalogRoot catalog = cachedCatalog;
            if (catalog == null || catalog.entries == null)
            {
                return null;
            }

            foreach (InfoPanelCatalogEntry candidate in catalog.entries)
            {
                if (candidate != null && string.Equals(candidate.entryId, entryId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the transform a user click would have to select to make the info panel show
        /// <paramref name="target"/>. Candidates are validated with the panel's own resolver
        /// (InfoPanelCatalogService.TryResolve) fed with the same arguments SelectionManager
        /// publishes, so the panel is guaranteed to resolve to the requested entry.
        /// </summary>
        private static Found FindSelectableTransform(InfoPanelCatalogEntry target)
        {
            var candidates = new Candidate();

            // A) Fastener families: runtime markers carry the catalog familyId.
            if (string.Equals(target.entryType, FastenerEntryType, StringComparison.OrdinalIgnoreCase))
            {
                FastenerRuntimeMarker[] markers = FindObjectsByType<FastenerRuntimeMarker>(FindObjectsSortMode.None);
                foreach (FastenerRuntimeMarker marker in markers)
                {
                    if (marker == null
                        || !marker.SourceIsPrimitiveFastener
                        || string.IsNullOrWhiteSpace(marker.FastenerFamilyId)
                        || !SelectionHierarchy.IsPrimitiveFastenerSource(marker.transform)
                        || !MatchesAnyId(target, marker.FastenerFamilyId))
                    {
                        continue;
                    }

                    candidates.Consider(marker.transform, 0, TargetKind.Fastener);
                }
            }

            // B) Parts / subpieces: walk every transform under every ExplodablePart.
            HashSet<string> filterKeys = BuildFilterKeys(target);
            var seen = new HashSet<Transform>();
            ExplodablePart[] parts = FindObjectsByType<ExplodablePart>(FindObjectsSortMode.None);
            foreach (ExplodablePart part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                foreach (Transform t in part.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !seen.Add(t))
                    {
                        continue;
                    }

                    ExplodablePart owner = t.GetComponentInParent<ExplodablePart>();
                    if (owner == null || owner.Data == null)
                    {
                        continue;
                    }

                    string partName = owner.Data.partName;
                    if (owner.transform == t)
                    {
                        // Full-part click: label = canonical part name.
                        if (MatchesAnyId(target, owner.Data.id))
                        {
                            candidates.Consider(t, 1, TargetKind.Part);
                        }
                        else if (Resolves(owner, partName, partName, target))
                        {
                            candidates.Consider(t, 3, TargetKind.Part);
                        }
                    }
                    else if (PassesNameFilter(t.name, filterKeys) && Resolves(owner, t.name, partName, target))
                    {
                        // Subpiece click: label = clicked transform name. A click lands on the transform
                        // that owns the collider, so prefer those over bare wrapper transforms.
                        candidates.Consider(t, t.GetComponent<Collider>() != null ? 2 : 12, TargetKind.Subpiece);
                    }
                }
            }

            return new Found { Target = candidates.Best, Kind = candidates.BestKind };
        }

        /// <summary>
        /// True when a subpiece entry is just the whole of its mother part (same title as the mother-part
        /// entry of one of its parentIds, e.g. the carbon fiber plates): a user click selects the part root.
        /// </summary>
        private static bool IsWholePartSubpiece(InfoPanelCatalogEntry target)
        {
            string title = target.en != null ? target.en.title : null;
            if (string.IsNullOrWhiteSpace(title) || target.parentIds == null)
            {
                return false;
            }

            foreach (string parentId in target.parentIds)
            {
                InfoPanelCatalogEntry parent = FindCatalogEntry(parentId);
                if (parent != null
                    && string.Equals(parent.entryType, "mother_part", StringComparison.OrdinalIgnoreCase)
                    && parent.en != null
                    && string.Equals(parent.en.title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindPartTransformById(string canonicalPartId)
        {
            if (string.IsNullOrWhiteSpace(canonicalPartId))
            {
                return null;
            }

            foreach (ExplodablePart part in FindObjectsByType<ExplodablePart>(FindObjectsSortMode.None))
            {
                if (part != null && part.Data != null
                    && string.Equals(part.Data.id, canonicalPartId, StringComparison.OrdinalIgnoreCase))
                {
                    return part.transform;
                }
            }

            return null;
        }

        private static bool Resolves(ExplodablePart owner, string selectionLabel, string canonicalName, InfoPanelCatalogEntry target)
        {
            return InfoPanelCatalogService.Instance.TryResolve(
                       owner.Data,
                       false,
                       string.Empty,
                       selectionLabel,
                       canonicalName,
                       out InfoPanelCatalogEntry resolved)
                   && resolved != null
                   && string.Equals(resolved.entryId, target.entryId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesAnyId(InfoPanelCatalogEntry target, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, target.entryId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, target.canonicalPartId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (target.selectionIds != null)
            {
                foreach (string id in target.selectionIds)
                {
                    if (string.Equals(value, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Cheap pre-filter so the (loose) catalog resolver only runs on plausible transforms.
        private static HashSet<string> BuildFilterKeys(InfoPanelCatalogEntry target)
        {
            var keys = new HashSet<string>();
            AddKey(keys, target.entryId);
            AddKey(keys, target.canonicalPartId);
            AddKeys(keys, target.selectionIds);
            AddKeys(keys, target.aliases);
            AddKey(keys, target.en != null ? target.en.title : null);
            AddKey(keys, target.es != null ? target.es.title : null);
            return keys;
        }

        private static void AddKeys(HashSet<string> keys, string[] values)
        {
            if (values == null)
            {
                return;
            }

            foreach (string value in values)
            {
                AddKey(keys, value);
            }
        }

        private static void AddKey(HashSet<string> keys, string value)
        {
            string key = AlphaNum(value);
            if (key.Length >= 4)
            {
                keys.Add(key);
            }
        }

        private static bool PassesNameFilter(string transformName, HashSet<string> keys)
        {
            string name = AlphaNum(transformName);
            if (name.Length < 4)
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (name.Contains(key) || key.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static string AlphaNum(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        private static string GetHierarchyPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                sb.Insert(0, p.name + "/");
            }

            return sb.ToString();
        }

        /// <summary>Deterministic best candidate: lowest score, active first, shallowest, then path order.</summary>
        private enum TargetKind { Part, Subpiece, Fastener }

        private struct Found
        {
            public Transform Target;
            public TargetKind Kind;
        }

        private sealed class Candidate
        {
            private Transform best;
            private TargetKind bestKind;
            private int bestScore = int.MaxValue;
            private int bestDepth = int.MaxValue;
            private string bestPath;

            public Transform Best => best;
            public TargetKind BestKind => bestKind;

            public void Consider(Transform t, int score, TargetKind kind)
            {
                if (t == null)
                {
                    return;
                }

                if (!t.gameObject.activeInHierarchy)
                {
                    score += 100;
                }

                int depth = 0;
                for (Transform p = t.parent; p != null; p = p.parent)
                {
                    depth++;
                }

                string path = GetHierarchyPath(t);
                bool better = best == null
                    || score < bestScore
                    || (score == bestScore && depth < bestDepth)
                    || (score == bestScore && depth == bestDepth && string.CompareOrdinal(path, bestPath) < 0);

                if (better)
                {
                    best = t;
                    bestKind = kind;
                    bestScore = score;
                    bestDepth = depth;
                    bestPath = path;
                }
            }
        }
    }
}
