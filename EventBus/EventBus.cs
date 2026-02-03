using System;
using System.Collections.Generic;
using UnityEngine;

// IEventBus now implements IDisposable
public interface IEventBus : IDisposable
{
    // Command-Based (For Server Events)
    void Subscribe<T>(string command, Action<T> callback);
    void Subscribe(string command, Action callback);
    void Unsubscribe<T>(string command, Action<T> callback);
    void Unsubscribe(string command, Action callback);
    void Publish<T>(string command, T payload);
    void Publish(string command);

    // Type-Based (For Client-Side Events)
    void Subscribe<T>(Action<T> callback);
    void Subscribe<T>(Action callback);
    void Unsubscribe<T>(Action<T> callback);
    void Unsubscribe<T>(Action callback);
    void Publish<T>(T payload);
}

public class EventBusGlobal : EventBus {}

/// <summary>
/// A universal event bus that supports both command-based (string) and type-based subscriptions.
/// - Use command-based for server events (via Commands.cs).
/// - Use type-based for internal client events (via a dedicated ClientEvents.cs).
/// </summary>
// EventBus now implements IDisposable
public abstract class EventBus : IEventBus
{
    
    #region Command-Based (For Server Events)
    private readonly Dictionary<string, Delegate> _commandSubscribers = new();
    private readonly Dictionary<string, Action> _commandActionSubscribers = new();

    /// <summary>
    /// Subscribe to a command-based event with a payload (ideal for server commands).
    /// </summary>
    public void Subscribe<T>(string command, Action<T> callback)
    {
        if (!_commandSubscribers.ContainsKey(command))
        {
            _commandSubscribers[command] = null;
        }
        if (_commandSubscribers[command] != null && _commandSubscribers[command].GetType() != typeof(Action<T>))
        {
            Debug.LogError($"[EventBus] Attempted to subscribe to command '{command}' with type '{typeof(T).Name}', but it's already registered with a different payload type.");
            return;
        }
        _commandSubscribers[command] = Delegate.Combine(_commandSubscribers[command], callback);
    }

    /// <summary>
    /// Subscribe to a command-based event without a payload.
    /// </summary>
    public void Subscribe(string command, Action callback)
    {
        if (!_commandActionSubscribers.ContainsKey(command))
        {
            _commandActionSubscribers[command] = null;
        }
        _commandActionSubscribers[command] += callback;
    }

    /// <summary>
    /// Unsubscribe from a command-based event with a payload.
    /// </summary>
    public void Unsubscribe<T>(string command, Action<T> callback)
    {
        if (_commandSubscribers.TryGetValue(command, out var d))
        {
            _commandSubscribers[command] = Delegate.Remove(d, callback);
        }
    }

    /// <summary>
    /// Unsubscribe from a command-based event without a payload.
    /// </summary>
    public void Unsubscribe(string command, Action callback)
    {
        if (_commandActionSubscribers.TryGetValue(command, out _))
        {
            _commandActionSubscribers[command] -= callback;
        }
    }

    /// <summary>
    /// Publish a command-based event with a payload.
    /// </summary>
    public void Publish<T>(string command, T payload)
    {
        if (!_commandSubscribers.TryGetValue(command, out var subscribersDelegate) || subscribersDelegate == null) return;
        var invocationList = subscribersDelegate.GetInvocationList();
        foreach (var listener in invocationList)
        {
            if (listener.Target is UnityEngine.Object unityObject && unityObject == null)
            {
                _commandSubscribers[command] = Delegate.Remove(_commandSubscribers[command], listener);
                continue;
            }
            try
            {
                (listener as Action<T>)?.Invoke(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] Error invoking listener for command '{command}'. Error: {e}");
            }
        }
    }

    /// <summary>
    /// Publish a command-based event without a payload.
    /// </summary>
    public void Publish(string command)
    {
        if (!_commandActionSubscribers.TryGetValue(command, out var action) || action == null) return;
        var invocationList = action.GetInvocationList();
        foreach (var listener in invocationList)
        {
            if (listener.Target is UnityEngine.Object unityObject && unityObject == null)
            {
                _commandActionSubscribers[command] -= (Action)listener;
                continue;
            }
            try
            {
                (listener as Action)?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventBus] Error while invoking listener for command '{command}': {e}");
            }
        }
    }
    #endregion

    #region Type-Based (For Client-Side Events)

    private readonly Dictionary<Type, Delegate> _typeSubscribers = new();
    private readonly Dictionary<Type, Action> _typeActionSubscribers = new();

    /// <summary>
    /// Subscribe to a type-based event (ideal for client-side events).
    /// The event is identified by its data type.
    /// </summary>
    /// <param name="callback">The action to execute when the event is published.</param>
    /// <typeparam name="T">The event type, which is also the payload.</typeparam>
    public void Subscribe<T>(Action<T> callback)
    {
        var type = typeof(T);
        if (!_typeSubscribers.ContainsKey(type))
        {
            _typeSubscribers[type] = null;
        }
        _typeSubscribers[type] = Delegate.Combine(_typeSubscribers[type], callback);
    }
    
    /// <summary>
    /// Subscribe to a type-based event without consuming its payload.
    /// </summary>
    public void Subscribe<T>(Action callback)
    {
        var type = typeof(T);
        if (!_typeActionSubscribers.ContainsKey(type))
        {
            _typeActionSubscribers[type] = null;
        }
        _typeActionSubscribers[type] += callback;
    }

    /// <summary>
    /// Unsubscribe from a type-based event.
    /// </summary>
    public void Unsubscribe<T>(Action<T> callback)
    {
        var type = typeof(T);
        if (_typeSubscribers.TryGetValue(type, out var d))
        {
            _typeSubscribers[type] = Delegate.Remove(d, callback);
        }
    }
    
    /// <summary>
    /// Unsubscribe from a parameterless type-based event.
    /// </summary>
    public void Unsubscribe<T>(Action callback)
    {
        var type = typeof(T);
        if (_typeActionSubscribers.ContainsKey(type))
        {
            _typeActionSubscribers[type] -= callback;
        }
    }

    /// <summary>
    /// Publish a type-based event.
    /// </summary>
    /// <param name="payload">The event object itself.</param>
    /// <typeparam name="T">The type of the event.</typeparam>
    public void Publish<T>(T payload)
    {
        var type = typeof(T);

        // Invoke subscribers that take a payload
        if (_typeSubscribers.TryGetValue(type, out var subscribersDelegate) && subscribersDelegate != null)
        {
            var invocationList = subscribersDelegate.GetInvocationList();
            foreach (var listener in invocationList)
            {
                if (listener.Target is UnityEngine.Object unityObject && unityObject == null)
                {
                    _typeSubscribers[type] = Delegate.Remove(_typeSubscribers[type], listener);
                    Debug.Log($"[EventBus] Removed a stale listener for event type '{type.Name}'.");
                    continue;
                }

                try
                {
                    (listener as Action<T>)?.Invoke(payload);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EventBus] Error while invoking listener for event type '{type.Name}': {e}");
                }
            }
        }
        
        // Invoke parameterless subscribers
        if (_typeActionSubscribers.TryGetValue(type, out var action) && action != null)
        {
            var invocationList = action.GetInvocationList();
            foreach (var listener in invocationList)
            {
                if (listener.Target is UnityEngine.Object unityObject && unityObject == null)
                {
                    _typeActionSubscribers[type] -= (Action)listener;
                    continue;
                }
                try
                {
                    (listener as Action)?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EventBus] Error while invoking parameterless listener for event type '{type.Name}': {e}");
                }
            }
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Clears all subscriptions from the event bus.
    /// This is crucial for scene-scoped buses to prevent memory leaks when a scene is unloaded.
    /// </summary>
    public void Dispose()
    {
        _commandSubscribers.Clear();
        _commandActionSubscribers.Clear();
        _typeSubscribers.Clear();
        _typeActionSubscribers.Clear();
        Debug.Log("[EventBus] Disposed and cleared all subscriptions.");
    }

    #endregion
}