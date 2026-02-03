using UnityEngine;
using Reflex.Core;

/// <summary>
/// Extension để ServiceLocator tự động theo Reflex scope hierarchy
/// </summary>
public static class ServiceLocatorExtensions
{
    /// <summary>
    /// Hook ServiceLocator vào Reflex Container (Global scope)
    /// </summary>
    public static void InstallServiceLocatorGlobal(this Container container)
    {
        ServiceLocatorReflex.InitializeScope(container, null); // No parent
        ServiceLocatorReflex.SetCurrentContainer(container);
        
        Debug.Log($"[ServiceLocator] Installed GLOBAL scope");
    }
    
    /// <summary>
    /// Hook ServiceLocator vào Reflex Container (Scene scope with parent)
    /// </summary>
    public static void InstallServiceLocatorScoped(this Container container, Container parentContainer)
    {
        ServiceLocatorReflex.InitializeScope(container, parentContainer);
        ServiceLocatorReflex.SetCurrentContainer(container);
        
        Debug.Log($"[ServiceLocator] Installed SCOPED scope with parent");
    }
}
