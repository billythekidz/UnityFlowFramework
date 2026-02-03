Using "Replace String in File"

```

```

Giờ usage đơn giản và rõ ràng:

## **ProjectInstaller.cs (Global):**

```csharp

using Reflex.Core;

using UnityEngine;

public class ProjectInstaller : MonoBehaviour

{

    private Container _container;

    void Awake()

    {

        _container = new ContainerBuilder()

            .AddSingleton<GameStateMachine>()

            .AddSingleton<EventBusGlobal>()

            .Build();

        // ✅ Install as GLOBAL scope

        _container.InstallServiceLocatorGlobal();

        // ✅ Register to ServiceLocator

        ServiceLocator.Register(_container.Resolve<GameStateMachine>());

        ServiceLocator.Register(_container.Resolve<EventBusGlobal>());

        // Hoặc đơn giản hơn - chỉ dùng ServiceLocator

        // ServiceLocator.RegisterGlobal(new GameStateMachine());

        // ServiceLocator.RegisterGlobal(new EventBusGlobal());

    }

}

```

## **LoginSceneInstaller.cs (Scoped):**

```csharp

public class LoginSceneInstaller : MonoBehaviour

{

    void Awake()

    {

        // Get parent container từ ProjectInstaller

        var parentContainer = FindFirstObjectByType<ProjectInstaller>()

            .GetComponent<ProjectInstaller>()

            ._container; // Hoặc static reference

        var sceneContainer = new ContainerBuilder()

            .AddSingleton<EventBusLogin>()

            .Build();

        // ✅ Install as SCOPED với parent

        sceneContainer.InstallServiceLocatorScoped(parentContainer);

        ServiceLocator.Register(sceneContainer.Resolve<EventBusLogin>());

        var popupManager = FindFirstObjectByType<PopupManager>();

        if (popupManager != null)

            ServiceLocator.Register(popupManager);

    }

}

```

## **Hoặc cách đơn giản nhất - Không cần parent tracking:**

```csharp

// ProjectInstaller.cs

void Awake()

{

    // ✅ Global services

    ServiceLocator.RegisterGlobal(new GameStateMachine());

    ServiceLocator.RegisterGlobal(new EventBusGlobal());

}

// LoginSceneInstaller.cs

void Awake()

{

    // ✅ Scene services - tự động register vào current

    ServiceLocator.Register(new EventBusLogin());

    ServiceLocator.Register(FindFirstObjectByType<PopupManager>());

}

// Usage - tự động fallback to global

var stateMachine = ServiceLocator.Get<GameStateMachine>(); // From global

var eventBus = ServiceLocator.Get<EventBusLogin>();       // From current scope

```

Đã fix lỗi API! Reflex không expose `ParentContainer`, nên tách thành 2 methods rõ ràng. ✅

Made changes.