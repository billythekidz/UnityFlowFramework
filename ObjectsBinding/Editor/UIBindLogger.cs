using UnityEngine;

public static class UIBindLogger
{
    private const string Prefix = "[UIBind] ";
    public static bool IsEnabled = true;
    public static bool IsDebugEnabled = false; // Enable for detailed diagnostics

    public static void Log(object message)
    {
        if (IsEnabled)
        {
            Debug.Log(Prefix + message);
        }
    }

    public static void LogDebug(object message)
    {
        if (IsEnabled && IsDebugEnabled)
        {
            Debug.Log($"<color=cyan>{Prefix}[DEBUG] {message}</color>");
        }
    }

    public static void LogWarning(object message)
    {
        if (IsEnabled)
        {
            Debug.LogWarning(Prefix + message);
        }
    }

    public static void LogError(object message)
    {
        if (IsEnabled)
        {
            Debug.LogError(Prefix + message);
        }
    }
}
