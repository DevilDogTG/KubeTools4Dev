# KubeTools4Dev

**KubeTools4Dev** is a cross-platform desktop application designed to simplify daily Kubernetes development tasks. Built with .NET 10 and Avalonia UI, it provides a modern, responsive interface for monitoring pods and managing service port forwarding without relying on complex command-line arguments.

## Features

### 🔌 Dashboard & Connection
- **Easy Connection**: Automatically detects and connects to your Kubernetes cluster using your local `kubeconfig` file.
- **Cluster Status**: Displays the current connection status and active cluster context name.

### 📦 Pod Monitoring
- **Real-time Updates**: View a live list of pods across all namespaces.
- **Comprehensive Details**: See essential pod information including Name, Namespace, Status, Age, and Restart counts. Statuses are dynamically parsed with color coding for quick issue spotting (e.g., `CrashLoopBackOff` appears in red).
- **Resource Metrics**: View real-time CPU and RAM usage directly in the datagrid (requires Kubernetes Metrics Server installed on your cluster).
- **Live Logs Streaming**: Instantly open a resizable side-panel to stream real-time raw pod logs with smart auto-scrolling.
- **Describe & Events**: Quickly inspect a pod's YAML configuration along with its real-time Kubernetes events in the side-panel.
- **Filtering**: Quickly find pods by filtering on Name or Namespace.
- **Auto-Refresh**: Configurable refresh interval to keep data up-to-date.

### 🔗 Service Management & Port Forwarding
- **Service Discovery**: Browse all available services and their TCP ports.
- **One-Click Port Forwarding**: Start port forwarding for any service with a single click.
- **Visual Status**: Clear visual indicators for active forwarding sessions.
- **Stop All**: Instantly stop all active port forwarding sessions with a dedicated "Stop All" button.
- **Exclusion Rules**: Permanently exclude specific services to prevent accidental forwarding (configurable in Settings).
- **Resilient Sessions**: Port-forward connections survive client disconnects and browser refreshes without silently dropping. The listener recovers from aborted connections automatically, and sessions are never terminated by an idle timeout — ensuring stable long-running tunnels.

### ⚙️ Settings
- **General**: Configure application logging levels and view the log file path.
- **Pods Configuration**: Adjust data refresh intervals and watch retry delays.
- **Service Configuration**: Manage excluded services and hide specific services by name or type.
- **Themes**: Modern UI with support for light/dark modes (system default).

## Download & Install

Pre-built installers are attached to each [GitHub Release](https://github.com/DevilDogTG/KubeTools4Dev/releases). Self-contained — no separate .NET runtime install needed.

### Windows
Download `KubeTools4Dev-Setup-X.Y.Z.exe` and run it. A portable `.zip` is also published if you prefer not to install.

### Linux (Ubuntu / Debian) — apt repository

```bash
# 1. Trust the signing key (one-time)
sudo install -d /etc/apt/keyrings
curl -fsSL https://devildogtg.github.io/KubeTools4Dev/KubeTools4Dev.gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/kubetools4dev.gpg

# 2. Register the repo (one-time)
echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/kubetools4dev.gpg] \
https://devildogtg.github.io/KubeTools4Dev/ stable main" \
  | sudo tee /etc/apt/sources.list.d/kubetools4dev.list

# 3. Install — and `apt upgrade` will pick up every future release
sudo apt update
sudo apt install kubetools4dev
kubetools4dev   # or launch from your application menu
```

The `signed-by=` form scopes trust of the signing key to this one repository (the modern replacement for the deprecated `apt-key add` flow). `arch=amd64` keeps `apt update` from probing for architectures we don't publish.

To remove the repository later:

```bash
sudo rm /etc/apt/sources.list.d/kubetools4dev.list \
       /etc/apt/keyrings/kubetools4dev.gpg
sudo apt update
```

#### Manual download (air-gapped fallback)
Download `KubeTools4Dev_X.Y.Z_amd64.deb` from the release page, then:

```bash
sudo apt install ./KubeTools4Dev_X.Y.Z_amd64.deb
```

Uninstall with `sudo apt remove kubetools4dev`.

Requires `libicu` (any of 66/70/72/74 — present on every supported Ubuntu and Debian). `kubectl` is **not** required at runtime; the app talks to your cluster directly through your `~/.kube/config`.

### WSL2 (Ubuntu)
The apt repository (and the manual `.deb`) works identically inside WSL Ubuntu and WSL Debian. On **Windows 11**, WSLg displays the Avalonia window automatically and the app appears in the Windows Start menu. On **Windows 10**, install an X server (VcXsrv / X410) and export `DISPLAY` before launching.

Note: your Linux-side `~/.kube/config` is **separate** from the Windows-side one. If you already manage clusters from PowerShell, copy or symlink it once:

```bash
ln -s /mnt/c/Users/<your-name>/.kube/config ~/.kube/config
```

## Prerequisites (Building from Source)

Only required if you intend to build the app yourself instead of installing a release:

- **.NET 10.0 SDK**: The application targets .NET 10.0.
- **kubeconfig**: A valid Kubernetes configuration file (usually `~/.kube/config`).

## Getting Started (Source Build)

1.  **Clone the Repository**
    ```bash
    git clone https://github.com/DevilDogTG/KubeTools4Dev.git
    cd KubeTools4Dev
    ```

2.  **Restore Dependencies**
    ```bash
    dotnet restore
    ```

3.  **Run the Application**
    ```bash
    dotnet run --project src/KubeTools4Dev/KubeTools4Dev.csproj
    ```

## Usage

1.  **Connect**: Upon launch, the application attempts to connect to your current Kubernetes context.
2.  **Monitor Pods**: Navigate to the **Pods** tab to view the status of your workloads. Use the search bar to filter results.
3.  **Forward Ports**: Go to the **Services** tab. Click the "Play" (triangle) icon next to a service port to start forwarding. The icon will change to a "Stop" (square) icon.
4.  **Configure**: Use the **Settings** tab to tweak refresh rates or manage excluded services.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Developer Workflows (Agent Skills)

Contributor workflows are managed as AI agent skills stored in [`.agent-brains/skills/`](.agent-brains/skills/). Invoke them with the `sk-` prefix in a Gemini/Copilot/Claude session.

| Skill | Description |
|-------|-------------|
| `sk-finish-feature` | Runs preflights (clean tree, rebase from main, build, test) then creates or updates the GitHub PR with an AI-generated description. |
| `sk-pr-review` | Reviews the PR diff with AI and posts structured findings (🔴 Critical / 🟡 Warning / 🔵 Info) as a GitHub comment. |
| `sk-release` | Analyzes changes since the last tag, recommends the version bump (major/minor/patch), generates release notes, and drives release → tag → publish end-to-end. |

## Author

**Supawat Tanmanee** (DevDogs)
- Repository: [https://github.com/DevilDogTG/KubeTools4Dev](https://github.com/DevilDogTG/KubeTools4Dev)
- Website: [https://devildogtg.dmnsn.com](https://devildogtg.dmnsn.com)
