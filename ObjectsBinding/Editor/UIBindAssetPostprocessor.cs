#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// Monitors asset movements (rename/move) and updates MonitoredObjects.json accordingly
/// </summary>
public class UIBindAssetPostprocessor : AssetPostprocessor
{
    private const string SYNC_FILE = "Assets/ObjectsBinding/Sync/MonitoredObjects.json";

    /// <summary>
    /// Called when assets are moved or renamed
    /// </summary>
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // Check if any View scripts were moved
        for (int i = 0; i < movedAssets.Length; i++)
        {
            string newPath = movedAssets[i];
            string oldPath = movedFromAssetPaths[i];

            // Only care about .cs files that end with "View.cs"
            if (newPath.EndsWith("View.cs") && oldPath.EndsWith("View.cs"))
            {
                UpdateMonitoredObjectsPath(oldPath, newPath);
            }
        }

        // Check if any View scripts were deleted
        foreach (string deletedPath in deletedAssets)
        {
            if (deletedPath.EndsWith("View.cs"))
            {
                RemoveFromMonitoredObjects(deletedPath);
            }
        }
    }

    /// <summary>
    /// Update script path in MonitoredObjects.json when file is moved
    /// </summary>
    private static void UpdateMonitoredObjectsPath(string oldPath, string newPath)
    {
        if (!File.Exists(SYNC_FILE)) return;

        try
        {
            string json = File.ReadAllText(SYNC_FILE);
            var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);

            if (wrapper?.list == null) return;

            // Find entry with old path
            var entry = wrapper.list.FirstOrDefault(info => info.scriptPath == oldPath);
            if (entry != null)
            {
                entry.scriptPath = newPath;
                
                // Save updated json
                File.WriteAllText(SYNC_FILE, JsonUtility.ToJson(wrapper, true));
                
                UIBindLogger.Log($"✅ Updated MonitoredObjects.json: {Path.GetFileName(oldPath)} → {newPath}");
            }
        }
        catch (System.Exception e)
        {
            UIBindLogger.LogError($"Failed to update MonitoredObjects.json: {e.Message}");
        }
    }

    /// <summary>
    /// Remove entry from MonitoredObjects.json when View script is deleted
    /// </summary>
    private static void RemoveFromMonitoredObjects(string scriptPath)
    {
        if (!File.Exists(SYNC_FILE)) return;

        try
        {
            string json = File.ReadAllText(SYNC_FILE);
            var wrapper = JsonUtility.FromJson<MonitoredListWrapper>(json);

            if (wrapper?.list == null) return;

            // Find and remove entry
            var entry = wrapper.list.FirstOrDefault(info => info.scriptPath == scriptPath);
            if (entry != null)
            {
                wrapper.list.Remove(entry);
                
                // Save updated json
                File.WriteAllText(SYNC_FILE, JsonUtility.ToJson(wrapper, true));
                
                UIBindLogger.Log($"🗑️ Removed from MonitoredObjects.json: {Path.GetFileName(scriptPath)}");
                
                // Also delete generated .g.cs file if exists
                string generatedPath = scriptPath.Replace(".cs", ".g.cs");
                if (File.Exists(generatedPath))
                {
                    File.Delete(generatedPath);
                    AssetDatabase.Refresh();
                    UIBindLogger.Log($"🗑️ Deleted generated file: {Path.GetFileName(generatedPath)}");
                }
            }
        }
        catch (System.Exception e)
        {
            UIBindLogger.LogError($"Failed to remove from MonitoredObjects.json: {e.Message}");
        }
    }

    [System.Serializable]
    private class MonitoredListWrapper
    {
        public System.Collections.Generic.List<MonitoredObjectInfo> list;
    }

    [System.Serializable]
    private class MonitoredObjectInfo
    {
        public string objectPath;
        public string scriptPath;
        public string lastHash;
    }
}
#endif
