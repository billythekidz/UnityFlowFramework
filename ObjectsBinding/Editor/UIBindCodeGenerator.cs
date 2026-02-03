#if UNITY_EDITOR
using UnityEngine;
using System.Text;

/// <summary>
/// Calculates a hierarchy hash for the UI Binding system.
/// </summary>
public static class UIBindCodeGenerator
{
    /// <summary>
    /// Gets a SHA256 hash representing the bindable hierarchy structure.
    /// </summary>
    public static string GetHierarchyHash(Transform root)
    {
        var sb = new StringBuilder();
        CollectHierarchyInfo(root, sb, 0);
        return HashString(sb.ToString());
    }

    private static void CollectHierarchyInfo(Transform t, StringBuilder sb, int depth)
    {
        // Use the public ShouldBind method from UIBindEditor
        if (!UIBindEditor.ShouldBindPublic(t))
            return;

        sb.Append($"{depth}:{t.name}:{t.childCount}");
        
        // Add components info
        var comps = UIBindEditor.GetUIComponentsPublic(t);
        foreach (var c in comps)
        {
            sb.Append($":{c.GetType().Name}");
        }
        sb.Append(";");

        // Recurse through children
        foreach (Transform child in t)
        {
            CollectHierarchyInfo(child, sb, depth + 1);
        }
    }

    private static string HashString(string input)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hash = sha.ComputeHash(bytes);
            return System.Convert.ToBase64String(hash);
        }
    }
}
#endif
