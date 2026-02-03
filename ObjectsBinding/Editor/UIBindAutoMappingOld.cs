/*
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UIBind.DataModels;

/// <summary>
/// Auto-detects hierarchy changes and updates a cache for the Source Generator.
/// </summary>
[InitializeOnLoad]
public class UIBindAutoMappingOld
{
    private static Dictionary<int, MonitoredObjectInfo> monitoredObjects = new Dictionary<int, MonitoredObjectInfo>();
    
    private static bool isChecking = false;
    
    private const string SYNC_FOLDER = "Assets/ObjectsBinding/Sync";
    private const string GENERATED_FOLDER = "Assets/ObjectsBinding/GeneratedUIView";
    private const string SYNC_FILE = SYNC_FOLDER + "/MonitoredObjects.json";

    [System.Serializable]
    private class MonitoredObjectInfo
    {
        public string objectPath;        // Hierarchy path (e.g., "Canvas/Panel")
        public string scriptPath;        // Script file path
        public string lastHash;          // Hierarchy hash
        public string assetPath;         // Prefab asset path (if prefab)
        public string sceneGuid;         // Scene GUID (if scene object)
    }

    static UIBindAutoMapping()
    {
        EditorSceneManager.sceneSaved += OnSceneSaved;
        PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += OnEditorReady;
        EditorApplication.update += PeriodicHealthCheck;
        
        LoadMonitoredObjects();
        UIBindLogger.Log($"🚀 UIBind Auto Mapping initialized - {monitoredObjects.Count} objects being monitored.");
    }

    private static System.DateTime lastHealthCheckTime = System.DateTime.MinValue;
    private static System.DateTime lastHeavyCheckTime = System.DateTime.MinValue;
    private static System.DateTime lastHierarchyChangeTime = System.DateTime.MinValue;
    private const double LIGHT_CHECK_INTERVAL_SECONDS = 5.0;   // Light checks every 5s
    private const double HEAVY_CHECK_INTERVAL_SECONDS = 10.0;  // Heavy checks every 10s (faster response)
    private const double HIERARCHY_CHANGE_DEBOUNCE_SECONDS = 0.5;  // Wait 0.5s after hierarchy change before checking

    /// <summary>
    /// Hybrid periodic health check with three levels:
    /// - INSTANT: Hierarchy changed (debounced 0.5s)
    /// - LIGHT (5s): Only check file existence (fast)
    /// - HEAVY (10s): Full hierarchy hash + field assignment check (slower)
    /// </summary>
    private static void PeriodicHealthCheck()
    {
        // IMPORTANT: Skip during compilation or Play Mode
        if (EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPaused)
            return;

        if (monitoredObjects.Count == 0) return;

        // Check 1: Hierarchy changed recently? (debounced)
        bool hierarchyJustChanged = lastHierarchyChangeTime != System.DateTime.MinValue &&
                                     (System.DateTime.Now - lastHierarchyChangeTime).TotalSeconds >= HIERARCHY_CHANGE_DEBOUNCE_SECONDS &&
                                     (System.DateTime.Now - lastHierarchyChangeTime).TotalSeconds < HIERARCHY_CHANGE_DEBOUNCE_SECONDS + 0.1;
        
        if (hierarchyJustChanged)
        {
            UIBindLogger.Log("🔄 Hierarchy changed, checking monitored objects...");
            lastHierarchyChangeTime = System.DateTime.MinValue; // Reset
            PerformHeavyCheck(); // Immediate heavy check
            return;
        }

        // Check 2 & 3: Scheduled checks
        bool doLightCheck = (System.DateTime.Now - lastHealthCheckTime).TotalSeconds >= LIGHT_CHECK_INTERVAL_SECONDS;
        bool doHeavyCheck = (System.DateTime.Now - lastHeavyCheckTime).TotalSeconds >= HEAVY_CHECK_INTERVAL_SECONDS;

        if (!doLightCheck && !doHeavyCheck) return;

        if (doLightCheck)
        {
            lastHealthCheckTime = System.DateTime.Now;
            PerformLightCheck();
        }

        if (doHeavyCheck)
        {
            lastHeavyCheckTime = System.DateTime.Now;
            PerformHeavyCheck();
        }
    }

    /// <summary>
    /// LIGHT CHECK (5s): Quick file existence check
    /// Cost: Low - Only File.Exists() calls
    /// </summary>
    private static void PerformLightCheck()
    {
        foreach (var kvp in monitoredObjects.Values)
        {
            // Quick check: Does .g.cs file exist?
            string[] pathParts = kvp.objectPath.Split('/');
            string goName = pathParts[pathParts.Length - 1];
            string className = goName + "View";
            string generatedFilePath = Path.Combine(GENERATED_FOLDER, className + ".g.cs");

            if (!File.Exists(generatedFilePath))
            {
                UIBindLogger.LogWarning($"⚠️ Missing .g.cs for {goName}, regenerating...");
                
                // Find GameObject (scene or prefab) and regenerate
                GameObject go = FindMonitoredGameObject(kvp);
                if (go != null)
                {
                    UpdateHierarchyCacheFor(go);
                }
            }
        }
    }

    /// <summary>
    /// HEAVY CHECK (10s): Full hierarchy scan + field assignment check
    /// Cost: High - GameObject search, hierarchy traversal, SerializedObject
    /// </summary>
    private static void PerformHeavyCheck()
    {
        bool needsRefresh = false;
        int checkedCount = 0;
        int regeneratedCount = 0;

        foreach (var kvp in monitoredObjects.Values)
        {
            GameObject go = FindMonitoredGameObject(kvp);
            if (go == null)
            {
                continue;
            }

            checkedCount++;

            // Check 1: Has hierarchy changed since last generation?
            string currentHash = UIBindCodeGenerator.GetHierarchyHash(go.transform);
            
            if (currentHash != kvp.lastHash)
            {
                // Determine context (prefab or scene)
                string context = !string.IsNullOrEmpty(kvp.assetPath) ? "prefab" : "scene";
                UIBindLogger.Log($"🔄 Hierarchy changed in {context}: {go.name}, regenerating...");
                
                UpdateHierarchyCacheFor(go);
                kvp.lastHash = currentHash;
                needsRefresh = true;
                regeneratedCount++;
                continue;
            }

            // Check 2: Are SerializeFields fully assigned?
            var viewComponent = go.GetComponent(go.name + "View");
            if (viewComponent != null)
            {
                UIBindLogger.LogDebug($"    Checking fields for {viewComponent.GetType().Name}...");
                bool hasUnassigned = HasUnassignedFields(viewComponent);
                UIBindLogger.LogDebug($"    Has unassigned/missing in Scene instance: {hasUnassigned}");
                
                if (hasUnassigned)
                {
                    UIBindLogger.LogWarning($"🔧 [Step 2/3] Missing/Null references detected in Scene {go.name}, fixing...");
                    EditorApplication.delayCall += () => AutoMapUIBindings(go);
                }
            }
            else
            {
                UIBindLogger.LogDebug($"    No View component found");
            }

            // Check 3: If this is a prefab instance, also check Prefab Asset
            #if UNITY_EDITOR
            var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabAssetPath))
            {
                UIBindLogger.LogDebug($"    Checking Prefab Asset: {prefabAssetPath}");
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                if (prefabAsset != null)
                {
                    var prefabViewComponent = prefabAsset.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(mb => mb.GetType().Name.EndsWith("View"));
                    if (prefabViewComponent != null)
                    {
                        UIBindLogger.LogDebug($"      Checking fields for Prefab {prefabViewComponent.GetType().Name}...");
                        bool prefabHasUnassigned = HasUnassignedFields(prefabViewComponent);
                        UIBindLogger.LogDebug($"      Has unassigned/missing in Prefab: {prefabHasUnassigned}");
                        
                        if (prefabHasUnassigned)
                        {
                            UIBindLogger.LogWarning($"🔧 Missing/Null references detected in Prefab Asset '{prefabAssetPath}', fixing...");
                            EditorApplication.delayCall += () => 
                            {
                                // Map the prefab asset directly
                                Component[] allComps = prefabAsset.GetComponents(typeof(MonoBehaviour));
                                MonoBehaviour prefabComp = null;
                                foreach (Component comp in allComps)
                                {
                                    if (comp is MonoBehaviour mb && mb.GetType().Name.EndsWith("View"))
                                    {
                                        prefabComp = mb;
                                        break;
                                    }
                                }
                                
                                if (prefabComp != null)
                                {
                                    MapViewComponent(prefabComp, prefabAsset);
                                    EditorUtility.SetDirty(prefabAsset);
                                    AssetDatabase.SaveAssets();
                                    UIBindLogger.Log($"✅ Fixed Prefab Asset: {prefabAssetPath}");
                                }
                            };
                        }
                    }
                }
            }
            #endif
        }

        if (needsRefresh)
        {
            SaveMonitoredObjects();
        }
        
        if (regeneratedCount > 0)
        {
            UIBindLogger.Log($"✅ Health check complete: {regeneratedCount}/{checkedCount} objects regenerated");
        }
    }

    /// <summary>
    /// Check if a component has unassigned or missing SerializeField references
    /// </summary>
    private static bool HasUnassignedFields(UnityEngine.Component component)
    {
        if (component == null) return false;

        var so = new SerializedObject(component);
        var iterator = so.GetIterator();

        // Check _transforms and _gameObjects lists
        var transformsProp = so.FindProperty("_transforms");
        var gameObjectsProp = so.FindProperty("_gameObjects");

        UIBindLogger.LogDebug($"      _transforms: {(transformsProp != null ? $"size={transformsProp.arraySize}" : "null")}");
        UIBindLogger.LogDebug($"      _gameObjects: {(gameObjectsProp != null ? $"size={gameObjectsProp.arraySize}" : "null")}");

        // Check if lists are empty
        if (transformsProp != null && transformsProp.arraySize == 0)
        {
            UIBindLogger.LogDebug("      -> Empty _transforms array detected");
            return true;
        }
        if (gameObjectsProp != null && gameObjectsProp.arraySize == 0)
        {
            UIBindLogger.LogDebug("      -> Empty _gameObjects array detected");
            return true;
        }

        // Check for missing/null references in _transforms
        if (transformsProp != null && HasMissingReferences(transformsProp))
        {
            UIBindLogger.LogWarning($"⚠️ Missing Transform references detected in {component.gameObject.name}");
            return true;
        }

        // Check for missing/null references in _gameObjects
        if (gameObjectsProp != null && HasMissingReferences(gameObjectsProp))
        {
            UIBindLogger.LogWarning($"⚠️ Missing GameObject references detected in {component.gameObject.name}");
            return true;
        }

        // Check component lists for missing references
        int checkedLists = 0;
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType == SerializedPropertyType.Generic && 
                iterator.isArray && 
                iterator.name.StartsWith("_") && 
                iterator.name.EndsWith("s"))
            {
                checkedLists++;
                UIBindLogger.LogDebug($"      Checking list: {iterator.name} (size={iterator.arraySize})");
                
                // Empty list
                if (iterator.arraySize == 0)
                {
                    UIBindLogger.LogDebug($"      -> Empty array detected: {iterator.name}");
                    return true;
                }

                // Has missing/null references
                if (HasMissingReferences(iterator))
                {
                    UIBindLogger.LogWarning($"⚠️ Missing references in list '{iterator.name}' on {component.gameObject.name}");
                    return true;
                }
            }
        }

        UIBindLogger.LogDebug($"      Checked {checkedLists} component lists - all OK");
        return false;
    }

    /// <summary>
    /// Check if an array property contains any missing (null) references
    /// </summary>
    private static bool HasMissingReferences(SerializedProperty arrayProp)
    {
        if (arrayProp == null || !arrayProp.isArray) return false;

        int missingCount = 0;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            
            // Check if it's an object reference property
            if (element.propertyType == SerializedPropertyType.ObjectReference)
            {
                // objectReferenceValue == null means missing reference
                if (element.objectReferenceValue == null)
                {
                    missingCount++;
                }
            }
        }

        if (missingCount > 0)
        {
            UIBindLogger.LogWarning($"   ↳ Found {missingCount} missing references in '{arrayProp.name}' (total: {arrayProp.arraySize})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get detailed report of missing references (for debugging)
    /// </summary>
    private static string GetMissingReferencesReport(UnityEngine.Component component)
    {
        if (component == null) return "Component is null";

        var so = new SerializedObject(component);
        var report = new System.Text.StringBuilder();
        int totalMissing = 0;

        var iterator = so.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType == SerializedPropertyType.Generic && 
                iterator.isArray && 
                iterator.name.StartsWith("_"))
            {
                int missingInArray = 0;
                for (int i = 0; i < iterator.arraySize; i++)
                {
                    var element = iterator.GetArrayElementAtIndex(i);
                    if (element.propertyType == SerializedPropertyType.ObjectReference && 
                        element.objectReferenceValue == null)
                    {
                        missingInArray++;
                    }
                }

                if (missingInArray > 0)
                {
                    report.AppendLine($"  • {iterator.name}: {missingInArray}/{iterator.arraySize} missing");
                    totalMissing += missingInArray;
                }
            }
        }

        if (totalMissing > 0)
        {
            report.Insert(0, $"Missing References Report for {component.gameObject.name}:\n");
            report.AppendLine($"  Total: {totalMissing} missing references");
        }

        return report.ToString();
    }

    private static void OnEditorReady()
    {
        // Check if there's a pending auto-binding task after compilation
        string pendingGOName = EditorPrefs.GetString("UIBind_PendingAutoBinding", "");
        if (!string.IsNullOrEmpty(pendingGOName))
        {
            EditorPrefs.DeleteKey("UIBind_PendingAutoBinding");

            EditorApplication.delayCall += () =>
            {
                GameObject go = null;

                // Check if we're in prefab edit mode
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    // Find in prefab stage
                    if (prefabStage.prefabContentsRoot.name == pendingGOName)
                    {
                        go = prefabStage.prefabContentsRoot;
                    }
                    else
                    {
                        // Search in prefab hierarchy
                        go = FindInPrefabStage(prefabStage, pendingGOName);
                    }
                }
                else
                {
                    // Find in scene
                    go = GameObject.Find(pendingGOName);
                }

                if (go != null)
                {
                    UIBindLogger.Log($"🔗 Auto-binding UI references for {go.name}...");
                    AutoMapUIBindings(go);
                }
                else
                {
                    UIBindLogger.LogWarning($"⚠️ Could not find GameObject '{pendingGOName}' for auto-binding");
                }
            };
        }
    }

    private static GameObject FindInPrefabStage(PrefabStage stage, string name)
    {
        var root = stage.prefabContentsRoot;
        if (root.name == name) return root;

        // Search in children
        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t.gameObject.name == name)
                return t.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Menu command to check missing references on selected GameObject
    /// </summary>
    // [MenuItem("Tools/UI Binding/Check Missing References")]
    private static void CheckMissingReferences()
    {
        var selectedGO = Selection.activeGameObject;
        if (selectedGO == null)
        {
            UIBindLogger.LogError("Please select a GameObject first!");
            return;
        }

        var viewComponent = selectedGO.GetComponents<MonoBehaviour>()
            .FirstOrDefault(mb => mb.GetType().Name.EndsWith("View"));

        if (viewComponent == null)
        {
            UIBindLogger.LogError($"No View component found on {selectedGO.name}!");
            return;
        }

        string report = GetMissingReferencesReport(viewComponent);
        if (string.IsNullOrEmpty(report))
        {
            UIBindLogger.Log($"✅ No missing references found in {selectedGO.name}");
        }
        else
        {
            UIBindLogger.LogWarning(report);
            
            // Offer to auto-fix
            if (EditorUtility.DisplayDialog(
                "Missing References Detected",
                $"Found missing references in {selectedGO.name}.\n\nWould you like to auto-fix them now?",
                "Fix Now",
                "Cancel"))
            {
                AutoMapUIBindings(selectedGO);
            }
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Only check when EXITING Play Mode back to Edit Mode
        // This ensures we capture any changes made during Play Mode testing
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            UIBindLogger.Log("⏸️ Exited Play Mode - checking for changes...");
            CheckMonitoredObjects(true); // Force refresh after exiting Play Mode
        }
        // Note: Do NOT check when entering Play Mode or during Play Mode
    }

    /// <summary>
    /// Called whenever hierarchy changes (add/delete/move objects)
    /// Debounced to avoid spamming checks
    /// </summary>
    private static void OnHierarchyChanged()
    {
        // Skip during Play Mode
        if (EditorApplication.isPlaying || EditorApplication.isPaused)
            return;

        // Skip if no monitored objects
        if (monitoredObjects.Count == 0)
            return;

        // Record the time of change (debouncing)
        lastHierarchyChangeTime = System.DateTime.Now;
    }

    private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        // Skip if in Play Mode - scene saves during Play Mode are temporary
        if (EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            UIBindLogger.Log("⚠️ Scene saved during Play Mode - skipping check (changes are temporary)");
            return;
        }
        
        UIBindLogger.Log("💾 Scene saved - checking for changes...");
        CheckMonitoredObjects(true); // Force refresh
    }

    private static void OnPrefabStageClosing(PrefabStage stage)
    {
        // Skip if in Play Mode
        if (EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            return;
        }
        
        UIBindLogger.Log("📦 Exiting prefab mode - checking for changes...");
        CheckMonitoredObjects(true); // Force refresh
    }

    private static void LoadMonitoredObjects()
    {
        if (!File.Exists(SYNC_FILE)) return;
        try
        {
            string json = File.ReadAllText(SYNC_FILE);
            if (string.IsNullOrEmpty(json)) return;

            var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
            if (wrapper?.list != null)
            {
                foreach (var info in wrapper.list)
                {
                    monitoredObjects[info.objectPath.GetHashCode()] = info;
                }
            }
        }
        catch (System.Exception e) { UIBindLogger.LogError($"Failed to load monitored objects: {e.Message}"); }
    }

    private static void SaveMonitoredObjects()
    {
        Directory.CreateDirectory(SYNC_FOLDER);
        var list = new List<MonitoredObjectInfo>(monitoredObjects.Values);
        var wrapper = new MonitoredListWrapper { list = list };
        File.WriteAllText(SYNC_FILE, JsonUtility.ToJson(wrapper, true));
    }

    [System.Serializable]
    private class MonitoredListWrapper { public List<MonitoredObjectInfo> list; }

    public static void CheckMonitoredObjects(bool forceRefresh)
    {
        // Skip during checking, compilation, or Play Mode
        if (isChecking || EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPaused) 
            return;
        
        isChecking = true;
        bool hasChanges = false;
        List<int> toRemove = new List<int>();

        foreach (var kvp in monitoredObjects)
        {
            GameObject go = FindMonitoredGameObject(kvp.Value);
            if (go == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            string currentHash = UIBindCodeGenerator.GetHierarchyHash(go.transform);
            if (currentHash != kvp.Value.lastHash)
            {
                UIBindLogger.Log($"🔄 Detected changes in {go.name}, updating cache...");
                UpdateHierarchyCacheFor(go);
                kvp.Value.lastHash = currentHash;
                hasChanges = true;
            }
        }

        foreach (var id in toRemove) monitoredObjects.Remove(id);
        if (toRemove.Count > 0 || hasChanges) SaveMonitoredObjects();
        
        if (hasChanges && forceRefresh)
        {
            UIBindLogger.Log("✨ Changes detected, triggering compilation...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
        
        isChecking = false;
    }

    public static void StartMonitoring(GameObject go, string scriptPath)
    {
        string objectPath = GetGameObjectPath(go.transform);
        int instanceId = objectPath.GetHashCode();

        if (!monitoredObjects.ContainsKey(instanceId))
        {
            // Check if this is a prefab or scene object
            string assetPath = "";
            string sceneGuid = "";
            
            #if UNITY_EDITOR
            UIBindLogger.LogDebug($"[StartMonitoring] Checking {go.name}...");
            
            // Check if it's a prefab asset
            var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            UIBindLogger.LogDebug($"  PrefabAssetPath: '{prefabAssetPath}'");
            if (!string.IsNullOrEmpty(prefabAssetPath))
            {
                assetPath = prefabAssetPath;
                UIBindLogger.LogDebug($"  → Set assetPath = '{assetPath}'");
            }
            
            // Get scene GUID if it's a scene object
            var scene = go.scene;
            UIBindLogger.LogDebug($"  Scene.IsValid: {scene.IsValid()}, Scene.path: '{scene.path}'");
            if (scene.IsValid())
            {
                sceneGuid = scene.path; // Use scene path as identifier
                UIBindLogger.LogDebug($"  → Set sceneGuid = '{sceneGuid}'");
            }
            #endif

            monitoredObjects[instanceId] = new MonitoredObjectInfo
            {
                objectPath = objectPath,
                scriptPath = scriptPath,
                lastHash = UIBindCodeGenerator.GetHierarchyHash(go.transform),
                assetPath = assetPath,
                sceneGuid = sceneGuid
            };
            
            SaveMonitoredObjects();
            
            string location = !string.IsNullOrEmpty(assetPath) ? $"Prefab: {assetPath}" : $"Scene: {sceneGuid}";
            UIBindLogger.Log($"📊 Now monitoring '{go.name}' ({location})");
        }
    }

    public static void UpdateHierarchyCacheFor(GameObject go)
    {
        // IMPORTANT: Do not generate code during Play Mode
        if (EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            // Clear any pending tasks to prevent execution after exiting Play Mode
            EditorPrefs.DeleteKey("UIBind_PendingAutoBinding");
            return;
        }

        string className = go.name + "View";
        
        // Set pending auto-binding after code generation
        EditorPrefs.SetString("UIBind_PendingAutoBinding", go.name);
        
        // Generate code directly from GameObject (no cache file needed)
        EditorApplication.delayCall += () => {
            var fallbackType = System.Type.GetType("LEARNING.GameFlowFramework.ObjectsBinding.Editor.FallbackCodeGenerator,Assembly-CSharp-Editor");
            if (fallbackType != null)
            {
                var method = fallbackType.GetMethod("GenerateCodeForGameObject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, new object[] { go, className });
                
                UIBindLogger.Log($"✅ Generated code for {className}");
            }
        };
    }

    public static string GetGeneratedScriptPath(GameObject go)
    {
        Directory.CreateDirectory(GENERATED_FOLDER);
        string className = go.name + "View";
        return Path.Combine(GENERATED_FOLDER, className + ".cs");
    }

    /// <summary>
    /// Auto-maps UI hierarchy to List-based serialized fields
    /// Smart handling: If target is a prefab asset, ONLY map the asset (scene instances inherit automatically)
    /// </summary>
    public static void AutoMapUIBindings(GameObject targetGO)
    {
        if (targetGO == null)
        {
            UIBindLogger.LogError("Target GameObject is null!");
            return;
        }

        // Find MonoBehaviour with generated code
        var viewComponent = targetGO.GetComponents<MonoBehaviour>()
            .FirstOrDefault(mb => mb.GetType().Name.EndsWith("View"));

        if (viewComponent == null)
        {
            UIBindLogger.LogError($"No View component found on {targetGO.name}!");
            return;
        }

        // Determine if this is a prefab asset or scene object
        #if UNITY_EDITOR
        string assetPath = AssetDatabase.GetAssetPath(targetGO);
        bool isPrefabAsset = !string.IsNullOrEmpty(assetPath);
        
        // Check if we're in prefab edit mode
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        bool isInPrefabEditMode = prefabStage != null;
        
        if (isPrefabAsset || isInPrefabEditMode)
        {
            // ✅ PREFAB MODE: Only map the prefab asset itself
            UIBindLogger.Log($"🎯 Mapping prefab asset: {(isInPrefabEditMode ? prefabStage.assetPath : assetPath)}");
            bool success = MapViewComponent(viewComponent, targetGO);
            
            if (success)
            {
                if (isInPrefabEditMode)
                {
                    // Mark prefab stage as dirty
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                    UIBindLogger.Log($"✅ Mapped prefab in edit mode (will save on exit)");
                }
                else
                {
                    // Save prefab asset
                    EditorUtility.SetDirty(targetGO);
                    AssetDatabase.SaveAssets();
                    UIBindLogger.Log($"✅ Mapped and saved prefab asset");
                }
                UIBindLogger.Log($"📌 Scene instances will automatically inherit the mappings from prefab.");
            }
            return;
        }
        
        // ✅ SCENE OBJECT MODE: Map scene instance
        var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(targetGO);
        if (!string.IsNullOrEmpty(prefabAssetPath))
        {
            // This is a prefab instance in scene
            UIBindLogger.Log($"🔗 Mapping scene instance of prefab: {targetGO.name}");
            bool success = MapViewComponent(viewComponent, targetGO);
            
            if (!success) return;
            
            // Also check and map the prefab asset if needed
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefabAsset != null)
            {
                var prefabViewComponent = prefabAsset.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(mb => mb.GetType().Name.EndsWith("View"));
                
                if (prefabViewComponent != null)
                {
                    bool prefabNeedsMapping = HasUnassignedFields(prefabViewComponent);
                    
                    if (prefabNeedsMapping)
                    {
                        UIBindLogger.Log($"🔗 Also mapping Prefab Asset (has missing references): {prefabAssetPath}");
                        MapViewComponent(prefabViewComponent, prefabAsset);
                        EditorUtility.SetDirty(prefabAsset);
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        UIBindLogger.Log($"⏭️ Skipped Prefab Asset mapping (already complete): {prefabAssetPath}");
                    }
                }
            }
        }
        else
        {
            // Pure scene object (not a prefab instance)
            UIBindLogger.Log($"🔗 Mapping scene object: {targetGO.name}");
            MapViewComponent(viewComponent, targetGO);
        }
        #endif
    }

    /// <summary>
    /// Core mapping logic extracted for reuse
    /// </summary>
    private static bool MapViewComponent(MonoBehaviour viewComponent, GameObject targetGO)
    {
        var so = new SerializedObject(viewComponent);
        
        // Get List fields
        var transformsProp = so.FindProperty("_transforms");
        var gameObjectsProp = so.FindProperty("_gameObjects");
        
        if (transformsProp == null || gameObjectsProp == null)
        {
            UIBindLogger.LogError("List fields not found! Make sure code is generated with List-based serialization.");
            return false;
        }

        // Clear existing lists
        transformsProp.ClearArray();
        gameObjectsProp.ClearArray();
        
        // Collect references
        var transforms = new List<Transform>();
        var gameObjects = new List<GameObject>();
        var componentsByType = new Dictionary<string, List<UnityEngine.Component>>();
        
        CollectReferencesFromHierarchy(targetGO.transform, "", transforms, gameObjects, componentsByType);
        
        // Populate transforms list
        for (int i = 0; i < transforms.Count; i++)
        {
            transformsProp.InsertArrayElementAtIndex(i);
            transformsProp.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
        }
        
        // Populate gameObjects list
        for (int i = 0; i < gameObjects.Count; i++)
        {
            gameObjectsProp.InsertArrayElementAtIndex(i);
            gameObjectsProp.GetArrayElementAtIndex(i).objectReferenceValue = gameObjects[i];
        }
        
        // Populate component lists
        foreach (var kvp in componentsByType)
        {
            string listName = $"_{kvp.Key.ToLower()}s";
            var prop = so.FindProperty(listName);
            if (prop != null)
            {
                prop.ClearArray();
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    prop.InsertArrayElementAtIndex(i);
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = kvp.Value[i];
                }
            }
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(viewComponent);
        
        UIBindLogger.Log($"✅ [Step 3/3] Auto-mapped {transforms.Count} transforms, {gameObjects.Count} gameObjects, " +
                        $"and {componentsByType.Sum(kvp => kvp.Value.Count)} components to {viewComponent.GetType().Name} on {targetGO.name}");
        
        return true;
    }

    private static void CollectReferencesFromHierarchy(
        Transform parent,
        string prefix,
        List<Transform> transforms,
        List<GameObject> gameObjects,
        Dictionary<string, List<UnityEngine.Component>> componentsByType)
    {
        if (!UIBindEditor.ShouldBindPublic(parent)) return;

        foreach (Transform child in parent)
        {
            if (!UIBindEditor.ShouldBindPublic(child)) continue;
            
            string safeName = SanitizeName(child.name);
            string fieldPrefix = string.IsNullOrEmpty(prefix) ? safeName : $"{prefix}_{safeName}";
            
            // Add transform and gameObject
            transforms.Add(child);
            gameObjects.Add(child.gameObject);
            
            // Add components
            var components = UIBindEditor.GetUIComponentsPublic(child);
            foreach (var comp in components)
            {
                string typeName = comp.GetType().Name;
                if (!componentsByType.ContainsKey(typeName))
                {
                    componentsByType[typeName] = new List<UnityEngine.Component>();
                }
                componentsByType[typeName].Add(comp);
            }
            
            // Recurse
            CollectReferencesFromHierarchy(child, fieldPrefix, transforms, gameObjects, componentsByType);
        }
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "UnnamedObject";
        
        var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
        if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"_+", "_");
        sanitized = sanitized.Trim('_');
        
        return string.IsNullOrEmpty(sanitized) ? "UnnamedObject" : sanitized;
    }

    /// <summary>
    /// Smart GameObject finder that prioritizes prefab assets when tracking prefabs
    /// </summary>
    private static GameObject FindMonitoredGameObject(MonitoredObjectInfo info)
    {
        // ✅ Priority 1: If tracking a prefab asset, load it directly (DON'T use scene instances)
        if (!string.IsNullOrEmpty(info.assetPath))
        {
            // Check if in Prefab Edit Mode for this specific prefab
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.assetPath == info.assetPath)
            {
                // Return prefab from edit mode
                return FindGameObjectInHierarchy(prefabStage.prefabContentsRoot.transform, info.objectPath);
            }
            
            // Load prefab asset directly from disk
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(info.assetPath);
            if (prefabAsset != null)
            {
                return FindGameObjectInPrefab(prefabAsset, info.objectPath);
            }
        }

        // ✅ Priority 2: Scene object (only if NOT a prefab)
        GameObject go = GameObject.Find(info.objectPath);
        if (go != null)
        {
            // Make sure this is NOT a prefab instance when we're tracking scene objects
            string goPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            bool isSceneObject = string.IsNullOrEmpty(info.assetPath) && string.IsNullOrEmpty(goPrefabPath);
            
            if (isSceneObject)
            {
                return go; // Pure scene object
            }
        }

        return null;
    }

    /// <summary>
    /// Find GameObject in prefab by hierarchy path
    /// </summary>
    private static GameObject FindGameObjectInPrefab(GameObject prefabRoot, string path)
    {
        if (prefabRoot.name == path) return prefabRoot;
        
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;
        
        if (prefabRoot.name == parts[0])
        {
            Transform current = prefabRoot.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null) return null;
                current = child;
            }
            return current.gameObject;
        }
        
        return null;
    }

    /// <summary>
    /// Find GameObject in hierarchy by path
    /// </summary>
    private static GameObject FindGameObjectInHierarchy(Transform root, string path)
    {
        if (root.name == path) return root.gameObject;
        
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;
        
        if (root.name == parts[0])
        {
            Transform current = root;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null) return null;
                current = child;
            }
            return current.gameObject;
        }
        
        return null;
    }

    private static string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
*/