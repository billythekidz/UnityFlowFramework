# Architectural Patterns in Unity Development: An In-depth Analysis of MVC, MVP, and MVVM

## 1. Overview: The Complexity of State Management in a Game Engine

Developing real-time 3D applications, especially within the Unity ecosystem, presents unique technical challenges that are distinctly different from traditional enterprise software development. While the core tenets of software engineering—such as Separation of Concerns (SoC), modularity, and testability—remain invaluable, applying them to a component-based engine requires nuanced adaptation.

Unity's default architecture revolves around the `MonoBehaviour` class. This is a powerful design for rapid prototyping, allowing developers to attach logic directly to objects in 3D space. However, this convenience often leads to an architectural trap known as "Spaghetti Code" or the "Massive View Controller" anti-pattern. In this model, Data, Business Logic, and Presentation Logic often coexist within a single class. As project scales expand, these classes become bloated, difficult to maintain, hard to unit test, and extremely fragile when changes are required.¹

To address this technical debt, the Unity development community has actively adopted and adapted architectural patterns from the broader software development world: **Model-View-Controller (MVC)**, **Model-View-Presenter (MVP)**, and **Model-View-ViewModel (MVVM)**. This report will provide a comprehensive and in-depth analysis of these three patterns, specifically contextualized for Unity. We will delve into their theoretical underpinnings, practical implementations through detailed code examples (focusing on a Health System and UI), and the integration of modern reactive programming libraries like **R3**, as well as infrastructure patterns like the **Service Locator** and **Event Bus**, to build enterprise-grade solutions.⁴

### 1.1 The Monolithic MonoBehaviour Problem

Before diving into solutions, we must dissect the problem. A naive implementation of a health bar in Unity often consists of a single script attached to a GameObject. This script holds the current health value, checks for keyboard input, handles physics collisions, and directly updates a UI Slider component.

```csharp
// Anti-Pattern: Tightly Coupled Monolithic MonoBehaviour
using UnityEngine;
using UnityEngine.UI;

public class NaiveHealth : MonoBehaviour
{
    // Data (Model) mixed with View
    public float currentHealth;
    public float maxHealth = 100f;

    // Direct reference to the View
    public Slider healthSlider;
    public Image fillImage;

    // Controller logic inside Update
    void Update()
    {
        // Mixing Input detection and Business Logic
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(10);
        }
    }

    void TakeDamage(float amount)
    {
        // Business Logic
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // Presentation Logic (directly manipulating the UI)
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }

        if (fillImage != null)
        {
            // Hard-coded visual logic
            fillImage.color = Color.Lerp(Color.red, Color.green, currentHealth / maxHealth);
        }

        if (currentHealth <= 0)
        {
            Debug.Log("Player Died"); // Side effect
        }
    }
}
```

The code above violates the Single Responsibility Principle (SRP). `NaiveHealth` is responsible for too many things: storing data, handling input, game logic, and display. If we wanted to change the UI from a 2D slider to a 3D health bar above the character's head, we would have to modify this class, risking a break in the health calculation logic. Furthermore, how would one write a Unit Test for the `TakeDamage` logic without starting the Unity Editor? It's nearly impossible with this architecture.⁷

---

## 2. Model-View-Controller (MVC): The Foundation of Separation

The Model-View-Controller (MVC) pattern is the precursor to most modern user interface architectures. Developed by Trygve Reenskaug at Xerox PARC in the 1970s, MVC was designed to separate the internal representation of information from the way that information is presented to and accepted from the user. In the context of Unity, MVC is often the first step developers take to escape the confines of MonoBehaviour.¹

### 2.1 Architectural Definition in the Unity Context

In a strict interpretation of MVC for Unity:

-   **Model:** Contains the data and core business logic. In Unity, this is typically a pure C# class (POCO - Plain Old CLR Object) or a `ScriptableObject`. The Model is completely unaware of the View or Controller. It uses the Observer pattern (often a C# `event` or `Action`) to notify interested parties when its state changes.
-   **View:** The component responsible for displaying data to the user. In Unity, the View is a `MonoBehaviour` attached to UI elements or GameObjects in the scene. The View observes the Model to update its display state but never directly changes the Model.
-   **Controller:** Acts as an intermediary that handles input. It receives signals from the user (Input) and translates them into actions on the Model. The Controller can be a `MonoBehaviour` (to receive `Update` or `OnCollisionEnter`) or a pure class orchestrated by a Manager.

### 2.2 Detailed Implementation: Refactored MVC Health System

Let's refactor the "NaiveHealth" example above into a robust MVC structure. The goal is to allow changing the user interface (View) without touching the logic (Model) or input method (Controller).³

#### 2.2.1 The Model: Pure Logic

The Model is the Source of Truth. By decoupling it from `MonoBehaviour`, we ensure it can be easily serialized and, most importantly, tested in a pure .NET environment without Unity's API.

```csharp
// MVC Model: Pure C# Logic
using System;

// Can be shown in the Inspector if wrapped
public class HealthModel
{
    // Encapsulated data
    private float _currentHealth;
    private float _maxHealth;

    // Observer Pattern: Model notifies when state changes
    // Using Action for cleaner syntax than a custom delegate
    public event Action<float, float> OnHealthChanged; // current, max
    public event Action OnDeath;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _currentHealth <= 0;

    // Constructor for Dependency Injection or initialization
    public HealthModel(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
    }

    // Pure business logic
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        _currentHealth -= amount;
        // Clamp value to ensure data integrity
        if (_currentHealth < 0) _currentHealth = 0;

        // Notify Observers (the View)
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (IsDead)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        _currentHealth += amount;
        if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
}
```

*Analysis:* `HealthModel` has no dependency on `UnityEngine.UI`. It can be used for both a player character (with a UI) and an AI enemy (with no UI or a different one). This separation allows for high code reusability.¹¹

#### 2.2.2 The View: Observing and Displaying

In the traditional MVC pattern, the View knows about the Model. It subscribes to the Model's events to update itself. In Unity, the View must be a `MonoBehaviour` to reference components in the Scene.

```csharp
// MVC View: Handles display
using UnityEngine;
using UnityEngine.UI;

public class HealthViewMVC : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Text _healthText;

    private Color _healthyColor = Color.green;
    private Color _criticalColor = Color.red;

    // The View holds a reference to the Model to subscribe to events.
    // However, the View MUST NOT call methods that change the Model's data (that's the Controller's job).
    public void Initialize(HealthModel model)
    {
        // Subscribe to events (Observer Pattern)
        model.OnHealthChanged += UpdateHealthBar;
        model.OnDeath += HandleDeath;

        // Update initial state
        UpdateHealthBar(model.CurrentHealth, model.MaxHealth);
    }

    private void UpdateHealthBar(float current, float max)
    {
        float percentage = current / max;
        if (_slider != null) _slider.value = percentage;
        if (_healthText != null) _healthText.text = $"{current:0}/{max:0}";
        if (_fillImage != null)
        {
            // Pure presentation logic
            _fillImage.color = Color.Lerp(_criticalColor, _healthyColor, percentage);
        }
    }

    private void HandleDeath()
    {
        // Only handles the visual aspect of death (hiding UI, playing animation, etc.)
        if (_healthText != null) _healthText.text = "DEAD";
        // Can trigger an animation here
    }

    // Clean up events to prevent Memory Leaks
    private void OnDestroy()
    {
        // Note: A mechanism to unsubscribe is needed if the Model outlives the View.
        // In this simple example, we assume their lifecycles are tied.
    }
}
```

#### 2.2.3 The Controller: Orchestration and Input

The Controller is the connecting brain. In Unity, a common scenario is for the Controller to initialize the Model and View, then listen for player input to manipulate the Model.

```csharp
// MVC Controller: Handles Input and Initialization
using UnityEngine;

public class HealthControllerMVC : MonoBehaviour
{
    [SerializeField] private HealthViewMVC _view; // Reference to the view in the scene

    // The Controller owns the Model
    private HealthModel _model;
    private float _initialHealth = 100f;

    void Awake()
    {
        // 1. Initialize the Model
        _model = new HealthModel(_initialHealth);

        // 2. Connect the View to the Model
        if (_view != null)
        {
            _view.Initialize(_model);
        }
        else
        {
            Debug.LogError("View reference missing inside Controller!");
        }
    }

    void Update()
    {
        // 3. Handle Input (Input Polling)
        // The Controller translates user actions into commands for the Model
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Input received: Take Damage");
            _model.TakeDamage(10f);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("Input received: Heal");
            _model.Heal(5f);
        }
    }
}
```

### 2.3 Evaluating the MVC Pattern in Unity

**Advantages:**

-   **Data Separation:** Business logic is safe in a pure C# class, easily portable to other projects.
-   **Clarity:** The data flow (Input -> Controller -> Model -> View) is relatively easy to understand.

**Disadvantages:**

-   **View's Dependency:** The View depends directly on the Model (`HealthModel`). If we change the Model's structure (e.g., rename an event), we must also modify the View. This creates an undesirable tight coupling between the presentation and data layers.¹²
-   **Fat Controller:** In Unity, the line between View and Controller often blurs because both are typically MonoBehaviours. The Controller often ends up shouldering too much lifecycle management responsibility.
-   **Difficult to Test View:** Because the View is tied to `MonoBehaviour` and directly references a concrete Model, writing independent unit tests for the View is very difficult.

---

## 3. Model-View-Presenter (MVP): The "Passive View" Architecture for Maximum Testability

The Model-View-Presenter (MVP) pattern is considered a natural evolution of MVC, particularly well-suited for complex UI systems in Unity. The core difference lies in the role of the **Presenter** and the **Passive** nature of the View.

In MVP, the View **knows nothing about the Model**. It is completely "dumb" or "passive." It only displays what the Presenter tells it to and forwards all user actions (like mouse clicks) to the Presenter. The Presenter acts as the absolute middleman: it retrieves data from the Model, formats it, and pushes it to the View.¹⁴

### 3.1 The Power of Interfaces

The key to a successful MVP implementation in Unity is the use of an **Interface** for the View. By having the Presenter communicate with an `IHealthView` instead of the concrete `HealthViewUnity` class, we achieve complete decoupling. This allows us to replace the entire UI (e.g., switch from uGUI to UI Toolkit) without changing a single line of code in the Presenter or Model. More importantly, it enables easy **Unit Testing** using Mock Objects.¹⁶

### 3.2 Detailed Implementation: MVP Health System

#### 3.2.1 Step 1: Define the Contract (The Interface)

This is the most critical step. We define what the View *can do* and what it *can report*.

```csharp
// MVP: The interface contract for the View
using System;
using UnityEngine;

public interface IHealthView
{
    // Command Methods: The Presenter orders the View
    void SetHealthText(string text);
    void UpdateHealthSlider(float value);
    void SetFillColor(Color color);
    void ShowDeathScreen();

    // Events: The View notifies the Presenter about user actions
    event Action OnHealRequested;
    event Action OnDamageRequested; // e.g., for a debug button
}
```

#### 3.2.2 Step 2: The View (Passive Implementation)

The concrete View is just a thin wrapper around Unity UI components. It contains no calculation logic.

```csharp
// MVP: Concrete View (MonoBehaviour)
using UnityEngine;
using UnityEngine.UI;
using System;

public class HealthViewMVP : MonoBehaviour, IHealthView
{
    [SerializeField] private Slider _slider;
    [SerializeField] private Text _text;
    [SerializeField] private Image _fill;
    [SerializeField] private Button _healButton;
    [SerializeField] private Button _damageButton;
    [SerializeField] private GameObject _deathPanel;

    // Implement events from the interface
    public event Action OnHealRequested;
    public event Action OnDamageRequested;

    private void Start()
    {
        // Bind UI events to C# Actions
        if (_healButton) _healButton.onClick.AddListener(() => OnHealRequested?.Invoke());
        if (_damageButton) _damageButton.onClick.AddListener(() => OnDamageRequested?.Invoke());
    }

    // Implement display methods
    public void SetHealthText(string text)
    {
        if (_text) _text.text = text;
    }

    public void UpdateHealthSlider(float value)
    {
        if (_slider) _slider.value = value;
    }

    public void SetFillColor(Color color)
    {
        if (_fill) _fill.color = color;
    }

    public void ShowDeathScreen()
    {
        if (_deathPanel) _deathPanel.SetActive(true);
    }
}
```

*Analysis:* `HealthViewMVP` has no reference to `HealthModel`. It doesn't know what "Health" is, only how to display a float and a string.

#### 3.2.3 Step 3: The Presenter (The Middle Brain)

The Presenter contains all presentation logic. It decides the color of the health bar based on its value, formats the text string, etc.

```csharp
// MVP: The Presenter (Presentation Logic)
using UnityEngine;
using System;

public class HealthPresenter : IDisposable
{
    private readonly HealthModel _model;
    private readonly IHealthView _view; // Depends only on the Interface

    // Constructor Injection
    public HealthPresenter(HealthModel model, IHealthView view)
    {
        _model = model;
        _view = view;

        // 1. Listen to the Model
        _model.OnHealthChanged += HandleModelChange;
        _model.OnDeath += HandleDeath;

        // 2. Listen to the View (User Input)
        _view.OnHealRequested += HandleHealInput;
        _view.OnDamageRequested += HandleDamageInput;

        // 3. Initialize the View's state
        RefreshView();
    }

    private void RefreshView()
    {
        HandleModelChange(_model.CurrentHealth, _model.MaxHealth);
    }

    // Handle data changes
    private void HandleModelChange(float current, float max)
    {
        float percentage = current / max;

        // The Presenter decides how to format the data
        _view.UpdateHealthSlider(percentage);
        _view.SetHealthText($"{current}/{max} HP");

        // Color logic is in the Presenter, not the View
        _view.SetFillColor(percentage < 0.3f ? Color.red : Color.green);
    }

    private void HandleDeath()
    {
        _view.ShowDeathScreen();
    }

    // Handle Input from the View
    private void HandleHealInput()
    {
        _model.Heal(10f);
    }

    private void HandleDamageInput()
    {
        _model.TakeDamage(10f);
    }

    // Important: Clean up to prevent Memory Leaks
    public void Dispose()
    {
        _model.OnHealthChanged -= HandleModelChange;
        _model.OnDeath -= HandleDeath;
        _view.OnHealRequested -= HandleHealInput;
        _view.OnDamageRequested -= HandleDamageInput;
    }
}
```

#### 3.2.4 Step 4: The Bootstrapper (The Connector)

Since the Presenter is not a MonoBehaviour, something needs to create it. This is typically a `Setup` script or is handled by Dependency Injection.

```csharp
using UnityEngine;

public class GameSetup : MonoBehaviour
{
    [SerializeField] private HealthViewMVP _view; // Reference to the concrete implementation
    private HealthModel _model;
    private HealthPresenter _presenter;

    void Awake()
    {
        _model = new HealthModel(100f);
        // The Presenter takes an IHealthView, but we pass the HealthViewMVP instance
        _presenter = new HealthPresenter(_model, _view);
    }

    void OnDestroy()
    {
        _presenter?.Dispose();
    }
}
```

### 3.3 Unit Testing in MVP

This is MVP's greatest advantage. We can test presentation logic without Unity. Assuming the use of the **NSubstitute** library:

```csharp
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class HealthPresenterTests
{
    [Test]
    public void WhenHealthIsCritical_ViewColorIsSetToRed()
    {
        // Arrange
        var model = new HealthModel(100);
        var view = Substitute.For<IHealthView>(); // Mock the View
        var presenter = new HealthPresenter(model, view);

        // Act
        model.TakeDamage(80); // Health is now 20 (20%)

        // Assert
        // Check if the Presenter called SetFillColor with red
        view.Received().SetFillColor(Color.red);
    }
}
```

This capability is invaluable in large projects that require high stability.¹⁸

---

## 4. Model-View-ViewModel (MVVM) and Reactive Programming

MVVM was originally designed by Microsoft for WPF and XAML. Its core distinction from MVP is **Data Binding**. In MVP, the Presenter must manually call `view.SetText(...)`. In MVVM, the View automatically listens and updates when the ViewModel changes via a binding mechanism, eliminating a great deal of boilerplate code.¹¹

However, Unity (before UI Toolkit) did not have a robust native binding system. Therefore, MVVM in Unity is often paired with **Reactive Programming** libraries like **UniRx** or the next-generation library **R3**.

### 4.1 What is R3 (Reactive Extensions 3rd Gen)?

Research documents²¹ point to the rise of R3 as the successor to UniRx. R3 offers better performance, tight integration with modern C# async/await, and addresses the memory allocation issues of previous versions.

In MVVM with R3, we no longer use C# events. We use `ReactiveProperty<T>`.

### 4.2 Implementation: Reactive Health System with R3

#### 4.2.1 The ViewModel

The ViewModel in MVVM serves to provide data that is already prepared for the View. It exposes data streams instead of static values.

```csharp
// MVVM: ViewModel using R3
using R3;
using System;
using UnityEngine;

public class HealthViewModel : IDisposable
{
    private readonly HealthModel _model;
    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    // Reactive Properties: Output data for the View
    // ReadOnlyReactiveProperty ensures the View cannot set the value back
    public ReadOnlyReactiveProperty<float> CurrentHealthRx { get; }
    public ReadOnlyReactiveProperty<float> HealthPercentageRx { get; }
    public ReadOnlyReactiveProperty<bool> IsDeadRx { get; }
    public ReadOnlyReactiveProperty<Color> HealthColorRx { get; }

    // Commands: Input from the View
    public Subject<Unit> OnHealCommand { get; } = new Subject<Unit>();
    public Subject<Unit> OnDamageCommand { get; } = new Subject<Unit>();

    public HealthViewModel(HealthModel model)
    {
        _model = model;

        // Convert the Model's traditional event to a Reactive Stream
        // Observable.FromEvent bridges the gap between OOP and FRP (Functional Reactive Programming)
        var healthChangedStream = Observable.FromEvent<Action<float, float>, (float current, float max)>(
            handler => (c, m) => handler((c, m)),
            h => _model.OnHealthChanged += h,
            h => _model.OnHealthChanged -= h
        );

        // Binding Logic: Transform raw data into display data

        // 1. Current Health Stream
        CurrentHealthRx = healthChangedStream
           .Select(x => x.current)
           .ToReadOnlyReactiveProperty(_model.CurrentHealth);

        // 2. Percentage Stream
        HealthPercentageRx = healthChangedStream
           .Select(x => x.current / x.max)
           .ToReadOnlyReactiveProperty(1.0f);

        // 3. Color Logic Stream (Reactive Logic)
        HealthColorRx = HealthPercentageRx
           .Select(pct => pct < 0.3f ? Color.red : Color.green)
           .ToReadOnlyReactiveProperty(Color.green);

        // 4. Death Stream
        IsDeadRx = Observable.FromEvent(
            h => _model.OnDeath += h,
            h => _model.OnDeath -= h
        ).Select(_ => true).ToReadOnlyReactiveProperty(false);

        // Handle Commands
        OnHealCommand.Subscribe(_ => _model.Heal(10f)).AddTo(_disposables);
        OnDamageCommand.Subscribe(_ => _model.TakeDamage(10f)).AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        OnHealCommand.Dispose();
        OnDamageCommand.Dispose();
    }
}
```

#### 4.2.2 The View (Binder)

In MVVM, the View (`MonoBehaviour`) is responsible for "wiring things up." It subscribes to the ReactiveProperties.

```csharp
// MVVM: The View (Binder Layer)
using UnityEngine;
using UnityEngine.UI;
using R3;

public class HealthViewMVVM : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private Text _text;
    [SerializeField] private Image _fill;
    [SerializeField] private Button _healButton;
    [SerializeField] private Button _damageButton;
    [SerializeField] private GameObject _deathScreen;

    private HealthViewModel _viewModel;

    public void Initialize(HealthViewModel viewModel)
    {
        _viewModel = viewModel;

        // 1. One-way Binding: ViewModel -> View
        // Automatically updates the UI when the ViewModel changes

        _viewModel.HealthPercentageRx
           .Subscribe(val => _slider.value = val)
           .AddTo(this); // Automatically unsubscribes when the GameObject is destroyed (R3 feature)

        _viewModel.CurrentHealthRx
           .Select(val => $"HP: {val:0}") // Formatting logic can be here or in VM
           .SubscribeToText(_text) // R3 extension for Text
           .AddTo(this);

        _viewModel.HealthColorRx
           .Subscribe(color => _fill.color = color)
           .AddTo(this);

        _viewModel.IsDeadRx
           .Where(isDead => isDead) // Filter for the 'true' event
           .Subscribe(_ => _deathScreen.SetActive(true))
           .AddTo(this);

        // 2. Interaction: View -> ViewModel (Commands)

        // Convert uGUI click events to Observables
        _healButton.OnClickAsObservable()
           .Subscribe(_ => _viewModel.OnHealCommand.OnNext(Unit.Default))
           .AddTo(this);

        _damageButton.OnClickAsObservable()
           .Subscribe(_ => _viewModel.OnDamageCommand.OnNext(Unit.Default))
           .AddTo(this);
    }
}
```

### 4.3 UI Toolkit: The Future of MVVM in Unity

Unity 6 and newer versions are heavily promoting **UI Toolkit**. This system supports native data binding similar to WPF. You don't need to write manual binding code as above. Instead, you set a `binding-path` in the UXML file, and the View will automatically synchronize with any object that implements `INotifyPropertyChanged`.¹¹

Example UXML binding:

```xml
<ui:ProgressBar binding-path="HealthPercentage" />
<ui:Label binding-path="CurrentHealthText" />
```

This turns Unity into a true MVVM environment, significantly reducing the amount of "glue code" a developer has to write.

---

## 5. "Glue" Patterns: Service Locator and Event Bus

Whether you choose MVC, MVP, or MVVM, a major architectural question always remains: **How do the components find each other?** If the View needs a Presenter, and the Presenter needs a Model, who instantiates and connects them? If a character dies, how does the Audio System know to play a scream without a direct reference?⁵

### 5.1 The Service Locator Pattern

A Service Locator is a central registry where systems register themselves. While often debated as an "anti-pattern" because it can hide dependencies, it is extremely common in Unity due to its simplicity.

```csharp
// Simple Service Locator implementation
using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public static void Register<T>(T service)
    {
        if (!_services.ContainsKey(typeof(T)))
            _services.Add(typeof(T), service);
    }

    public static T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out object service))
            return (T)service;

        throw new Exception($"Service of type {typeof(T)} has not been registered.");
    }
}

// Usage in an MVP Presenter
public class PlayerPresenter
{
    public PlayerPresenter()
    {
        // The Presenter finds the Audio Service itself
        var audio = ServiceLocator.Get<IAudioService>();
        audio.PlaySound("Spawn");
    }
}
```

### 5.2 The Event Bus

To solve the problem of horizontal communication, an Event Bus is the optimal solution. It allows completely decoupled systems to communicate with each other through messages.

```csharp
// Type-Safe Generic Event Bus implementation
public interface IEvent { }

public static class EventBus<T> where T : IEvent
{
    private static event Action<T> _onEvent;

    public static void Subscribe(Action<T> action) => _onEvent += action;
    public static void Unsubscribe(Action<T> action) => _onEvent -= action;
    public static void Raise(T eventItem) => _onEvent?.Invoke(eventItem);
}

// Define an event
public struct PlayerDamageEvent : IEvent
{
    public float DamageAmount;
    public Vector3 Location;
}

// Usage:
// Where damage occurs:
EventBus<PlayerDamageEvent>.Raise(new PlayerDamageEvent { DamageAmount = 10, Location = transform.position });

// Particle System (Blood Effect):
EventBus<PlayerDamageEvent>.Subscribe(evt => SpawnBloodEffect(evt.Location));
```

This pattern helps the `HealthPresenter` remain unaware of the `AudioSystem` or `ParticleSystem`, minimizing cross-dependencies.⁶

---

## 6. Comparative Analysis and Summary Table

To help you make the right architectural decision, here is a detailed comparison table based on technical criteria.

| **Criterion** | **MVC (Unity Style)** | **MVP (Passive View)** | **MVVM (Reactive)** |
| --- | --- | --- | --- |
| **View "Intelligence"** | View knows the Model (Subscribes to events) | View is "dumb" (Display only) | View knows the ViewModel (Binding) |
| **UI Logic Location** | Controller | Presenter | ViewModel |
| **Coupling** | High (View -> Model) | Low (Via Interface) | Low (Via Binding) |
| **Unit Testability** | Low (Depends on MonoBehaviour) | Very High (Easy to Mock View) | High (ViewModel is POCO) |
| **Code Complexity** | Low | Medium (More Boilerplate) | High (Requires understanding Reactive/Rx) |
| **Runtime Performance** | Highest (Direct function calls) | High (Calls via Interface) | Medium (Event/Delegate overhead) |
| **Debuggability** | Easy (Clear call stack) | Easy | Hard (Complex stack traces due to Rx) |
| **Best Suited For** | Prototypes, Small Games, Game Jams | Complex Gameplay Logic, TDD | Complex UI (Inventory, Shop) |

### 6.1 Performance Analysis

-   **MVC/MVP:** Use direct function calls or C# delegates. The cost is very low, with no significant GC Allocation if used carefully. Suitable for tight game loops (Update loop).
-   **MVVM (Rx):** The creation of Streams (`Observable`), subscriptions (`Subscribe`), and closures (local variables in lambdas) often creates GC allocation. Although R3 is highly optimized compared to UniRx, using MVVM for per-frame logic updates on thousands of entities is still not recommended. MVVM should be limited to the UI layer (where update frequency is lower).²¹

---

## 7. Conclusion and Architectural Recommendations

The shift from "MonoBehaviour spaghetti" to a structured architecture is a sign of maturity in a Unity development process.

1.  **Use MVC when:** You are working on a small project, a quick prototype, or your team is not familiar with complex patterns. It's better than putting everything in one file but not flexible enough for large projects.
2.  **Use MVP when:** You are building core Gameplay systems (Character controllers, Combat systems). The ability to decouple the View via an Interface helps you write Unit Tests for game logic, ensuring high stability. This is the "sweet spot" that balances clarity and control.
3.  **Use MVVM when:** You are building complex, data-heavy UI systems (Inventory screens, Skill Trees, Shops, Settings). Especially if you are using **UI Toolkit**, MVVM is the mandatory choice to leverage the engine's power. Combined with **R3**, you can create smooth, responsive interfaces with minimal code.

**Hybrid Architecture:** In a real production environment, it's rare for a project to use only one pattern. A common architecture is to use **MVP for Gameplay** (to optimize performance and control) and **MVVM for UI** (to optimize the UI development workflow). These systems are then loosely connected via an **Event Bus** or **Service Locator**.

By understanding the strengths and weaknesses of each pattern, you can design games that not only run smoothly but are also easy to maintain and extend in the future.
