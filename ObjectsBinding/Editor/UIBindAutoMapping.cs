#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Reflection;
using UIBind;
using ObjectsBinding;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Handles the core logic for the UIBind system.
/// This script is now much simpler and only acts when manually triggered.
/// </summary>
[InitializeOnLoad]
public class UIBindAutoMapping
{
    // --- FROM OLD SYSTEM (for initial creation) ---
    private static Dictionary<int, MonitoredObjectInfo> monitoredObjects = new Dictionary<int, MonitoredObjectInfo>();
    private const string SYNC_FOLDER = "Assets/ObjectsBinding/Sync";
    private const string GENERATED_FOLDER = "Assets/ObjectsBinding/GeneratedUIView";
    private const string SYNC_FILE = SYNC_FOLDER + "/MonitoredObjects.json";

    [System.Serializable]
    private class MonitoredObjectInfo
    {
        public string objectPath;
        public string scriptPath;
        public string lastHash;
        public string assetPath;
        public string sceneGuid;
    }

    [System.Serializable]
    private class MonitoredListWrapper { public List<MonitoredObjectInfo> list; }
    // -----------------------------------------

    private const string PENDING_AUTO_BIND_PREF = "UIBind_PendingAutoBinding_V2"; // V2 to avoid conflicts with old format

    [System.Serializable]
    private class PendingBindingContext
    {
        public string gameObjectName; // For logging
        public string scenePath;
        public string rootPrefabAssetPath;
        public string relativePathInPrefab;
        public string fullHierarchyPath; // For scene objects
    }

    static UIBindAutoMapping()
    {
        EditorApplication.delayCall += OnEditorReady;
        LoadMonitoredObjects();
    }

    // --- METHODS FOR NEW "UPDATE" WORKFLOW ---

    public static void RegenerateBindingsFor(MonoBehaviour viewComponent)
    {
        if (viewComponent == null)
        {
            UIBindLogger.LogError("Target component is null!");
            return;
        }

        GameObject targetGO = viewComponent.gameObject;
        UIBindLogger.Log($"🔵 Manual update requested for '{targetGO.name}'.");

        var componentType = viewComponent.GetType();
        var attribute = componentType.GetCustomAttribute<UIBindAttribute>();
        if (attribute == null)
        {
            UIBindLogger.LogError($"The component '{componentType.Name}' on '{targetGO.name}' does not have a [UIBind] attribute.");
            return;
        }

        // Find the script file's current path by searching the asset database.
        // This is robust against the file being moved.
        string[] guids = AssetDatabase.FindAssets($"{componentType.Name} t:script");
        string scriptPath = guids.Select(AssetDatabase.GUIDToAssetPath)
                                 .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == componentType.Name);

        if (string.IsNullOrEmpty(scriptPath))
        {
            UIBindLogger.LogError($"Could not find the source script file for '{componentType.Name}'. It may have been deleted. Cannot regenerate bindings.");
            return;
        }
        UIBindLogger.Log($"🔍 Found script at: {scriptPath}");

        UIBindLogger.Log($"[1/2] Regenerating code file for script: {scriptPath}");
        GenerateCode(targetGO, scriptPath);
        UIBindLogger.Log($"[2/2] Auto-mapping of UI elements will run after compilation completes.");
    }
    
    // Made public to be callable from UpdateHierarchyCacheFor
    public static void GenerateCode(GameObject go, string scriptPath)
    {
        // This method is called to trigger code generation. It now saves a rich
        // context to find the object again after compilation for auto-mapping fields.
        var context = new PendingBindingContext { gameObjectName = go.name };
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

        if (prefabStage != null && go.scene == prefabStage.scene)
        {
            context.rootPrefabAssetPath = prefabStage.assetPath;
            if (go != prefabStage.prefabContentsRoot)
            {
                context.relativePathInPrefab = AnimationUtility.CalculateTransformPath(go.transform, prefabStage.prefabContentsRoot.transform);
            }
        }
        else if(go.scene.IsValid())
        {
            context.scenePath = go.scene.path;
            context.fullHierarchyPath = GetGameObjectPath(go.transform);
        }

        EditorPrefs.SetString(PENDING_AUTO_BIND_PREF, JsonUtility.ToJson(context));
        
        // The rest of the method invokes the FallbackCodeGenerator
        EditorApplication.delayCall += () => {
            var fallbackType = System.Type.GetType("ObjectsBinding.Editor.FallbackCodeGenerator, ObjectsBinding.Editor");
            if (fallbackType == null)
            {
                UIBindLogger.LogError("FATAL: Could not find the 'FallbackCodeGenerator' type. Ensure it is in the 'ObjectsBinding.Editor' namespace and assembly.");
                return;
            }

            var method = fallbackType.GetMethod("GenerateCodeForGameObject", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                UIBindLogger.LogError("FATAL: Could not find the 'GenerateCodeForGameObject' method in the code generator.");
                return;
            }
            
            string className = Path.GetFileNameWithoutExtension(scriptPath);
            method.Invoke(null, new object[] { go, className, scriptPath });
            AssetDatabase.Refresh();
        };
    }
    
    private static void OnEditorReady()
    {
        string pendingJson = EditorPrefs.GetString(PENDING_AUTO_BIND_PREF, "");
        if (string.IsNullOrEmpty(pendingJson)) return;

        EditorPrefs.DeleteKey(PENDING_AUTO_BIND_PREF);
        
        PendingBindingContext context;
        try {
            context = JsonUtility.FromJson<PendingBindingContext>(pendingJson);
        } catch {
            UIBindLogger.LogWarning($"Failed to parse pending binding context. Skipping auto-map.");
            return;
        }

        GameObject targetGO = null;
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

        // Case 1: We are in a prefab stage that matches the context
        if (prefabStage != null && !string.IsNullOrEmpty(context.rootPrefabAssetPath) && prefabStage.assetPath == context.rootPrefabAssetPath)
        {
            if (string.IsNullOrEmpty(context.relativePathInPrefab))
            {
                targetGO = prefabStage.prefabContentsRoot; // It's the root
            }
            else
            {
                var rootTransform = prefabStage.prefabContentsRoot.transform;
                var childTransform = rootTransform.Find(context.relativePathInPrefab);
                if (childTransform != null)
                {
                    targetGO = childTransform.gameObject;
                }
            }
        }
        // Case 2: We are in a scene that matches the context
        else if (!string.IsNullOrEmpty(context.scenePath) && context.scenePath == UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path)
        {
            // GameObject.Find can take a path with / but doesn't work well with roots.
            // A custom finder is more reliable. We'll iterate from roots.
            foreach (var root in UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == context.fullHierarchyPath.Split('/')[0])
                {
                    var result = root.transform.Find(string.Join("/", context.fullHierarchyPath.Split('/').Skip(1)));
                    if (result != null)
                    {
                        targetGO = result.gameObject;
                        break;
                    }
                    else if (root.name == context.fullHierarchyPath) // It was the root
                    {
                        targetGO = root;
                        break;
                    }
                }
            }
            if (targetGO == null)
            {
                 targetGO = GameObject.Find(context.fullHierarchyPath);
            }
        }

        if (targetGO != null)
        {
            EditorApplication.delayCall += () => 
            {
                UIBindLogger.Log($"🔗 Auto-binding UI references for '{targetGO.name}'...");
                AutoMapUIBindings(targetGO);
            };
        }
        else
        {
            UIBindLogger.LogWarning($"Could not find GameObject '{context.gameObjectName}' to auto-bind after compilation. Context: {pendingJson}");
        }
    }

    public static void AutoMapUIBindings(GameObject targetGO)
    {
        if (targetGO == null) return;

        var viewComponent = targetGO.GetComponents<MonoBehaviour>().FirstOrDefault(mb => mb.GetType().GetCustomAttribute<UIBindAttribute>() != null);
        if (viewComponent == null)
        {
            UIBindLogger.LogError($"No '...View' script found on '{targetGO.name}' to map references to.");
            return;
        }

        var so = new SerializedObject(viewComponent);
        var transformsProp = so.FindProperty("_transforms");
        var gameObjectsProp = so.FindProperty("_gameObjects");
        
        if (transformsProp == null || gameObjectsProp == null)
        {
            UIBindLogger.LogError("List fields '_transforms' or '_gameObjects' not found! Make sure code was generated correctly.");
            return;
        }

        transformsProp.ClearArray();
        gameObjectsProp.ClearArray();
        
        var allComponents = viewComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => f.FieldType.IsSubclassOf(typeof(Component)))
            .ToList();

        foreach (var field in allComponents)
        {
            var listProp = so.FindProperty($"_{field.FieldType.Name.ToLower()}s");
            if(listProp != null)
            {
                listProp.ClearArray();
            }
        }

        var collectedTransforms = new System.Collections.Generic.List<Transform>();
        var collectedGameObjects = new System.Collections.Generic.List<GameObject>();
        var collectedComponents = new System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<Component>>();

        CollectReferences(targetGO.transform, collectedTransforms, collectedGameObjects, collectedComponents);

        PopulateSerializedList(transformsProp, collectedTransforms.Cast<Object>().ToList());
        PopulateSerializedList(gameObjectsProp, collectedGameObjects.Cast<Object>().ToList());

        foreach(var entry in collectedComponents)
        {
            var listProp = so.FindProperty($"_{entry.Key.Name.ToLower()}s");
            if(listProp != null)
            {
                PopulateSerializedList(listProp, entry.Value.Cast<Object>().ToList());
            }
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(viewComponent);
        
        UIBindLogger.Log($"✅ Successfully mapped {collectedTransforms.Count} references to '{viewComponent.GetType().Name}'.");
    }

    // We need to mirror the logic from the generator to stop recursion at nested views.
    // To do that, we need the same helper methods.
    
    /// <summary>
    /// Check if GameObject has its own View component that's being monitored.
    /// This is a copy of the logic in FallbackCodeGenerator.
    /// </summary>
    private static bool HasMonitoredViewComponent(GameObject go)
    {
        if (go == null) return false;
        string expectedClassName = go.name + "View";
        var viewComponent = go.GetComponent(expectedClassName);
        if (viewComponent == null) return false;

        if (!File.Exists(SYNC_FILE)) return false;
        try
        {
            string json = File.ReadAllText(SYNC_FILE);
            var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
            if (wrapper?.list == null) return false;
            string goPath = GetGameObjectPath(go.transform);
            return wrapper.list.Any(info => info.objectPath == goPath);
        }
        catch { return false; }
    }
    
    private static void CollectReferences(Transform parent, System.Collections.Generic.List<Transform> transforms, System.Collections.Generic.List<GameObject> gameObjects, System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<Component>> components)
    {
        // This method now mirrors the logic of CreateHierarchyNode from the generator.
        foreach (Transform child in parent)
        {
            if (!UIBindEditor.ShouldBindPublic(child)) continue;

            // Always add the child's transform and gameobject itself to the main lists.
            transforms.Add(child);
            gameObjects.Add(child.gameObject);

            // Collect all standard components on this child.
            var uiComponents = UIBindEditor.GetUIComponentsPublic(child);
            foreach (var comp in uiComponents)
            {
                var compType = comp.GetType();
                if (!components.ContainsKey(compType))
                {
                    components[compType] = new System.Collections.Generic.List<Component>();
                }
                components[compType].Add(comp);
            }
            
            bool isNestedView = HasMonitoredViewComponent(child.gameObject);
            if (isNestedView)
            {
                // This is a nested view. Collect the view component itself.
                string viewClassName = child.name + "View";
                var viewComponent = child.GetComponent(viewClassName);
                if (viewComponent != null)
                {
                    var viewType = viewComponent.GetType();
                    if (!components.ContainsKey(viewType))
                    {
                        components[viewType] = new System.Collections.Generic.List<Component>();
                    }
                    components[viewType].Add(viewComponent);
                }
                
                // An important step: DO NOT recurse into the children of a nested view.
                continue;
            }

            // If it's not a nested view, recurse into its children.
            CollectReferences(child, transforms, gameObjects, components);
        }
    }

    private static void PopulateSerializedList(SerializedProperty listProp, System.Collections.Generic.List<Object> objects)
    {
        listProp.ClearArray();
        for (int i = 0; i < objects.Count; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            listProp.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
    }
    
    // --- METHODS COPIED FROM OLD SYSTEM ---

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

    public static void StartMonitoring(GameObject go, string scriptPath)
    {
        string objectPath = GetGameObjectPath(go.transform);
        int instanceId = objectPath.GetHashCode();

        if (!monitoredObjects.ContainsKey(instanceId))
        {
            string assetPath = "";
            string sceneGuid = "";
            
            // Correctly determine the asset path without climbing to the root of the prefab.
            if (PrefabUtility.IsPartOfPrefabAsset(go)) // This handles if 'go' is a prefab asset itself.
            {
                assetPath = AssetDatabase.GetAssetPath(go);
            }
            else // Could be a scene object, which might be a prefab instance.
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null) // It is a prefab instance.
                {
                    assetPath = AssetDatabase.GetAssetPath(source);
                }
            }

            var scene = go.scene;
            if (scene.IsValid())
            {
                sceneGuid = scene.path;
            }

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
        string scriptPath = GetGeneratedScriptPath(go);
        GenerateCode(go, scriptPath);
    }

    public static string GetGeneratedScriptPath(GameObject go)
    {
        Directory.CreateDirectory(GENERATED_FOLDER);
        string className = go.name + "View";
        return Path.Combine(GENERATED_FOLDER, className + ".cs");
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