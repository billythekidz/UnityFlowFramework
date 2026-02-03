﻿﻿﻿﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UIBind.DataModels;

namespace ObjectsBinding.Editor
{
    /// <summary>
    /// Fallback code generator that directly writes .g.cs files when Roslyn Source Generator fails.
    /// This bypasses Roslyn entirely and generates code at edit time.
    /// </summary>
    [InitializeOnLoad]
    public class FallbackCodeGenerator
    {
        private const string MONITORED_FILE = "Assets/ObjectsBinding/Sync/MonitoredObjects.json";
        
        // Index mapping for List-based references (shared across generation methods)
        private static Dictionary<string, int> s_indexMap;
        
        static FallbackCodeGenerator()
        {
            // Listen for scene changes to regenerate code
            EditorApplication.update += CheckForMonitoredObjectChanges;
        }

        private static System.DateTime lastCheckTime = System.DateTime.MinValue;
        private static Dictionary<string, string> lastObjectHashes = new Dictionary<string, string>();

        private static void CheckForMonitoredObjectChanges()
        {
            // Check every 0.5 seconds for instant feedback when editing prefabs
            if ((System.DateTime.Now - lastCheckTime).TotalSeconds < 0.5) return;
            lastCheckTime = System.DateTime.Now;

            // IMPORTANT: Skip during compilation or Play Mode
            if (EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPaused)
                return;

            if (!File.Exists(MONITORED_FILE)) return;

            try
            {
                // Read monitored objects
                string json = File.ReadAllText(MONITORED_FILE);
                var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
                
                if (wrapper?.list == null) return;

                foreach (var info in wrapper.list)
                {
                    GameObject go = GameObject.Find(info.objectPath);
                    if (go == null) continue;

                    // Check if hierarchy changed
                    string currentHash = UIBindCodeGenerator.GetHierarchyHash(go.transform);
                    if (!lastObjectHashes.ContainsKey(info.objectPath) || 
                        lastObjectHashes[info.objectPath] != currentHash)
                    {
                        lastObjectHashes[info.objectPath] = currentHash;
                        
                        // Regenerate code for this GameObject
                        string className = go.name + "View";
                        GenerateCodeForGameObject(go, className, info.scriptPath);
                    }
                }
            }
            catch (System.Exception e)
            {
                // Silently ignore errors
            }
        }

        [System.Serializable]
        private class MonitoredListWrapper 
        { 
            public List<MonitoredObjectInfo> list; 
        }

        [System.Serializable]
        private class MonitoredObjectInfo
        {
            public string objectPath;
            public string scriptPath;
            public string lastHash;
        }

        // [MenuItem("Tools/UI Binding/Force Regenerate All Code")]
        public static void ForceRegenerateAll()
        {
            if (!File.Exists(MONITORED_FILE))
            {
                Debug.LogWarning("[FallbackCodeGenerator] No monitored objects found.");
                return;
            }

            string json = File.ReadAllText(MONITORED_FILE);
            var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);

            if (wrapper?.list == null || wrapper.list.Count == 0)
            {
                Debug.LogWarning("[FallbackCodeGenerator] No monitored objects.");
                return;
            }

            foreach (var info in wrapper.list)
            {
                GameObject go = GameObject.Find(info.objectPath);
                if (go != null)
                {
                    string className = go.name + "View";
                    GenerateCodeForGameObject(go, className, info.scriptPath);
                }
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"[FallbackCodeGenerator] Regenerated code for {wrapper.list.Count} objects!");
        }

        public static void GenerateCodeForGameObject(GameObject go, string className, string sourceScriptPath)
        {
            if (go == null)
            {
                Debug.LogError("[FallbackCodeGenerator] GameObject is null!");
                return;
            }

            // Build hierarchy node from GameObject
            var rootNode = CreateHierarchyNode(go.transform);
            if (rootNode == null)
            {
                Debug.LogWarning($"[FallbackCodeGenerator] No bindable hierarchy for {go.name}");
                return;
            }

            GenerateCodeForClass(className, rootNode, sourceScriptPath);
        }

        private static HierarchyNode CreateHierarchyNode(Transform t)
        {
            if (!UIBindEditor.ShouldBindPublic(t)) return null;

            var node = new HierarchyNode
            {
                name = t.name,
                components = UIBindEditor.GetUIComponentsPublic(t).Select(c => c.GetType().Name).ToArray(),
                children = new List<HierarchyNode>()
            };

            foreach (Transform childTransform in t)
            {
                if (HasMonitoredViewComponent(childTransform.gameObject))
                {
                    // This child is a nested view. Get its standard components, but also add the view script itself
                    // as a component to be referenced. Then, stop recursion for this branch.
                    var componentNames = UIBindEditor.GetUIComponentsPublic(childTransform).Select(c => c.GetType().Name).ToList();
                    componentNames.Add(childTransform.name + "View");

                    var viewNode = new HierarchyNode
                    {
                        name = childTransform.name,
                        components = componentNames.Distinct().ToArray(),
                        children = new List<HierarchyNode>()
                    };
                    node.children.Add(viewNode);
                }
                else
                {
                    // This is a normal child, so recurse.
                    var childNode = CreateHierarchyNode(childTransform);
                    if (childNode != null)
                    {
                        node.children.Add(childNode);
                    }
                }
            }
            
            return node;
        }

        /// <summary>
        /// Check if GameObject has its own View component that's being monitored
        /// AND the View component still exists on the GameObject
        /// </summary>
        private static bool HasMonitoredViewComponent(GameObject go)
        {
            if (go == null) return false;

            // First check: Does GameObject actually have a View component?
            string expectedClassName = go.name + "View";
            var viewComponent = go.GetComponent(expectedClassName);
            
            if (viewComponent == null)
            {
                // No View component on GameObject - should not be treated as separate View
                // Remove from monitored list if it exists
                CleanupMissingViewFromMonitored(go);
                return false;
            }

            // Second check: Is this GameObject in MonitoredObjects.json?
            if (!File.Exists(MONITORED_FILE)) return false;

            try
            {
                string json = File.ReadAllText(MONITORED_FILE);
                var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
                
                if (wrapper?.list == null) return false;

                // Check if this GameObject path exists in monitored list
                string goPath = GetGameObjectPath(go.transform);
                return wrapper.list.Any(info => info.objectPath == goPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Remove GameObject from MonitoredObjects.json if its View component no longer exists
        /// </summary>
        private static void CleanupMissingViewFromMonitored(GameObject go)
        {
            if (!File.Exists(MONITORED_FILE)) return;

            try
            {
                string json = File.ReadAllText(MONITORED_FILE);
                var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
                
                if (wrapper?.list == null) return;

                string goPath = GetGameObjectPath(go.transform);
                var toRemove = wrapper.list.FirstOrDefault(info => info.objectPath == goPath);
                
                if (toRemove != null)
                {
                    wrapper.list.Remove(toRemove);
                    File.WriteAllText(MONITORED_FILE, JsonUtility.ToJson(wrapper, true));
                    
                    Debug.Log($"[FallbackCodeGenerator] Removed {go.name} from monitored objects (View component deleted)");
                    
                    // Trigger regeneration of parent Views to include this hierarchy
                    TriggerParentViewRegeneration(go);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FallbackCodeGenerator] Failed to cleanup monitored object: {e.Message}");
            }
        }

        /// <summary>
        /// Find and trigger regeneration of parent Views when a child View is removed
        /// </summary>
        private static void TriggerParentViewRegeneration(GameObject go)
        {
            if (!File.Exists(MONITORED_FILE)) return;

            try
            {
                string json = File.ReadAllText(MONITORED_FILE);
                var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);
                
                if (wrapper?.list == null) return;

                // Find parent GameObject that might contain this as a child
                Transform current = go.transform.parent;
                while (current != null)
                {
                    string parentPath = GetGameObjectPath(current);
                    var parentInfo = wrapper.list.FirstOrDefault(info => info.objectPath == parentPath);
                    
                    if (parentInfo != null)
                    {
                        // Found parent with View - trigger regeneration
                        GameObject parentGO = GameObject.Find(parentPath);
                        if (parentGO != null)
                        {
                            string className = parentGO.name + "View";
                            Debug.Log($"[FallbackCodeGenerator] Triggering regeneration of {className} to include {go.name} hierarchy");
                            
                            EditorApplication.delayCall += () => {
                                GenerateCodeForGameObject(parentGO, className, parentInfo.scriptPath);
                            };
                        }
                        break;
                    }
                    
                    current = current.parent;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FallbackCodeGenerator] Failed to trigger parent regeneration: {e.Message}");
            }
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

        private static void GenerateCodeForClass(string className, HierarchyNode rootNode, string sourceScriptPath)
        {
            try
            {
                if (rootNode == null) return;

                // Generate the .g.cs file next to the original .cs file
                string outputPath = sourceScriptPath.Replace(".cs", ".g.cs");
                string sourceCode = GenerateSourceCode(className, rootNode);

                // Check if file needs update
                bool needsUpdate = true;
                if (File.Exists(outputPath))
                {
                    string existingContent = File.ReadAllText(outputPath);
                    // Don't update if only timestamp changed
                    needsUpdate = !CompareCodeIgnoringTimestamp(existingContent, sourceCode);
                }

                if (needsUpdate)
                {
                    File.WriteAllText(outputPath, sourceCode);
                    Debug.Log($"[FallbackCodeGenerator] Generated code for {className}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FallbackCodeGenerator] CRASHED while generating code for {className}. Error: {e}");
            }
        }

        private static bool CompareCodeIgnoringTimestamp(string code1, string code2)
        {
            // Remove timestamp line for comparison
            var lines1 = code1.Split('\n').Where(l => !l.Contains("Auto-generated") && !l.Contains("at 20")).ToArray();
            var lines2 = code2.Split('\n').Where(l => !l.Contains("Auto-generated") && !l.Contains("at 20")).ToArray();
            return string.Join("\n", lines1) == string.Join("\n", lines2);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "UnnamedObject";
            
            // Replace spaces and special characters with underscores
            var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
            
            // Ensure it doesn't start with a number
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            
            // Remove consecutive underscores
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"_+", "_");
            
            // Remove trailing/leading underscores
            sanitized = sanitized.Trim('_');
            
            return string.IsNullOrEmpty(sanitized) ? "UnnamedObject" : sanitized;
        }

        private static string GenerateSourceCode(string className, HierarchyNode rootNode)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ObjectsBinding;");
            sb.AppendLine();
            sb.AppendLine($"// Auto-generated by FallbackCodeGenerator at {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            sb.AppendLine($"public partial class {className}");
            sb.AppendLine("{");
            sb.AppendLine("#region Auto Generated Code For UI Bindings");
            
            // 1. Prepare data: unique names and component reference maps
            var nodeNameMap = new Dictionary<HierarchyNode, string>();
            var usedNames = new HashSet<string>();
            AssignUniqueNamesRecursive(rootNode, usedNames, nodeNameMap);

            s_indexMap = new Dictionary<string, int>();
            var transformsList = new List<string>();
            var gameObjectsList = new List<string>();
            var componentsByType = new Dictionary<string, List<string>>();
            CollectAllReferencesByType(rootNode, transformsList, gameObjectsList, componentsByType, s_indexMap, nodeNameMap);
            
            // 2. Generate serialized fields and public getters
            GenerateFieldsAndGetters(sb, transformsList, gameObjectsList, componentsByType);

            // 3. Generate public properties for top-level scopes
            GenerateRootScopeProperties(sb, rootNode, nodeNameMap);
            
            // 4. Generate InitViewBinding and MappingReferences methods
            GenerateInitLogic(sb, rootNode, nodeNameMap);

            // 5. Generate all nested scope classes
            GenerateNestedClasses(sb, className, rootNode, nodeNameMap);

            sb.AppendLine();
            sb.AppendLine("#endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AssignUniqueNamesRecursive(HierarchyNode node, HashSet<string> usedNames, Dictionary<HierarchyNode, string> nodeNameMap)
        {
            if (node == null) return;

            string safeName = SanitizeName(node.name);
            string uniqueName = safeName;
            int counter = 1;

            while (usedNames.Contains(uniqueName))
            {
                counter++;
                uniqueName = $"{safeName}{counter}";
            }

            usedNames.Add(uniqueName);
            nodeNameMap[node] = uniqueName;

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    AssignUniqueNamesRecursive(child, usedNames, nodeNameMap);
                }
            }
        }
        
        private static void CollectAllReferencesByType(
            HierarchyNode parent, 
            List<string> transforms, 
            List<string> gameObjects, 
            Dictionary<string, List<string>> componentsByType,
            Dictionary<string, int> indexMap,
            Dictionary<HierarchyNode, string> nodeNameMap)
        {
            if (parent.children == null) return;
            
            foreach (var child in parent.children)
            {
                string uniqueName = nodeNameMap[child];
                
                string transformKey = $"{uniqueName}_Transform";
                if (!indexMap.ContainsKey(transformKey))
                {
                    indexMap[transformKey] = transforms.Count;
                    transforms.Add(transformKey);
                }

                string gameObjectKey = $"{uniqueName}_GO";
                 if (!indexMap.ContainsKey(gameObjectKey))
                {
                    indexMap[gameObjectKey] = gameObjects.Count;
                    gameObjects.Add(gameObjectKey);
                }

                if (child.components != null)
                {
                    foreach (var compName in child.components)
                    {
                        if (string.IsNullOrEmpty(compName)) continue;
                        if (!componentsByType.ContainsKey(compName))
                        {
                            componentsByType[compName] = new List<string>();
                        }
                        
                        string componentKey = $"{uniqueName}_{compName}";
                        if (!indexMap.ContainsKey(componentKey))
                        {
                            indexMap[componentKey] = componentsByType[compName].Count;
                            componentsByType[compName].Add(componentKey);
                        }
                    }
                }
                
                // Recurse for non-view-ref children (CreateHierarchyNode already prevents recursion into view contents)
                CollectAllReferencesByType(child, transforms, gameObjects, componentsByType, indexMap, nodeNameMap);
            }
        }
        
        private static void GenerateFieldsAndGetters(StringBuilder sb, List<string> transforms, List<string> gameObjects, Dictionary<string, List<string>> componentsByType)
        {
            sb.AppendLine();
            sb.AppendLine("    // ============ Serialized References ============");
            sb.AppendLine($"    [SerializeField] private List<Transform> _transforms = new List<Transform>({transforms.Count});");
            sb.AppendLine($"    [SerializeField] private List<GameObject> _gameObjects = new List<GameObject>({gameObjects.Count});");

            foreach (var kvp in componentsByType.OrderBy(x => x.Key))
            {
                string typeName = kvp.Key;
                if (string.IsNullOrEmpty(typeName)) continue;
                string listName = $"_{typeName.ToLowerInvariant()}s";
                sb.AppendLine($"    [SerializeField] private List<{typeName}> {listName} = new List<{typeName}>({kvp.Value.Count});");
            }
            
            sb.AppendLine();
            sb.AppendLine("    // ============ Public Component Getters ============");
            sb.AppendLine("    public Transform GetTransform(int index) => _transforms[index];");
            sb.AppendLine("    public GameObject GetGameObject(int index) => _gameObjects[index];");

            foreach (var kvp in componentsByType.OrderBy(x => x.Key))
            {
                string typeName = kvp.Key;
                if (string.IsNullOrEmpty(typeName)) continue;
                string methodName = GetMethodName(typeName);
                sb.AppendLine($"    public {typeName} Get{methodName}(int index) => _{typeName.ToLowerInvariant()}s[index];");
            }
        }

        private static void GenerateRootScopeProperties(StringBuilder sb, HierarchyNode root, Dictionary<HierarchyNode, string> nodeNameMap)
        {
            sb.AppendLine();
            sb.AppendLine("    // ============ Public Nested Scope Properties ============");
            if (root.children != null)
            {
                foreach (var child in root.children)
                {
                    string uniqueName = nodeNameMap[child];
                    string expectedViewName = child.name + "View";
                    bool isViewRef = child.components.Length == 1 && child.components[0] == expectedViewName;
        
                    if (isViewRef)
                    {
                        sb.AppendLine($"    public {expectedViewName} {uniqueName} {{ get; private set; }}");
                    }
                    else
                    {
                        string scopeName = uniqueName + "Scope";
                        sb.AppendLine($"    public {scopeName} {uniqueName} {{ get; private set; }}");
                    }
                }
            }
        }

        private static void GenerateInitLogic(StringBuilder sb, HierarchyNode parent, Dictionary<HierarchyNode, string> nodeNameMap)
        {
            sb.AppendLine();
            sb.AppendLine("    partial void InitViewBinding()");
            sb.AppendLine("    {");
            sb.AppendLine("        MappingReferences();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void MappingReferences()");
            sb.AppendLine("    {");

            if (parent.children != null)
            {
                foreach (var child in parent.children)
                {
                    string uniqueName = nodeNameMap[child];
                    string expectedViewName = child.name + "View";
                    bool isViewRef = child.components.Length == 1 && child.components[0] == expectedViewName;

                    if (isViewRef)
                    {
                        string viewCompName = child.components[0];
                        string viewMethodName = GetMethodName(viewCompName);
                        string viewComponentKey = $"{uniqueName}_{viewCompName}";
                        int viewCompIdx = s_indexMap.ContainsKey(viewComponentKey) ? s_indexMap[viewComponentKey] : -1;
                        
                        sb.AppendLine($"        this.{uniqueName} = this.Get{viewMethodName}({viewCompIdx});");
                    }
                    else
                    {
                        string scopeName = uniqueName + "Scope";
                        sb.AppendLine($"        this.{uniqueName} = new {scopeName}(this);");
                    }
                }
            }
            
            sb.AppendLine("    }");
        }

        private static void GenerateNestedClasses(StringBuilder sb, string rootClassName, HierarchyNode parent, Dictionary<HierarchyNode, string> nodeNameMap)
        {
            if (parent.children == null) return;
            
            foreach (var child in parent.children)
            {
                string uniqueName = nodeNameMap[child];
                string expectedViewName = child.name + "View";
                bool isViewRef = child.components.Length == 1 && child.components[0] == expectedViewName;

                if (isViewRef)
                {
                    // This is a reference to another View, not a scope to be generated.
                    continue;
                }

                string scopeName = uniqueName + "Scope";
                
                sb.AppendLine();
                sb.AppendLine($"    public class {scopeName}");
                sb.AppendLine("    {");
                
                sb.AppendLine($"        private readonly {rootClassName} _root;");
                sb.AppendLine();
                
                sb.AppendLine("        // Component Fields");
                sb.AppendLine("        public readonly GameObject gameObject;");
                sb.AppendLine("        public readonly Transform transform;");
                if (child.components != null)
                {
                    foreach (var compName in child.components) 
                    {
                        if (string.IsNullOrEmpty(compName)) continue;
                        string fieldName = GetComponentFieldName(compName);
                        sb.AppendLine($"        public readonly {compName} {fieldName};");
                    }
                }
                
                sb.AppendLine();
                sb.AppendLine("        // Child Scopes & Views");
                if (child.children != null)
                {
                    foreach (var grandChild in child.children)
                    {
                        string grandChildUniqueName = nodeNameMap[grandChild];
                        string grandChildExpectedViewName = grandChild.name + "View";
                        bool isGrandChildViewRef = grandChild.components.Length == 1 && grandChild.components[0] == grandChildExpectedViewName;

                        if (isGrandChildViewRef)
                        {
                             sb.AppendLine($"        public {grandChildExpectedViewName} {grandChildUniqueName} {{ get; private set; }}");
                        }
                        else
                        {
                            string grandChildScopeName = grandChildUniqueName + "Scope";
                            sb.AppendLine($"        public {grandChildScopeName} {grandChildUniqueName} {{ get; private set; }}");
                        }
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"        public {scopeName}({rootClassName} root)");
                sb.AppendLine("        {");
                GenerateConstructorBody(sb, rootClassName, child, nodeNameMap);
                sb.AppendLine("        }");
                sb.AppendLine("    }");

                // Recurse for grandchildren that are not view refs
                GenerateNestedClasses(sb, rootClassName, child, nodeNameMap);
            }
        }

        private static string GetMethodName(string typeName)
        {
            // For "TMP_Text", this should return "TMP_Text" to be used as "GetTMP_Text"
            return typeName.Replace("_", "");
        }

        private static string GetComponentFieldName(string compName)
        {
            if (string.IsNullOrEmpty(compName)) return "component";
            
            if (compName.StartsWith("TMP_"))
            {
                return "tmp" + compName.Substring(4).ToLowerInvariant();
            }

            if (compName.Length > 1 && char.IsUpper(compName[0]) && char.IsLower(compName[1]))
            {
                 return char.ToLower(compName[0]) + compName.Substring(1);
            }
            
            return compName.ToLowerInvariant();
        }
        
        private static void GenerateConstructorBody(StringBuilder sb, string rootClassName, HierarchyNode parent, Dictionary<HierarchyNode, string> nodeNameMap)
        {
            string uniqueName = nodeNameMap[parent];

            sb.AppendLine($"            this._root = root;");
            sb.AppendLine();

            string transformKey = $"{uniqueName}_Transform";
            int transformIdx = s_indexMap.ContainsKey(transformKey) ? s_indexMap[transformKey] : -1;
            sb.AppendLine($"            this.transform = _root.GetTransform({transformIdx});");

            string goKey = $"{uniqueName}_GO";
            int goIdx = s_indexMap.ContainsKey(goKey) ? s_indexMap[goKey] : -1;
            sb.AppendLine($"            this.gameObject = _root.GetGameObject({goIdx});");

            if (parent.components != null)
            {
                foreach (var compName in parent.components)
                {
                    if (string.IsNullOrEmpty(compName)) continue;
                    string fieldName = GetComponentFieldName(compName);
                    string methodName = GetMethodName(compName);
                    string componentKey = $"{uniqueName}_{compName}";
                    int compIdx = s_indexMap.ContainsKey(componentKey) ? s_indexMap[componentKey] : -1;
                    sb.AppendLine($"            this.{fieldName} = _root.Get{methodName}({compIdx});");
                }
            }

            if (parent.children != null)
            {
                sb.AppendLine();
                foreach (var child in parent.children)
                {
                    string childUniqueName = nodeNameMap[child];
                    string childExpectedViewName = child.name + "View";
                    bool isChildViewRef = child.components.Length == 1 && child.components[0] == childExpectedViewName;

                    if (isChildViewRef)
                    {
                        string viewCompName = child.components[0];
                        string viewMethodName = GetMethodName(viewCompName);
                        string viewComponentKey = $"{childUniqueName}_{viewCompName}";
                        int viewCompIdx = s_indexMap.ContainsKey(viewComponentKey) ? s_indexMap[viewComponentKey] : -1;
                        sb.AppendLine($"            this.{childUniqueName} = _root.Get{viewMethodName}({viewCompIdx});");
                    }
                    else
                    {
                        string childScopeName = childUniqueName + "Scope";
                        sb.AppendLine($"            this.{childUniqueName} = new {childScopeName}(_root);");
                    }
                }
            }
        }
    }
}
#endif
