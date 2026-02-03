# GameFlowFramework Learning Project

This repository is a learning project for building a robust and scalable game framework in Unity. It explores various modern concepts and patterns, including:

- **MVVM (Model-View-ViewModel):** For clean separation of UI logic and game state.
- **Reactive Programming (with R3):** For managing complex data flows and events.
- **Async/Await (with UniTask):** For writing clean, non-blocking asynchronous code.
- **Dependency Injection (with Reflex):** For decoupling components and improving testability.
- **Custom Code Generation:** For reducing boilerplate and automating repetitive tasks.

## Installing Necessary Frameworks

This project uses several powerful frameworks to support programming. You need to install them through the **Unity Package Manager (UPM)**.

### Installation Guide via Git URL

This is the simplest and most recommended method.

1.  Open Unity Editor.
2.  Go to `Window` -> `Package Manager`.
3.  Click the **`+`** button in the top left corner.
4.  Select **`Add package from git URL...`**.
5.  Paste each URL below one by one and click `Add`.

#### 1. UniTask

Optimizes `async/await` for Unity, helping to write efficient, garbage-free asynchronous code.
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

#### 2. PrimeTween

A high-performance, zero-garbage tweening library. Installation instructions are here:
Import PrimeTween from the Asset Store. This is the preferable option because it comes with a Demo scene.
Optional: install via Unity Package Manager (UPM).
```
https://github.com/KyryloKuzyk/PrimeTween?tab=readme-ov-file#install-via-unity-package-manager-upm
```

#### 3. Reflex

A lightweight and powerful Dependency Injection (DI) framework that helps reduce dependencies between classes.
```
https://github.com/gustavopsantos/reflex.git?path=/Assets/Reflex/
```

After installing all the above packages, your project will be ready to go.


### 4. R3 Installation Steps

Used for reactive programming, managing data streams and events.

**1. Install NuGet Packages**
1.  Install NuGet using [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
    ```
    https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
    ```
-   Open `Window -> NuGet -> Manage NuGet Packages`.
-   **Install R3:** Search for "**R3**" and press **Install**.
-   **Install ObservableCollections:** Search for "**ObservableCollections**" (also from Cysharp) and press **Install**.
-   **Install ObservableCollections.R3:** Search for "**ObservableCollections.R3**" and press **Install**. This is an extension module to make `ObservableCollections` work smoothly with `R3`.

-   If you encounter version conflict errors, please disable version validation in Player Settings (Edit -> Project Settings -> Player -> Scroll down and expand "Other Settings" then uncheck "Assembly Version Validation" under the "Configuration" section).

**2. Install R3.Unity Package**

-   Install the `R3.Unity` package by referencing the git URL:
    ```
    https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity
    ```

R3 uses the *.*.* release tag, so you can specify a version like #1.0.0. For example: `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.0.0`
