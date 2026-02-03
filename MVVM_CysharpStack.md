# In-depth Report: High-Performance MVVM Architecture Implementation Strategy in Unity with the Cysharp Ecosystem (VContainer, R3, UniTask, VitalRouter)

## 1. Architectural Shift in Modern Unity Development

### 1.1. From MonoBehaviour to Data-Oriented Thinking

In the history of Unity development, the traditional programming model centered around `MonoBehaviour` has served as a fundamental cornerstone, enabling millions of developers to access the engine. However, as project scales grow beyond simple prototypes into complex commercial products, the "God Class" model—where a single script manages game logic, data, and UI display—begins to reveal critical weaknesses. The tight coupling between business logic and the Unity engine makes unit testing nearly impossible, while state management becomes a nightmare as data is scattered across various Inspectors.¹

The emergence of the MVVM (Model-View-ViewModel) design pattern, originating from Microsoft's XAML/WPF ecosystem, has brought a fresh perspective to developing user interfaces (UI) in games. MVVM is not just about separating files; it represents a paradigm shift from "event-driven" to "data-driven" and "reactive" thinking. In Unity, applying MVVM requires a robust tool stack capable of handling the specific challenges of the game loop, memory management (GC allocation), and multi-threading.

### 1.2. The Cysharp Ecosystem: A New Standard for .NET in Unity

In this context, the "Cysharp Stack"—a collection of open-source libraries developed by Yoshifumi Kawai (neuecc) and the community—has emerged as a comprehensive solution, thoroughly optimized for Unity's performance. Unlike standard .NET libraries, Cysharp's tools are designed with a "Zero Allocation" mindset and are deeply integrated into Unity's PlayerLoop lifecycle.

This report will provide a detailed analysis of combining four technological pillars to build an MVVM architecture:

1.  **VContainer:** Manages Dependency Injection (DI) with extremely fast resolution speeds and low memory overhead.³
2.  **R3 (Reactive Extensions for C#):** The third generation of the Reactive library, replacing UniRx, addressing issues with time and memory management.⁵
3.  **UniTask:** A high-performance async/await solution, replacing traditional Coroutines and Tasks.³
4.  **VitalRouter:** A zero-allocation command routing system for decoupling communication between modules.⁸

---

## 2. Architectural Foundation: Dependency Injection with VContainer

### 2.1. Why VContainer Replaces Zenject in MVVM

In an MVVM architecture, ViewModels must be pure C# classes (POCO - Plain Old CLR Objects) to ensure independence and testability. This poses a challenge: who will instantiate the ViewModel and provide its dependencies (like Models, Services)? A Dependency Injection (DI) Container is the answer.

Zenject (or Extenject) was once a popular choice, but VContainer has surpassed it with a modern architecture and superior performance. VContainer uses IL code generation or optimized Reflection to achieve resolution speeds 5-10 times faster than Zenject, while completely eliminating garbage collection (GC) allocation during object resolution.⁴ This is critically important in games, where frame rate hitches due to GC are unacceptable.

### 2.2. Setting up the Composition Root and LifetimeScope

The starting point for any application using VContainer is the `LifetimeScope`. This is the only place in the application that is allowed to "know" about the container's structure. In Unity, we typically design a hierarchical scope system: `RootLifetimeScope` (for the entire game) -> `SceneLifetimeScope` (for each scene) -> `FeatureLifetimeScope` (for specific features).

#### Real-world Code: Configuring the Root Scope

Below is an example of implementing a `GameLifetimeScope` that acts as a Global Scope, persisting throughout the game (DontDestroyOnLoad):

```csharp
using VContainer;
using VContainer.Unity;
using UnityEngine;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Register Models: Player data, exists as a singleton
        builder.Register<UserProfileModel>(Lifetime.Singleton);
        builder.Register<InventoryModel>(Lifetime.Singleton);

        // Register Services: Handle network logic, IO (Singleton)
        // Registering an interface makes Mocking for Unit Tests easy
        builder.Register<IAuthenticationService, AuthenticationService>(Lifetime.Singleton);
        builder.Register<IAssetLoader, AddressablesAssetLoader>(Lifetime.Singleton);

        // EntryPoints: Logic classes that are not MonoBehaviours but need to run with the Unity lifecycle
        builder.RegisterEntryPoint<GameInitializer>();
        builder.RegisterEntryPoint<NetworkTickManager>();
    }
}
```

### 2.3. EntryPoint: Replacing Initialization Logic in MonoBehaviour

A common mistake when transitioning to MVVM is keeping initialization logic in the `Start()` method of a `MonoBehaviour`. VContainer provides interfaces like `IStartable`, `IPostStartable`, and `ITickable` to separate lifecycle logic from `MonoBehaviour`.³

Notably, `IAsyncStartable` combined with UniTask allows for complex asynchronous initialization sequences (e.g., Login -> Load Config -> Load User Data) to be executed sequentially and clearly before the game starts.

```csharp
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class GameInitializer : IAsyncStartable
{
    private readonly IAuthenticationService _authService;
    private readonly UserProfileModel _userProfile;

    // Constructor Injection: VContainer automatically provides dependencies
    public GameInitializer(
        IAuthenticationService authService,
        UserProfileModel userProfile)
    {
        _authService = authService;
        _userProfile = userProfile;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        Debug.Log("Game Initializing...");

        // Sequential initialization chain
        await _authService.InitializeAsync(cancellation);

        var loginResult = await _authService.LoginAnonymouslyAsync(cancellation);
        if (loginResult.IsSuccess)
        {
            await _userProfile.LoadDataAsync(loginResult.UserId, cancellation);
            Debug.Log("Game Ready!");
        }
        else
        {
            Debug.LogError("Login Failed!");
            // Handle error or show a Retry Popup
        }
    }
}
```

### 2.4. Managing ViewModels with Scoped Lifetime

In MVVM, a ViewModel's lifecycle is often tied to its View. When a View (e.g., an Inventory window) is closed, its corresponding ViewModel should also be destroyed to free up memory. VContainer supports `Lifetime.Scoped` to solve this problem. When a child `LifetimeScope` (e.g., attached to an Inventory Window prefab) is created, ViewModels registered as `Scoped` within it are newly created and automatically `Dispose`d when that scope is destroyed.⁹

Comparison of VContainer Lifetimes for MVVM:

| **Lifetime** | **Description** | **Use Case** |
| --- | --- | --- |
| **Singleton** | A single instance for the entire parent Container. | Global Models, Network Services, Audio Manager. |
| **Scoped** | One instance per LifetimeScope. | ViewModels, Presenters for a specific screen. |
| **Transient** | A new instance is created every time it's requested. | Small items in a list (ItemViewModel), temporary effects. |

---

## 3. The Reactive Core: R3 and the Power of ReactiveProperty

### 3.1. R3: A Leap Forward from UniRx

UniRx was the standard for Reactive Programming in Unity for many years. However, it had architectural limitations, such as the use of `IScheduler`, which made controlling time and frame counts difficult, as well as memory allocation issues when using LINQ operators.

R3 (Reactive Extensions 3rd generation) thoroughly addresses these issues by integrating .NET 8's `TimeProvider` and a Unity-specific `FrameProvider` concept.⁶ This allows ViewModels to precisely control time-based logic (e.g., Debounce input, Delay animation) while maintaining testability (via `FakeTimeProvider`).

### 3.2. ReactiveProperty: The Bridge Between Model and View

In the MVVM architecture, the ViewModel does not directly call the View to update the UI. Instead, the ViewModel exposes properties representing its state, and the View "observes" these properties. `ReactiveProperty<T>` in R3 is the tool for this job.

A standard `ReactiveProperty` model in R3 includes:

1.  **ReactiveProperty (Mutable):** Used internally within the ViewModel or Model to change the value.
2.  **ReadOnlyReactiveProperty (Immutable):** Exposed externally for View binding, ensuring encapsulation.

#### Real-world Code: ViewModel with R3

```csharp
using R3;
using System;

public class HealthViewModel : IDisposable
{
    // Mutable backing fields
    private readonly ReactiveProperty<float> _currentHp;
    private readonly ReactiveProperty<float> _maxHp;

    // Public read-only properties for the View
    public ReadOnlyReactiveProperty<float> CurrentHp => _currentHp;
    public ReadOnlyReactiveProperty<float> MaxHp => _maxHp;

    // Computed Property: Automatically calculated when HP or MaxHP changes
    public ReadOnlyReactiveProperty<float> HealthPercent { get; }

    public HealthViewModel(float initialHp, float maxHp)
    {
        _currentHp = new ReactiveProperty<float>(initialHp);
        _maxHp = new ReactiveProperty<float>(maxHp);

        // CombineLatest: Combines the latest values of two streams
        HealthPercent = _currentHp
           .CombineLatest(_maxHp, (current, max) => current / max)
           .ToReadOnlyReactiveProperty();
    }

    public void TakeDamage(float damage)
    {
        // Validate business logic
        var newHp = Math.Max(0, _currentHp.Value - damage);
        _currentHp.Value = newHp;
    }

    public void Dispose()
    {
        // Dispose all subscriptions and properties when the ViewModel is destroyed
        _currentHp.Dispose();
        _maxHp.Dispose();
        HealthPercent.Dispose();
    }
}
```

### 3.3. Handling Multi-threading and FrameProvider

One of the most common crashes in Unity is accessing the Unity API (like `transform.position`, `text.text`) from a different thread (Background Thread). R3 provides a safe mechanism through `ObserveOn` with a `UnityFrameProvider`.¹¹

Unlike UniRx's generic `ObserveOnMainThread`, R3 allows precise selection of the point in the PlayerLoop:

-   `UnityFrameProvider.Update`: For normal game logic updates.
-   `UnityFrameProvider.FixedUpdate`: For physics logic.
-   `UnityFrameProvider.LateUpdate`: For camera or UI updates after logic has run (to avoid jitter).

Example of processing heavy data in the background and updating the UI safely:

```csharp
_searchQuery
   .Debounce(TimeSpan.FromMilliseconds(200)) // Wait for the user to stop typing
   .SelectAwait(async (query, ct) =>
    {
        // Run on the ThreadPool
        await UniTask.SwitchToThreadPool();
        return await SearchHeavyDataAsync(query, ct);
    })
   .ObserveOn(UnityFrameProvider.Update) // Return to the Main Thread
   .Subscribe(results => UpdateUI(results));
```

---

## 4. Asynchronous Handling: The Power of UniTask

### 4.1. Overcoming the Limitations of Coroutines

Unity's Coroutines, while easy to use, have major drawbacks: they create garbage every time `yield return new...` is called, they don't support return values, and exception handling (try-catch) is difficult. The standard C# `System.Threading.Tasks.Task` is too heavyweight for the game loop.

UniTask emerges as a perfect replacement: it's struct-based (zero allocation), deeply integrated into the PlayerLoop, and fully supports Unity's APIs (like `UnityWebRequest`, `AssetBundle`, `Addressables`) through `await` extension methods.³

### 4.2. UniTask in the ViewModel

In MVVM, ViewModels frequently need to perform asynchronous tasks like loading items, saving the game, or waiting for a server response. Using UniTask makes the code in the ViewModel linear, much easier to read, and simpler to debug compared to Callback Hell.

**Safety Rule:** Always pass a `CancellationToken` into async methods in the ViewModel. This `CancellationToken` should be linked to the lifecycle of the View or the Scope containing the ViewModel.

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;

public class InventoryViewModel
{
    private readonly IInventoryService _inventoryService;
    public ReactiveProperty<bool> IsLoading { get; } = new(false);

    public InventoryViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async UniTask RefreshInventoryAsync(CancellationToken cancellation)
    {
        IsLoading.Value = true;
        try
        {
            // Automatically throws OperationCanceledException if cancellation is triggered
            var items = await _inventoryService.GetItemsAsync(cancellation);

            // Process data
            _items.Value = items;
        }
        catch (OperationCanceledException)
        {
            // Handle task cancellation (usually by ignoring it)
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            // Display error on the UI
        }
        finally
        {
            IsLoading.Value = false;
        }
    }
}
```

### 4.3. Preventing Memory Leaks with Cancellation

A serious issue with async/await in Unity is this: if a GameObject is destroyed while a task is running, that task continues to run in the background (a "zombie task"). When it completes, it tries to access destroyed components, causing `MissingReferenceException` or, worse, incorrect logic.

UniTask provides `GetCancellationTokenOnDestroy()` for MonoBehaviours. However, in a ViewModel (a Plain Class), we don't have this method. The solution is to implement `IDisposable` in the ViewModel and create a private `CancellationTokenSource`.

```csharp
public class BaseViewModel : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    // This property is passed down to async methods
    protected CancellationToken CancellationToken => _cts.Token;

    public virtual void Dispose()
    {
        _cts.Cancel(); // Cancel all running tasks
        _cts.Dispose();
    }
}
```

---

## 5. Module Decoupling: VitalRouter and the Command Pattern

### 5.1. The "Spaghetti Code" Problem in UI Communication

As an application grows, ViewModels often need to communicate with each other. For example, the `InventoryViewModel` needs to notify the `CharacterStatsViewModel` when an item is equipped. If we inject one ViewModel into another, we create circular dependencies and a tightly coupled code structure that is difficult to separate.

VitalRouter solves this problem with a high-performance "Mediator" or "Command Bus" model. ViewModels don't know about each other; they only know about "Commands".⁸

### 5.2. Defining Commands and Routing

VitalRouter uses a Source Generator to create routing code at compile time, completely eliminating the cost of Reflection and GC allocation when sending commands.

**Step 1: Define a Command (Struct)**

Use a struct to ensure zero allocation.

```csharp
using VitalRouter;

public readonly struct EquipItemCommand : ICommand
{
    public readonly string ItemId;
    public readonly EquipmentSlot Slot;

    public EquipItemCommand(string itemId, EquipmentSlot slot)
    {
        ItemId = itemId;
        Slot = slot;
    }
}
```

**Step 2: Send the Command from ViewModel A**

```csharp
public class InventoryViewModel
{
    public void Equip(string itemId)
    {
        // Fire and forget or await
        Router.Default.PublishAsync(new EquipItemCommand(itemId, EquipmentSlot.MainHand));
    }
}
```

**Step 3: Handle the Command in ViewModel B (or a Presenter)**

The handling class needs to be marked `partial`, and the handling method decorated. This is required for the Source Generator to work.

```csharp
using VitalRouter;
using Cysharp.Threading.Tasks;

public partial class CharacterStatsPresenter
{
    private readonly CharacterModel _model;

    public CharacterStatsPresenter(CharacterModel model)
    {
        _model = model;
    }

    [Command]
    public async UniTask OnEquipItem(EquipItemCommand cmd, CancellationToken ct)
    {
        // Handle business logic
        await _model.ApplyEquipmentStatsAsync(cmd.ItemId, ct);

        UnityEngine.Debug.Log($"Equipped {cmd.ItemId} to slot {cmd.Slot}");
    }
}
```

### 5.3. Interceptors: Middleware for Game Logic

The true power of VitalRouter lies in its Interceptor system. It allows injecting logic between the sending and receiving of a command, creating a processing pipeline (similar to middleware in a web server). This is extremely useful for:

-   **Validation:** Checking conditions (level requirement, sufficient funds) before executing a command.
-   **Logging:** Logging all player actions.
-   **Error Handling:** Global error catching for all commands.

```csharp
public class LoggingInterceptor : ICommandInterceptor
{
    public async ValueTask InvokeAsync<T>(
        T command,
        PublishContext context,
        PublishContinuation<T> next)
        where T : ICommand
    {
        UnityEngine.Debug.Log($"[Command] {command.GetType().Name} executing...");

        try
        {
            await next(command, context); // Call the next handler
            UnityEngine.Debug.Log($"[Command] {command.GetType().Name} success.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Command] Failed: {ex.Message}");
            throw; // Rethrow or swallow the error depending on logic
        }
    }
}
```

---

## 6. ViewModel Design: The Logic Core of MVVM

### 6.1. Standard ViewModel Design Principles

ViewModels in Unity must strictly adhere to the following principles to be effective:

1.  **No View Dependency:** The ViewModel must not contain references to `GameObject`, `Transform`, or any UI Components (`Button`, `Text`).
2.  **Clear Input/Output:**
    -   **Input:** Methods or `Subject<T>` that receive user events (Click, Drag, Input Text).
    -   **Output:** `ReadOnlyReactiveProperty<T>` or `Observable<T>` for the View to display data.
3.  **State Management:** The ViewModel holds the temporary state of the UI (e.g., text being typed, selected tab) and synchronizes persistent state from the Model.

### 6.2. ReactiveCommand: Safe Input Handling

R3 provides `ReactiveCommand`, an enhanced version of `Subject` specialized for handling button inputs. The strength of `ReactiveCommand` is its ability to manage a `CanExecute` state.

Example: An "Upgrade" button is only enabled when the player has enough gold.

```csharp
public class UpgradeViewModel : IDisposable
{
    public ReadOnlyReactiveProperty<long> CurrentGold { get; }
    public ReadOnlyReactiveProperty<long> UpgradeCost { get; }

    // Command can only be triggered when there is enough money
    public ReactiveCommand<Unit> UpgradeCommand { get; }

    public UpgradeViewModel(WalletModel wallet, UpgradeConfig config)
    {
        CurrentGold = wallet.Gold;
        UpgradeCost = new ReactiveProperty<long>(config.BaseCost);

        // CanExecute logic: Gold >= Cost
        var canUpgrade = CurrentGold
           .CombineLatest(UpgradeCost, (gold, cost) => gold >= cost);

        // Create the Command with the canUpgrade condition
        UpgradeCommand = new ReactiveCommand<Unit>(canUpgrade);

        // Handle the logic when the command is executed
        UpgradeCommand.Subscribe(_ =>
        {
            wallet.Subtract(UpgradeCost.CurrentValue);
            // Perform upgrade...
        });
    }
}
```

With this design, the View only needs to bind the button's `interactable` property to the `CanExecute` property of the `UpgradeCommand`. The button will automatically enable/disable based on the player's gold without a single `if-else` line of code in the View.

---

## 7. View Layer Implementation: A Multi-Platform Binding Strategy

The View is the only place in MVVM allowed to inherit from `MonoBehaviour`. The View's job is to "bind" UI Components to the ViewModel. Unity currently has two main UI systems: uGUI (traditional Canvas) and UI Toolkit (new, based on HTML/CSS). The Cysharp stack supports both well, but the approaches differ.

### 7.1. Binding with uGUI (Canvas)

R3 supports uGUI through powerful Extension Methods, making binding code concise and declarative [^15^].

**Common uGUI Extension Methods Table:**

| **Component** | **Event (View -> VM)** | **Binding (VM -> View)** |
| --- | --- | --- |
| **Button** | `button.OnClickAsObservable()` | `command.BindTo(button)` |
| **InputField** | `input.OnValueChangedAsObservable()` | `property.Subscribe(x => input.text = x)` |
| **Toggle** | `toggle.OnValueChangedAsObservable()` | `property.Subscribe(x => toggle.isOn = x)` |
| **Slider** | `slider.OnValueChangedAsObservable()` | `property.Subscribe(x => slider.value = x)` |
| **Text (Legacy)** | N/A | `property.SubscribeToText(textComponent)` |
| **TMP_Text** | N/A | `property.SubscribeToText(tmpComponent)` |

**uGUI Binding Code Sample:**

```csharp
using UnityEngine;
using TMPro;
using R3;
using System;
using VContainer;

public class UserProfileView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _changeNameButton;
    [SerializeField] private TMP_InputField _nameInput;

    private UserProfileViewModel _vm;

    [Inject] // VContainer injects the ViewModel here
    public void Construct(UserProfileViewModel vm)
    {
        _vm = vm;
    }

    void Start()
    {
        // 1. One-way Binding: Display name
        _vm.UserName
           .SubscribeToText(_nameText)
           .AddTo(this); // Important: Attach the subscription to the GameObject's lifecycle

        // 2. Two-way Binding: Input Field
        // View -> ViewModel
        _nameInput.OnValueChangedAsObservable()
           .Subscribe(txt => _vm.InputName.Value = txt)
           .AddTo(this);

        // ViewModel -> View (to reset input when needed)
        _vm.InputName
           .Subscribe(txt => _nameInput.text = txt)
           .AddTo(this);

        // 3. Command Binding
        // Bind the click command to the ViewModel
        _changeNameButton.OnClickAsObservable()
           .ThrottleFirst(TimeSpan.FromSeconds(1)) // Prevent spam clicking
           .Subscribe(_ => _vm.ConfirmChangeNameCommand.Execute(Unit.Default))
           .AddTo(this);

        // Automatically disable the button if Input is empty (via CanExecute)
        _vm.ConfirmChangeNameCommand.CanExecute
           .SubscribeToInteractable(_changeNameButton)
           .AddTo(this);
    }
}
```

### 7.2. Binding with UI Toolkit (Modern)

UI Toolkit does not have built-in extensions like uGUI in R3 (as of now), and its mechanism is based on `VisualElement` instead of `Component`. However, we can build similar extension methods or use community-made binding libraries.

The biggest difference is that UI Toolkit doesn't use the Inspector for direct references. We must use `UQuery` (the `Q<T>` function) to find elements by name (defined in the UI Builder).¹²

**Building Binding Helpers for UI Toolkit:**

```csharp
public static class R3UIToolkitExtensions
{
    // Bind text for a Label
    public static IDisposable BindText(this Label label, Observable<string> source)
    {
        return source.Subscribe(label, (text, l) => l.text = text);
    }

    // Bind click for a Button
    public static Observable<Unit> OnClickAsObservable(this Button button)
    {
        return Observable.FromEvent<ClickEvent>(
            h => button.RegisterCallback(h),
            h => button.UnregisterCallback(h)
        ).Select(_ => Unit.Default);
    }

    // Bind a class (to toggle active/inactive styles)
    public static IDisposable BindClass(this VisualElement element, string className, Observable<bool> toggleSource)
    {
        return toggleSource.Subscribe(element, (isOn, el) =>
        {
            if (isOn) el.AddToClassList(className);
            else el.RemoveFromClassList(className);
        });
    }
}
```

**Usage in a View:**

```csharp
public class InventoryUIView : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    private InventoryViewModel _vm;

    [Inject]
    public void Construct(InventoryViewModel vm) => _vm = vm;

    void Start()
    {
        var root = _document.rootVisualElement;
        var equipBtn = root.Q<Button>("btn-equip");
        var statusLabel = root.Q<Label>("lbl-status");

        // Binding
        statusLabel.BindText(_vm.StatusMessage).AddTo(this);

        equipBtn.OnClickAsObservable()
           .Subscribe(_ => _vm.EquipCommand.Execute(Unit.Default))
           .AddTo(this);
    }
}
```

### 7.3. Handling Complex Lists (ListView) with ObservableList

A major challenge in MVVM is binding lists (e.g., a list of 100 items in an inventory). Creating 100 child GameObjects is terrible for performance. UI Toolkit provides a virtualized `ListView` to solve this, but how do we connect it to the ViewModel?

Cysharp provides the `ObservableCollections` library (separate from R3 but fully compatible).¹⁴

1.  **ViewModel:** Uses an `ObservableList<ItemViewModel>`.
2.  **View:** Uses UI Toolkit's `ListView`.

We need an Adapter to bridge the gap between `ObservableList` and Unity's `ListView`.

```csharp
// ViewModel
public ObservableList<ItemViewModel> Items { get; } = new();

// View
private void BindList(ListView uiListView, ObservableList<ItemViewModel> sourceList)
{
    // Set up the visuals for the ListView
    uiListView.makeItem = () => new Label(); // Or load from a VisualTreeAsset
    uiListView.bindItem = (element, index) =>
    {
        var itemVM = sourceList[index];
        (element as Label).text = itemVM.Name.Value;
    };

    // Important: Set the itemsSource
    uiListView.itemsSource = sourceList; // ObservableList implements IList, so it can be assigned directly

    // Listen for list changes to refresh the UI when items are added/removed
    // ObservableCollections supports the R3 extension ObserveCountChanged
    sourceList.ObserveCountChanged()
       .Subscribe(_ => uiListView.RefreshItems())
       .AddTo(this);
}
```

This approach leverages the virtualization capabilities of UI Toolkit (rendering only visible items) while maintaining the reactive nature of the data.

---

## 8. Advanced Scenarios and Optimization Strategies

### 8.1. Multi-Scene MVVM and Navigation

In games, switching scenes or opening nested popups is common. VContainer supports `LifetimeScope.CreateChild()` to create dynamic child scopes.

Example: A Navigation system that opens a Popup.

1.  **NavigationService:** Registered as a Singleton.
2.  **Logic:** When `OpenPopup<InventoryViewModel>()` is called, the service loads a Prefab containing an `InventoryScope`.
3.  **Scope:** The `InventoryScope` (a child of the Root scope) will instantiate the `InventoryViewModel` and its specific dependencies.
4.  **VitalRouter:** Used to pass initialization data into the Popup (e.g., an `OpenPopupCommand` containing an `InventoryID`).

### 8.2. Unit Testing ViewModels

A huge benefit of this architecture is its testability. Since ViewModels are POCOs and their dependencies are interfaces, we can use a mocking library (like `Moq` or `NSubstitute`) to test the logic.

**Test Example:**

```csharp
using NUnit.Framework;
using Moq;
using R3;

[TestFixture]
public class UpgradeViewModelTests
{
    [Test]
    public void Test_Upgrade_Success()
    {
        // Arrange
        var mockWallet = new Mock<IWalletService>();
        mockWallet.Setup(w => w.Gold).Returns(new ReactiveProperty<long>(1000));

        var vm = new UpgradeViewModel(mockWallet.Object);

        // Act
        vm.UpgradeCommand.Execute(Unit.Default);

        // Assert
        mockWallet.Verify(w => w.Subtract(It.IsAny<long>()), Times.Once);
    }
}
```

### 8.3. Performance Tuning

-   **Struct-based Commands:** VitalRouter uses `struct`s for Commands to avoid GC. Always prefer `struct` for high-frequency messages (like position updates, health changes).
-   **UniTaskTracker:** Use the `UniTask Tracker` tool (available in the package) to check for any "leaked" or hanging tasks. Leaked tasks are often caused by forgetting to pass a CancellationToken.
-   **R3 Allocation:** Avoid using `Select` + `Subscribe` repeatedly in `Update()`. If you need per-frame updates, use `Observable.EveryUpdate()` instead of firing events manually.

### 8.4. Conclusion

Implementing MVVM in Unity with the Cysharp Stack is not just about installing libraries; it's about applying a strict architectural discipline.

-   **VContainer** keeps the application structure loose and easy to extend.
-   **R3** makes data flow clear and safe.
-   **UniTask** unleashes the power of multi-threading in a simple way.
-   **VitalRouter** eliminates the complexity of module communication.

This combination creates a "Solid Foundation" for large-scale games, ensuring the codebase remains clean, maintainable, and achieves the highest possible performance on the Unity platform.
