using System;
using System.Runtime.CompilerServices;

namespace ObjectsBinding
{
    /// <summary>
    /// Marks a MonoBehaviour class as using UI Binding framework.
    /// This attribute enables:
    /// - Auto-discovery of UI-bound classes
    /// - Health checks and integrity validation
    /// - Recovery if MonitoredObjects.json is lost
    ///
    /// UNIQUE IDENTIFIER: Uses script path (auto-detected via CallerFilePath)
    ///
    /// Usage Examples:
    ///
    ///    [UIBind]
    ///    public partial class CanvasView : MonoBehaviour { }
    ///
    /// Note: ScriptPath is ALWAYS auto-detected via CallerFilePath - no manual input needed!
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UIBindAttribute : Attribute
    {
        /// <summary>
        /// UNIQUE identifier: Script file path (auto-detected)
        /// This is guaranteed unique across the project - no collisions possible
        /// </summary>
        public string ScriptPath { get; }

        /// <summary>
        /// Hierarchy path of the GameObject this View is bound to (e.g., "Canvas", "UI/MainPanel")
        /// NOTE: This can be ambiguous (multiple scenes/prefabs can have same path)
        /// Used for display/search only, NOT as unique key
        /// OPTIONAL: Can be left empty and populated later by the framework
        /// </summary>
        public string GameObjectPath { get; set; }

        /// <summary>
        /// Scene or Prefab asset path where this GameObject exists
        /// Helps distinguish between different instances
        /// OPTIONAL: Can be left empty and populated later by the framework
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// Create UIBind attribute - script path is auto-detected
        ///
        /// Usage: [UIBind] or [UIBind(GameObjectPath = "Canvas")]
        /// </summary>
        /// <param name="scriptPath">Auto-filled by compiler via CallerFilePath - DO NOT provide manually</param>
        public UIBindAttribute([CallerFilePath] string scriptPath = "")
        {
            ScriptPath = scriptPath;
            GameObjectPath = string.Empty;
            AssetPath = string.Empty;
        }
    }
    
    /// <summary>
    /// [DEPRECATED] Use UIBindAttribute instead.
    /// Kept for backward compatibility.
    /// </summary>
    [Obsolete("Use [UIBind] attribute instead.", false)]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateObjectBindingAttribute : Attribute
    {
    }
}

