# EventBus Framework

A universal event bus designed for Unity projects, providing a centralized messaging system that supports both server-driven and client-side events.

This framework helps decouple different parts of your application, leading to cleaner, more maintainable code. It supports two main event handling strategies:

1.  **Command-Based**: Uses string constants (like those in `Commands.cs`) to identify events. This is ideal for handling messages from a server, where commands are often sent as strings.
2.  **Type-Based**: Uses the C# class/struct type itself as the event identifier. This is perfect for internal, client-side events, ensuring type safety and reducing reliance on "magic strings".

## Features

-   **Dual-System Design**: Subscribe to events using string-based commands or C# types.
-   **Payload Support**: Publish events with or without data payloads.
-   **Memory Safe**: Automatically cleans up subscriptions to destroyed `UnityEngine.Object` instances.
-   **Lifecycle Management**: Implements `IDisposable` to clear all subscriptions, which is crucial for managing scene-specific event buses.
-   **IoC Friendly**: Designed to be integrated with a dependency injection container like `Reflex`.

## Integration with Reflex

To make the `EventBus` available throughout your application, you should register it as a singleton in your Reflex IoC container. This ensures that the same instance is shared everywhere, creating a truly global communication channel.

**Example: In your project's main installer**
```csharp
// In your ProjectInstaller.cs or a similar setup script

public class ProjectInstaller : Installer
{
    public override void InstallBindings()
    {
        // Bind the EventBus as a singleton instance
        Container.Bind<IEventBus>().To<EventBus>().AsSingleton();
    }
}
```

## How to Use

After registering `IEventBus`, you can inject it into any class that needs to publish or subscribe to events.

```csharp
public class MyService
{
    private readonly IEventBus _eventBus;

    // Inject the event bus via constructor
    public MyService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
}
```

### Subscribing to Events

**1. Command-Based (Server Events)**

Listen for events using string commands from your `Commands.cs` file.

```csharp
// Example: Listening for another player joining the game
// The server sends the command "OtherPlayerJoinGame" with player data.

public class GameManager
{
    private readonly IEventBus _eventBus;

    public GameManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    private void OnPlayerJoined(PlayerJoinData data)
    {
        Debug.Log($"Player {data.PlayerName} joined the game!");
        // Add player to the scene...
    }

    // Remember to unsubscribe to prevent memory leaks
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }
}
```

**2. Type-Based (Client Events)**

Listen for events based on their data type. This is the preferred method for internal application events.

```csharp
// Define a simple class or struct for your event
public class PlayerProfileOpened
{
    public string PlayerId;
}

public class UIManager
{
    private readonly IEventBus _eventBus;

    public UIManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<PlayerProfileOpened>(OnProfileOpened);
    }

    private void OnProfileOpened(PlayerProfileOpened evt)
    {
        Debug.Log($"Opening profile for player: {evt.PlayerId}");
        // Show the profile UI...
    }
    
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerProfileOpened>(OnProfileOpened);
    }
}
```

### Publishing Events

**1. Command-Based**

Typically, a central class (e.g., `ServerMessageHandler`) would receive raw server messages, parse them, and publish them on the `EventBus`.

```csharp
public class ServerMessageHandler
{
    private readonly IEventBus _eventBus;

    public ServerMessageHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void HandleRawMessage(string command, string jsonPayload)
    {
        // Example for player join
        if (command == Commands.BOARD_PLAYER_JOIN)
        {
            var data = JsonUtility.FromJson<PlayerJoinData>(jsonPayload);
            _eventBus.Publish(Commands.BOARD_PLAYER_JOIN, data);
        }
        // Example for a command with no payload
        else if (command == Commands.BOARD_HAND_START)
        {
            _eventBus.Publish(Commands.BOARD_HAND_START);
        }
    }
}
```

**2. Type-Based**

Any part of your client code can publish a type-based event.

```csharp
public class PlayerAvatar : MonoBehaviour
{
    [Inject] private readonly IEventBus _eventBus;
    public string PlayerId;

    public void OnAvatarClicked()
    {
        // Publish an event for other systems to consume
        _eventBus.Publish(new PlayerProfileOpened { PlayerId = this.PlayerId });
    }
}
```

## Migration from `Signal` System

The `EventBus` is a direct replacement for older `Signal` or `addHandler` patterns. The migration path is straightforward.

### Before: `Signal.addHandler`

Previously, you might have registered handlers like this:

```csharp
// Old system using a hypothetical Signal dispatcher
public class OldGameManager
{
    void Start()
    {
        // Assuming 'gameSignal' is a global or accessible signal instance
        gameSignal.addHandler(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    void OnDestroy()
    {
        gameSignal.removeHandler(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    // The handler might receive a generic object or a specific type
    private void OnPlayerJoined(object payload)
    {
        var data = (PlayerJoinData)payload;
        // ...
    }
}
```

### After: `IEventBus.Subscribe`

With the new `EventBus`, the equivalent code is cleaner and more type-safe.

```csharp
// New system using injected IEventBus
public class NewGameManager : IDisposable
{
    private readonly IEventBus _eventBus;

    // Get the bus via constructor injection
    public NewGameManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // Subscribe with a specific, strongly-typed payload
        _eventBus.Subscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }

    // The handler is now strongly-typed
    private void OnPlayerJoined(PlayerJoinData data)
    {
        Debug.Log($"Player {data.PlayerName} with ID {data.PlayerId} joined.");
        // ...
    }
    
    // Unsubscribe in a Dispose method (called by Reflex or manually)
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerJoinData>(Commands.BOARD_PLAYER_JOIN, OnPlayerJoined);
    }
}
```
