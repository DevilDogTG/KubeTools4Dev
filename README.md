# KubeTools4Dev

**KubeTools4Dev** is a cross-platform desktop application designed to simplify daily Kubernetes development tasks. Built with .NET 10 and Avalonia UI, it provides a modern, responsive interface for monitoring pods and managing service port forwarding without relying on complex command-line arguments.

## Features

### 🔌 Dashboard & Connection
- **Easy Connection**: Automatically detects and connects to your Kubernetes cluster using your local `kubeconfig` file.
- **Cluster Status**: Displays the current connection status and active cluster context name.

### 📦 Pod Monitoring
- **Real-time Updates**: View a live list of pods across all namespaces.
- **Comprehensive Details**: See essential pod information including Name, Namespace, Status, Age, and Restart counts.
- **Filtering**: Quickly find pods by filtering on Name or Namespace.
- **Auto-Refresh**: Configurable refresh interval to keep data up-to-date.

### 🔗 Service Management & Port Forwarding
- **Service Discovery**: browse all available services and their TCP ports.
- **One-Click Port Forwarding**: Start port forwarding for any service with a single click.
- **Visual Status**: Clear visual indicators for active forwarding sessions.
- **Stop All**: Instantly stop all active port forwarding sessions with a dedicated "Stop All" button.
- **Exclusion Rules**: Permanently exclude specific services to prevent accidental forwarding (configurable in Settings).

### ⚙️ Settings
- **General**: Configure application logging levels and view the log file path.
- **Pods Configuration**: Adjust data refresh intervals and watch retry delays.
- **Service Configuration**: Manage excluded services and hide specific services by name or type.
- **Themes**: Modern UI with support for light/dark modes (system default).

## Prerequisites

Before running KubeTools4Dev, ensure you have the following installed:

- **.NET 10.0 SDK**: The application targets .NET 10.0.
- **kubectl**: Required for underlying cluster interactions.
- **kubeconfig**: A valid Kubernetes configuration file (usually located at `~/.kube/config`).

## Getting Started

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

## Author

**Supawat Tanmanee** (DevDogs)
- Repository: [https://github.com/DevilDogTG/KubeTools4Dev](https://github.com/DevilDogTG/KubeTools4Dev)
- Website: [https://devildogtg.dmnsn.com](https://devildogtg.dmnsn.com)
