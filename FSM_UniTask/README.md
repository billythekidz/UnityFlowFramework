# FSM_UniTask: A Hierarchical & Asynchronous FSM Framework for Unity

This document provides a comprehensive guide to the `FSM_UniTask` framework, a robust system for creating Hierarchical State Machines (HSM) in Unity, built with `UniTask` for powerful asynchronous capabilities.

## Overview

`FSM_UniTask` is a lightweight, high-performance state machine framework designed to manage complex game logic and UI flows. Its core strengths are:

-   **Hierarchical Structure:** States can contain their own sub-states, allowing you to break down complex logic into manageable, nested state machines.
-   **Asynchronous by Design:** All state lifecycle methods (`Enter`, `Update`, `Exit`, etc.) are built on `UniTask`, making it trivial to handle time-based events, scene loading, API calls, and other async operations without callbacks or Coroutines.
-   **Robust Cancellation:** The framework automatically manages `CancellationToken`s, ensuring that when a state is exited, all of its asynchronous operations are safely and reliably cancelled, preventing errors and memory leaks.
-   **Data Passing:** Easily pass data between states during transitions.

## Core Concepts

The framework is built on four core abstract classes.

### 1. `FSM_System`
-   The main "brain" of the state machine. It is a `MonoBehaviour` that you attach to a persistent GameObject.
-   **Responsibilities:**
    -   Manages the current active state.
    -   Handles state transitions via `GoToState()`.
    -   Creates and manages the `CancellationToken` for the lifecycle of each state.
    -   Drives the `Update`, `LateUpdate`, and `FixedUpdate` async loops.

### 2. `FSM_State`
-   The basic building block of the FSM. Represents a single, "leaf" state that does not have children.
-   **Responsibilities:**
    -   Implement application logic within its lifecycle methods (`StateEnter`, `StateUpdate`, `StateExit`).
    -   Holds a reference to the main `FSM_System` to request state transitions.

### 3. `FSM_StateParent`
-   A more complex state that inherits from `FSM_State`. It can contain and manage its own FSM of sub-states.
-   **Responsibilities:**
    -   All responsibilities of an `FSM_State`.
    -   Manages the lifecycle of its sub-states via `GoToSubState()`.
    -   Propagates `Update` calls down to its active sub-state.
    -   Manages a dedicated `CancellationToken` for its children, separate from the parent's token.

### 4. `FSM_SubState`
-   A state that is managed by an `FSM_StateParent`.
-   Its structure is nearly identical to `FSM_State`, but it holds a reference to its `FSM_StateParent` instead of the main `FSM_System`.

## Basic Usage Guide

Here's how to set up a simple FSM with two states: `Loading` and `MainMenu`.

### Step 1: Create the Main State Machine

Create a class that inherits from `FSM_System`. Initialize all your top-level states in the constructor or `Awake`.

```csharp
// File: MyGameStateMachine.cs
public class MyGameStateMachine : FSM_System
{
    // Define your states
    public readonly LoadingState State_Loading;
    public readonly MainMenuState State_MainMenu;

    // Use constructor or Awake to initialize states
    public MyGameStateMachine()
    {
        State_Loading = new LoadingState(this);
        State_MainMenu = new MainMenuState(this);
    }

    // Set the initial state in Start
    public override void Start()
    {
        base.Start();
        // Use .Forget() because we don't need to wait for this transition here
        GoToState(State_Loading).Forget();
    }
}
```

### Step 2: Define a State with an Async Operation

`LoadingState` will simulate loading assets and then transition to `MainMenuState`.

```csharp
// File: LoadingState.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class LoadingState : FSM_State
{
    // The constructor must pass the FSM_System to the base class
    public LoadingState(FSM_System fsm) : base(fsm) {}

    // Override lifecycle methods
    public override async UniTask StateEnter(CancellationToken token)
    {
        Debug.Log("Entered Loading State. Loading assets...");

        // Simulate a 2-second asset loading operation
        await UniTask.Delay(2000, cancellationToken: token);
        
        // If the state was exited while we were delaying, the token would be
        // cancelled, an exception would be thrown, and this line would not be reached.

        Debug.Log("Loading complete. Transitioning to Main Menu...");

        // Get the specific FSM system to access its states
        var fsm = (MyGameStateMachine)_stateMachine;
        await _stateMachine.GoToState(fsm.State_MainMenu);
    }

    public override UniTask StateExit()
    {
        Debug.Log("Exiting Loading State.");
        return UniTask.CompletedTask;
    }
}
```

### Step 3: Define a Simple State

`MainMenuState` is a simple state that does nothing except log a message.

```csharp
// File: MainMenuState.cs
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class MainMenuState : FSM_State
{
    public MainMenuState(FSM_System fsm) : base(fsm) {}

    public override UniTask StateEnter(CancellationToken token)
    {
        Debug.Log("Welcome to the Main Menu!");
        // This state finishes its entry logic immediately
        return UniTask.CompletedTask;
    }
}
```

## Key APIs

### `FSM_System`
-   `UniTask GoToState(FSM_State newState)`: Transitions to a new state.
-   `UniTask GoToState(FSM_State newState, object data)`: Transitions to a new state and passes data to it.

### `FSM_State` / `FSM_SubState` (Virtual Methods to Override)
-   `UniTask StateEnter(CancellationToken cancellationToken)`
-   `UniTask StateEnter<T>(T data, CancellationToken cancellationToken)`
-   `UniTask StateUpdate(CancellationToken cancellationToken)`
-   `UniTask StateLateUpdate(CancellationToken cancellationToken)`
-   `UniTask StateFixedUpdate(CancellationToken cancellationToken)`
-   `UniTask StateExit()`

### `FSM_StateParent` (Protected Methods)
-   `UniTask GoToSubState(FSM_SubState newSubState)`
-   `UniTask GoToSubState<T>(FSM_SubState newSubState, T data)`
