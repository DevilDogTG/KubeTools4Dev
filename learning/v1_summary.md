# ElysianMonitor V1 - Project Summary & Learnings

## 1. Goal
To develop **ElysianMonitor**, a cross-platform desktop application (Windows/Linux) using **.NET 10** and **Avalonia UI**. The primary purpose is to replace usage of PowerShell scripts for:
- **Monitoring Kubernetes Pods**: Real-time status, restarts, age, and logs.
- **Port Forwarding**: Easy, one-click management of service port forwarding.

## 2. Key Challenges & Issues Attempted

### A. Port Forwarding Instability (Native Implementation)
**Initial Approach**: 
We attempted to implement port forwarding natively using `System.Net.Sockets.TcpListener` and the `KubernetesClient` library (`WebSocketNamespacedPodPortForwardAsync`).
**Issues**:
- **Data Flow**: Connections were established ("Accepted connection"), but data often failed to transfer or hung.
- **Stream Demuxing**: The `StreamDemuxer` required to handle the multiplexed WebSocket stream (data channel vs error channel) was complex to manage manually.
- **Buffering**: We encountered issues where data sat in buffers without being flushed to the client.
- **Stability**: The native implementation was fragile, often resulting in "Stream closed" or `IOException` errors immediately upon connection.

### B. Target Port Resolution
**Issue**: 
Kubernetes Services define a `Port` (exposed) and a `TargetPort` (container).
- Our app initially resolved the `TargetPort` (e.g., 18888) and tried to forward to that.
- However, standard `kubectl` workflows often forward `Local:ServicePort` -> `Pod:TargetPort`.
- Attempting to forward directly to the `TargetPort` causing confusion when it didn't match the user's expectation of "forwarding port 8088".

### C. UI & Data Presentation
**Issues**:
- **Service Flattening**: Services with multiple ports were initially not displaying correctly (only the first port was shown).
- **Auto-Refresh**: Pod ages and statuses were static and required manual refreshing.

## 3. Solutions

### A. Switching to `kubectl` Wrapper (The "Silver Bullet")
**Solution**: 
Instead of fighting the low-level stream complexities of the C# Kubernetes Client, we refactored `PortForwardService` to wrap the standard `kubectl port-forward` command.
**Implementation**:
- Spawn a `Process` running `kubectl`.
- Arguments: `port-forward svc/{serviceName} {localPort}:{remotePort} -n {namespace} --address 0.0.0.0`.
- **Benefit**: This guarantees behavior identical to the CLI, effectively solving all connectivity, buffering, and stability issues.

### B. Defaulting to Service Port
**Solution**:
Updated the `ServiceListViewModel` to default the destination port to the **Service Port** (e.g., 8088) rather than the Container Port.
- `kubectl` handles the resolution of `Service Port` -> `Container Target Port` automatically.
- This creates a user experience consistent with CLI usage.

### C. UI Enhancements
- **Multi-Port Support**: Refactored the Service List to "flatten" entries, creating a row for every port defined in a service.
- **Auto-Refresh**: Implemented a `DispatcherTimer` to update Pod "Age" and "Status" every second (configurable).
- **Serilog**: Integrated structured file and console logging to aid in deep debugging.

## 4. Technical Stack
- **Framework**: .NET 10.0
- **UI**: Avalonia UI
- **MVVM**: CommunityToolkit.Mvvm
- **Kubernetes**: KubernetesClient (for fetching metadata), `kubectl` (for forwarding)
- **Logging**: Serilog

## 5. Future Considerations
- **Native Implementation**: If native forwarding is required (to remove `kubectl` dependency), we would need to investigate a more robust `StreamDemuxer` implementation or look at the underlying protocol more closely.
- **Linux Testing**: Verify `process.Start` behavior and path resolution for `kubectl` on Linux environments.
