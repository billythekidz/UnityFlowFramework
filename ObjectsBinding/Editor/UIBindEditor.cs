using UnityEditor;
using UnityEngine;
using System.Reflection;
using ObjectsBinding;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor.Callbacks;

/// <summary>
/// This custom editor automatically finds any MonoBehaviour with the [UIBind] attribute
/// and adds an "UPDATE UI BINDING" button to its Inspector.
/// It also contains public helper methods used by other editor scripts.
/// </summary>
[CustomEditor(typeof(MonoBehaviour), true)] // True = apply to all child classes of MonoBehaviour
public class UIBindEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Check if the target component has the UIBindAttribute
        var hasBindAttribute = target.GetType().GetCustomAttribute<UIBindAttribute>() != null;

        if (hasBindAttribute)
        {
            // Change the GUI color to make the button stand out
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // A nice green color

            // Draw the button at the top
            if (GUILayout.Button("UPDATE UI BINDING", GUILayout.Height(30)))
            {
                // When the button is clicked, call the logic from another script
                UIBindAutoMapping.RegenerateBindingsFor(target as MonoBehaviour);
            }

            // Reset the color to default
            GUI.backgroundColor = Color.white;

            // Add a space for visual separation
            EditorGUILayout.Space();
        }

        // Draw the default inspector afterwards
        DrawDefaultInspector();
    }

    /// <summary>
    /// Public helper to determine if a Transform should be included in the binding process.
    /// It checks for a UIBindIgnore component.
    /// </summary>
    public static bool ShouldBindPublic(Transform t)
    {
        if (t == null) return false;
        // Exclude objects with the UIBindIgnore component
        return t.GetComponent<UIBindIgnore>() == null;
    }

    /// <summary>
    /// Public helper to get all components from a Transform that should be bound.
    /// This now gets ALL components except for the Transform itself and the UIBind view script.
    /// </summary>
    public static List<Component> GetUIComponentsPublic(Transform t)
    {
        if (t == null) return new List<Component>();

        // Get all components and filter out the ones we don't want to create properties for.
        return t.GetComponents<Component>()
            .Where(c => c != null && !(c is Transform) && c.GetType().GetCustomAttribute<UIBindAttribute>() == null)
            .ToList();
    }

    [MenuItem("Tools/UI Binding/Start Binding This", priority = -1)]
    public static void GenerateSelected()
    {
        var selectedObject = Selection.activeObject as GameObject;
        if (selectedObject == null)
        {
            UIBindLogger.LogError("Please select a GameObject first!");
            return;
        }

        // Sanitize the default name to be a valid C# identifier
        string defaultName = selectedObject.name;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { ' ' });
        foreach (var c in invalidChars)
        {
            defaultName = defaultName.Replace(c.ToString(), "");
        }
        defaultName += "View";

        if (!char.IsLetter(defaultName[0]) && defaultName[0] != '_')
        {
            defaultName = "_" + defaultName;
        }

        ScriptNamePopup.ShowWindow(selectedObject, defaultName, ProceedWithBinding);
    }

    public static void ProceedWithBinding(GameObject selectedObject, string className)
    {
        // 1. Search for an existing script file across the entire project.
        string[] guids = AssetDatabase.FindAssets($"{className} t:script");
        string existingCsPath = guids.Select(AssetDatabase.GUIDToAssetPath)
                                     .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == className);

        if (!string.IsNullOrEmpty(existingCsPath))
        {
            // --- CASE 1: SCRIPT ALREADY EXISTS ---
            // Link the selected GameObject to the existing script and update its bindings.
            UIBindLogger.Log($"Found existing script at '{existingCsPath}'. Attaching and updating...");

            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(existingCsPath);
            if (scriptAsset == null) 
            {
                UIBindLogger.LogError($"Failed to load script asset at path: {existingCsPath}");
                return;
            }
            var scriptType = scriptAsset.GetClass();
            if (scriptType == null)
            {
                UIBindLogger.LogError($"Could not get class type from script asset: {scriptAsset.name}");
                return;
            }

            // Attach the component to the selected object if it doesn't already have it.
            var viewComponent = selectedObject.GetComponent(scriptType);
            if (viewComponent == null)
            {
                viewComponent = selectedObject.AddComponent(scriptType);
                UIBindLogger.Log($"✅ Attached component '{scriptType.Name}' to '{selectedObject.name}'.");
            }
            else
            {
                UIBindLogger.Log($"Component '{scriptType.Name}' already exists on '{selectedObject.name}'.");
            }
            
            // This is required for the code generator to find the newly attached component.
            EditorUtility.SetDirty(selectedObject);

            // Now that the component is attached, trigger the robust update flow.
            // We use delayCall to ensure this runs after the AddComponent operation is fully processed.
            EditorApplication.delayCall += () =>
            {
                UIBindAutoMapping.RegenerateBindingsFor(viewComponent as MonoBehaviour);
                
                // If we are in prefab mode, mark the stage as dirty to ensure changes are saved.
                var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && selectedObject.scene == prefabStage.scene)
                {
                     UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                     UIBindLogger.Log($"✏️ Marked prefab stage as dirty.");
                }
                else if(PrefabUtility.IsPartOfPrefabInstance(selectedObject))
                {
                    // For prefab instances in the main scene, apply the changes to the prefab asset.
                    PrefabUtility.ApplyPrefabInstance(selectedObject, InteractionMode.UserAction);
                    UIBindLogger.Log($"✅ Applied component changes to prefab instance.");
                }
            };
        }
        else
        {
            // --- CASE 2: SCRIPT DOES NOT EXIST (Original "Create New" Flow) ---
            UIBindLogger.Log($"Script '{className}.cs' not found. Creating new binding script.");
            
            string scriptPath;
            try
            {
                // Default location for new scripts. The user can move it later.
                string scriptDir = "Assets/ObjectsBinding/GeneratedUIView";
                if (!Directory.Exists(scriptDir))
                {
                    Directory.CreateDirectory(scriptDir);
                }
                scriptPath = Path.Combine(scriptDir, className + ".cs");
            }
            catch (System.Exception e)
            {
                UIBindLogger.LogError($"Error creating script path: {e.Message}");
                return;
            }

            // Create the .cs user file.
            if (!File.Exists(scriptPath))
            {
                string objectPath = GetGameObjectPath(selectedObject.transform);
                string scriptContent = GetScriptShellContent(className, objectPath);
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.ImportAsset(scriptPath);
                UIBindLogger.Log($"📝 Created empty script shell at {scriptPath}");
            }
            
            // The rest of this flow uses OnScriptsReloaded to attach the newly created script.
            string assetPath = "";
            
            // Determine which asset to attach to after scripts reload.
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                assetPath = prefabStage.assetPath;
                string relativePath = AnimationUtility.CalculateTransformPath(selectedObject.transform, prefabStage.prefabContentsRoot.transform);
                EditorPrefs.SetString("UIBind_PendingGOInPrefab_RelativePath", relativePath);
            }
            else
            {
                assetPath = AssetDatabase.GetAssetPath(selectedObject);
                if (string.IsNullOrEmpty(assetPath)) 
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(selectedObject);
                    if (source != null) {
                         assetPath = AssetDatabase.GetAssetPath(source);
                    } else {
                         EditorPrefs.SetString("UIBind_PendingGOScriptAttach_SceneObject", selectedObject.name);
                    }
                }
            }

            // Schedule for attachment after compilation.
            EditorPrefs.SetString("UIBind_PendingGOScriptAttach_Path", assetPath);
            EditorPrefs.SetString("UIBind_PendingScriptPath", scriptPath);

            // Start monitoring (uses the now-fixed version).
            UIBindAutoMapping.StartMonitoring(selectedObject, scriptPath);

            // Trigger compilation.
            UIBindLogger.Log("✨ Initial setup complete. Triggering compilation...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }

    #region Script Name Popup

    public class ScriptNamePopup : EditorWindow
    {
        private string _scriptName;
        private GameObject _selectedObject;
        private System.Action<GameObject, string> _onConfirm;

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
            "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
            "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static void ShowWindow(GameObject selectedObject, string defaultName, System.Action<GameObject, string> onConfirm)
        {
            var window = GetWindow<ScriptNamePopup>(true, "Enter Script Name", true);
            window._selectedObject = selectedObject;
            window._scriptName = defaultName;
            window._onConfirm = onConfirm;
            window.minSize = new Vector2(350, 120);
            window.maxSize = new Vector2(350, 120);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Enter the name for the new binding script:", EditorStyles.wordWrappedLabel);
            _scriptName = EditorGUILayout.TextField("Script Name", _scriptName);

            EditorGUILayout.Space();

            bool isValid = IsValidTypeName(_scriptName);
            GUI.enabled = isValid;

            if (GUILayout.Button("Create and Bind"))
            {
                _onConfirm?.Invoke(_selectedObject, _scriptName);
                Close();
            }
            GUI.enabled = true;

            if (!isValid)
            {
                EditorGUILayout.HelpBox("Script name must be a valid C# identifier and not a keyword.", MessageType.Warning);
            }
        }

        private bool IsValidTypeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (CSharpKeywords.Contains(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;
            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_') return false;
            }
            return true;
        }
    }

    #endregion

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

    private static string GetScriptShellContent(string className, string gameObjectPath)
    {
        return $@"using UnityEngine;
using ObjectsBinding;

// UIBind attribute - ScriptPath is auto-detected via CallerFilePath
// GameObjectPath: {{gameObjectPath}}
[UIBind]
public partial class {className} : MonoBehaviour
{{
    // This is a partial method declaration. The implementation is generated in the .g.cs file.
    partial void InitViewBinding();

    private void Awake()
    {{
        // Initialize UI bindings from generated code
        InitViewBinding();
    }}

    // Add your custom logic here
    private void Start()
    {{
        // Example: Access generated UI properties
        // BlackBG.gameObject.SetActive(true);
    }}
}}
";
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        string assetPath = EditorPrefs.GetString("UIBind_PendingGOScriptAttach_Path", "");
        string sceneObjectName = EditorPrefs.GetString("UIBind_PendingGOScriptAttach_SceneObject", "");
        string scriptPath = EditorPrefs.GetString("UIBind_PendingScriptPath", "");
        string relativePathInPrefab = EditorPrefs.GetString("UIBind_PendingGOInPrefab_RelativePath", "");

        if (string.IsNullOrEmpty(scriptPath) || (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(sceneObjectName)))
            return;

        // Clean up the keys now that we've read them
        EditorPrefs.DeleteKey("UIBind_PendingGOScriptAttach_Path");
        EditorPrefs.DeleteKey("UIBind_PendingGOScriptAttach_SceneObject");
        EditorPrefs.DeleteKey("UIBind_PendingScriptPath");
        EditorPrefs.DeleteKey("UIBind_PendingGOInPrefab_RelativePath");

        EditorApplication.delayCall += () =>
        {
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (scriptAsset == null) return;
            var scriptType = scriptAsset.GetClass();
            if (scriptType == null) return;

            GameObject targetGo = null;
            bool isPrefab = !string.IsNullOrEmpty(assetPath);

            if (isPrefab)
            {
                UIBindLogger.Log($"📦 Binding prefab asset: {assetPath}");
                
                var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && prefabStage.assetPath == assetPath)
                {
                    // We are in the prefab stage where the binding was initiated.
                    var rootTransform = prefabStage.prefabContentsRoot.transform;
                    if (!string.IsNullOrEmpty(relativePathInPrefab))
                    {
                        var childTransform = rootTransform.Find(relativePathInPrefab);
                        if (childTransform != null)
                        {
                            targetGo = childTransform.gameObject;
                            UIBindLogger.Log($"🎯 Found selected object '{targetGo.name}' within prefab via path: {relativePathInPrefab}");
                        }
                        else
                        {
                            UIBindLogger.LogWarning($"Could not find object at relative path '{relativePathInPrefab}'. Attaching to root '{prefabStage.prefabContentsRoot.name}' as a fallback.");
                            targetGo = prefabStage.prefabContentsRoot;
                        }
                    }
                    else
                    {
                        // No relative path saved, fall back to the root.
                        targetGo = prefabStage.prefabContentsRoot;
                        UIBindLogger.Log($"✏️ Attaching to prefab root in edit mode: {targetGo.name}");
                    }
                }
                else
                {
                    // Not in the prefab stage, load the asset directly.
                    // This logic branch handles attaching scripts to prefabs selected in the Project window.
                    targetGo = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    UIBindLogger.Log($"📦 Loading prefab asset directly: {assetPath}");
                }
                
                if (targetGo != null && targetGo.GetComponent(scriptType) == null)
                {
                    var newComponent = targetGo.AddComponent(scriptType) as MonoBehaviour;
                    UIBindLogger.Log($"✅ Attached {scriptType.Name} to prefab asset {targetGo.name}");

                    if (newComponent != null)
                    {
                        UIBindLogger.Log($"✨ Triggering initial binding generation for {newComponent.name}...");
                        UIBindAutoMapping.RegenerateBindingsFor(newComponent);
                    }
                    
                    var currentPrefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (currentPrefabStage != null && currentPrefabStage.assetPath == assetPath)
                    {
                        // Mark the prefab scene as dirty to ensure changes are saved.
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentPrefabStage.scene);
                        UIBindLogger.Log($"✏️ Marked prefab stage as dirty.");
                    }
                    else
                    {
                        // If we're not in a prefab stage, explicitly save the asset.
                        PrefabUtility.SavePrefabAsset(targetGo);
                        UIBindLogger.Log($"💾 Saved prefab asset: {assetPath}");
                    }
                    
                    UIBindLogger.Log($"🎯 Prefab binding complete. Scene instances will inherit this script automatically.");
                }
            }
            else // It's a scene object
            {
                targetGo = GameObject.Find(sceneObjectName);
                
                if (targetGo == null)
                {
                    UIBindLogger.LogError($"❌ Could not find scene object: {sceneObjectName}");
                    return;
                }

                if (targetGo.GetComponent(scriptType) == null)
                {
                    var newComponent = targetGo.AddComponent(scriptType) as MonoBehaviour;
                    UIBindLogger.Log($"✅ Attached {scriptType.Name} to scene object {targetGo.name}");
                    if (newComponent != null)
                    {
                        UIBindLogger.Log($"✨ Triggering initial binding generation for {newComponent.name}...");
                        UIBindAutoMapping.RegenerateBindingsFor(newComponent);
                    }
                }
            }
        };
    }
}
