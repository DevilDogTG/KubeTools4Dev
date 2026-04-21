# Copilot Instructions for KubeTools4Dev

## Project Overview

KubeTools4Dev is a cross-platform **desktop application** (Windows primary target) built with **.NET 10** and **Avalonia UI**. It provides a GUI for monitoring Kubernetes pods and managing service port forwarding without the command line.

## Build & Run

```powershell
# Restore dependencies
dotnet restore

# Run the application
dotnet run --project src/KubeTools4Dev/KubeTools4Dev.csproj

# Build (release)
dotnet build -c Release
```

There are currently no automated tests (no test project exists yet). The `.agent\memory.md` references `src/KubeTools4Dev.Core.Test` as a planned xUnit test project.

### Release (Velopack installer + GitHub Release)

```powershell
.\scripts\release.ps1 -DryRun   # preview
.\scripts\release.ps1            # patch bump
.\scripts\release.ps1 -Minor    # minor bump
.\scripts\release.ps1 -Major    # major bump
```

The release script requires **Git**, **GitHub CLI (`gh`)**, and **.NET 10 SDK**. It auto-installs `vpk` (Velopack CLI) if missing.

---

## Architecture

### Project Layout

| Project | Purpose |
|---|---|
| `src/KubeTools4Dev` | Avalonia UI frontend — views, ViewModels, DI wiring, Serilog bootstrap |
| `src/KubeTools4Dev.Core` | Core logic — Kubernetes service, port forwarding, settings |

### MVVM Pattern (CommunityToolkit.Mvvm)

- **ViewModels** inherit `ViewModelBase` → `ObservableObject`.
- Observable properties use `[ObservableProperty]` on a `private` backing field; the generator creates the public property.
- Commands use `[RelayCommand]` on `private` methods; the generator creates the `*Command` property.
- Partial `OnXxxChanged(value)` methods are used for property-change side effects (e.g., `OnFilterTextChanged`, `OnRefreshIntervalSecondsChanged`).
- UI thread updates from background tasks always go through `Dispatcher.UIThread.InvokeAsync(...)`.

### Dependency Injection (`App.axaml.cs`)

All services and ViewModels are registered in `App.OnFrameworkInitializationCompleted` via `ConfigureServices`. Services are **Singletons**. `PodListViewModel` and `ServiceListViewModel` are **Transient**; `SettingsViewModel` and `MainViewModel` are **Singleton**.

### Core Services (`KubeTools4Dev.Core`)

- **`IKubernetesService` / `KubernetesService`** — wraps the `KubernetesClient` (`k8s`) library. Exposes async methods for pods, services, metrics, log streaming, and watch streams (`IAsyncEnumerable`). `""` or `"*"` as namespace means all namespaces.
- **`IPortForwardService` / `PortForwardService`** — implements `kubectl port-forward` using raw `Socket` + WebSocket tunneling via `WebSocketNamespacedPodPortForwardAsync`. Binds `IPAddress.IPv6Any` with `DualMode = true` so both `127.0.0.1` and `::1` clients connect. Resolves service → pod via label selectors with a 10-second pod-name cache.
- **`ISettingsService` / `SettingsService`** — loads defaults from `appsettings.json` (`Settings` section), persists user overrides to `%APPDATA%\KubeTools4Dev\settings.json`. Fires `SettingsChanged` event on save.
- **`IUpdateService` / `UpdateService`** — wraps Velopack for in-app updates.

### Configuration & Logging

- `appsettings.json` (copied to output) provides app defaults including Serilog configuration and `Settings:*` sections.
- `Program.cs` bootstraps Serilog by merging `appsettings.json` with user-overrides from `%APPDATA%\KubeTools4Dev\settings.json` before `BuildAvaloniaApp()` is called.
- Velopack's `VelopackApp.Build().Run()` **must** be the very first call in `Main`.

---

## Key Conventions

### Coding Style

- **.NET 10**, file-scoped namespaces, nullable enabled.
- **Always use braces** for all control flow blocks, even single-line.
- Prefer `var` for local variables.
- Prefer **expression-bodied members** where concise.
- Private fields: `_camelCase`.
- **XML doc comments are required on all public classes and members** (`GenerateDocumentationFile` is `true`; missing docs raise CS1591).

### Logging

- The `DMNSN.Core` NuGet package adds extension methods (`logger.Information(...)`, `logger.Error(...)`, etc.) as convenience wrappers over `ILogger<T>`. Both the DMNSN-style and standard `LogXxx` style appear in the codebase — prefer the standard `LogXxx` methods for new code.
- Inject `ILogger<T>` via constructor. Services in `KubeTools4Dev.Core` use primary constructors (`public class Foo(ILogger<Foo> logger)`).

### Async & Cancellation

- All I/O is `async`/`await`. Always accept and forward `CancellationToken` parameters.
- Fire-and-forget background loops (watch, metrics polling) are started with `_ = SomeAsync(token)` and rely on the `CancellationToken` for lifecycle.
- Log streams and pod-watch loops restart automatically after errors with a short `Task.Delay`.

### ViewModels that own background work implement `IDisposable`

Use the full dispose pattern (`Dispose(bool disposing)` + `GC.SuppressFinalize(this)`). Cancel and dispose `CancellationTokenSource` instances in `Dispose(true)`. Unsubscribe from events (`SettingsChanged`, timer ticks).

### Port Forwarding Key Detail

`PortForwardService` does **service-level** forwarding by resolving which pod backs the service (via label selectors) rather than forwarding directly to a pod. The pod-name cache is invalidated on WebSocket connect failure to handle pod restarts.
