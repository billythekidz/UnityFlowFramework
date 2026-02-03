
using System;
using System.Collections.Generic;
using UnityEngine;
using Reflex.Core;

/// <summary>
/// ServiceLocator tự động theo Reflex scope hierarchy
/// </summary>
public class ServiceLocatorScope
{
	private Dictionary<Type, object> _services = new();
	private ServiceLocatorScope _parent;
	
	public ServiceLocatorScope(ServiceLocatorScope parent = null)
	{
		_parent = parent;
	}
	
	public void Register<T>(T service)
	{
		_services[typeof(T)] = service;
		Debug.Log($"[ServiceLocator] Registered {typeof(T).Name}");
	}
	
	public T Get<T>()
	{
		// Tìm trong scope hiện tại
		if (_services.TryGetValue(typeof(T), out var service))
		{
			return (T)service;
		}
		
		// Tìm trong parent scope (giống Reflex)
		if (_parent != null)
		{
			return _parent.Get<T>();
		}
		
		throw new Exception($"[ServiceLocator] Service {typeof(T).Name} not found!");
	}
	
	public bool TryGet<T>(out T service)
	{
		if (_services.TryGetValue(typeof(T), out var obj))
		{
			service = (T)obj;
			return true;
		}
		
		if (_parent != null)
		{
			return _parent.TryGet(out service);
		}
		
		service = default;
		return false;
	}
	
	public void Clear()
	{
		_services.Clear();
	}
}

/// <summary>
/// Static accessor tự động map theo Container hiện tại
/// </summary>
public static class ServiceLocatorReflex
{
	private static Dictionary<Container, ServiceLocatorScope> _scopeMap = new();
	private static ServiceLocatorScope _globalScope = new ServiceLocatorScope();
	
	/// <summary>
	/// Tự động tạo scope khi Container được build
	/// </summary>
	public static void InitializeScope(Container container, Container parentContainer = null)
	{
		if (_scopeMap.ContainsKey(container))
			return;
		
		ServiceLocatorScope parentScope = parentContainer != null && _scopeMap.TryGetValue(parentContainer, out var ps) 
			? ps 
			: _globalScope;
		
		var scope = new ServiceLocatorScope(parentScope);
		_scopeMap[container] = scope;
		
		Debug.Log($"[ServiceLocator] Scope initialized for {container.GetHashCode()}");
	}
	
	/// <summary>
	/// Cleanup scope khi Container dispose
	/// </summary>
	public static void DisposeScope(Container container)
	{
		if (_scopeMap.TryGetValue(container, out var scope))
		{
			scope.Clear();
			_scopeMap.Remove(container);
			Debug.Log($"[ServiceLocator] Scope disposed for {container.GetHashCode()}");
		}
	}
	
	/// <summary>
	/// Register service vào scope của container
	/// </summary>
	public static void Register<T>(Container container, T service)
	{
		if (!_scopeMap.TryGetValue(container, out var scope))
		{
			InitializeScope(container);
			scope = _scopeMap[container];
		}
		
		scope.Register(service);
	}
	
	/// <summary>
	/// Get service từ scope của container (với parent fallback)
	/// </summary>
	public static T Get<T>(Container container)
	{
		if (_scopeMap.TryGetValue(container, out var scope))
		{
			return scope.Get<T>();
		}
		
		// Fallback to global
		return _globalScope.Get<T>();
	}
	
	/// <summary>
	/// Register vào global scope
	/// </summary>
	public static void RegisterGlobal<T>(T service)
	{
		_globalScope.Register(service);
	}
	
	/// <summary>
	/// Get từ global scope
	/// </summary>
	public static T GetGlobal<T>()
	{
		return _globalScope.Get<T>();
	}
	
	// ==================== CONVENIENCE API ====================
	
	private static Container _currentContainer;
	
	/// <summary>
	/// Set container hiện tại (tự động gọi từ LifetimeScope)
	/// </summary>
	public static void SetCurrentContainer(Container container)
	{
		_currentContainer = container;
		Debug.Log($"[ServiceLocator] Current container set to {container?.GetHashCode()}");
	}
	
	/// <summary>
	/// Register vào current scope (không cần truyền Container)
	/// </summary>
	public static void Register<T>(T service)
	{
		if (_currentContainer == null)
		{
			Debug.LogWarning("[ServiceLocator] No current container, registering to global");
			RegisterGlobal(service);
			return;
		}
		
		Register(_currentContainer, service);
	}
	
	/// <summary>
	/// Get từ current scope (với parent fallback)
	/// </summary>
	public static T Get<T>()
	{
		if (_currentContainer == null)
		{
			Debug.LogWarning("[ServiceLocator] No current container, getting from global");
			return GetGlobal<T>();
		}
		
		return Get<T>(_currentContainer);
	}
	
	/// <summary>
	/// Try get từ current scope
	/// </summary>
	public static bool TryGet<T>(out T service)
	{
		if (_currentContainer == null || !_scopeMap.TryGetValue(_currentContainer, out var scope))
		{
			return _globalScope.TryGet(out service);
		}
		
		return scope.TryGet(out service);
	}
}
