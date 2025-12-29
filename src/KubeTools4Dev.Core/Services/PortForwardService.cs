using DMNSN.Core;
using k8s;
using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Port forwarding service using KubernetesClient library.
/// </summary>
/// <seealso cref="IPortForwardService" />
public partial class PortForwardService(
    IKubernetesService kubernetesService,
    ILogger<PortForwardService> logger
) : IPortForwardService
{
    /// <summary>
    /// Connection timeout for WebSocket connections.
    /// </summary>
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retry delay when errors occur.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The active forwards with their cancellation token sources.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeForwards = new();

    /// <summary>
    /// Starts the service port forward asynchronous.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="targetPort">The target port.</param>
    /// <param name="localPort">The local port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartServicePortForwardAsync(
        string serviceName,
        string namespaceName,
        object targetPort,
        int localPort,
        CancellationToken cancellationToken)
    {
        var key = $"{namespaceName}/{serviceName}:{localPort}";

        // Resolve port to integer
        int remotePort = targetPort switch
        {
            int iVal => iVal,
            string sVal => int.TryParse(sVal, out var parsed)
                ? parsed
                : throw new ArgumentException($"Invalid port: {sVal}"),
            IntOrString iosVal => iosVal.Value != null
                ? int.Parse(iosVal.Value)
                : iosVal.ToInt(),
            _ => throw new ArgumentException($"Unsupported port type: {targetPort?.GetType().Name}")
        };

        // Create a linked cancellation token source
        var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeForwards[key] = linkedCancellationToken;

        try
        {
            // Start two listeners: one for IPv4 (Loopback) and one for IPv6 (IPv6Loopback)
            var ipv4Task = RunListenerAsync(
                IPAddress.Loopback,
                localPort,
                serviceName,
                namespaceName,
                remotePort,
                linkedCancellationToken.Token);

            var ipv6Task = RunListenerAsync(
                IPAddress.IPv6Loopback,
                localPort,
                serviceName,
                namespaceName,
                remotePort,
                linkedCancellationToken.Token);

            await Task.WhenAll(ipv4Task, ipv6Task);
        }
        finally
        {
            _activeForwards.TryRemove(key, out _);
        }
    }

    private async Task RunListenerAsync(
        IPAddress address,
        int localPort,
        string serviceName,
        string namespaceName,
        int servicePort,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket? listener = null;

                try
                {
                    // Resolve pod and target port from service
                    var resolutionResult = await ResolvePodAndPortAsync(
                        serviceName,
                        namespaceName,
                        servicePort,
                        cancellationToken);

                    if (resolutionResult == null)
                    {
                        LogNoPodFound(serviceName, namespaceName);
                        await Task.Delay(RetryDelay, cancellationToken);
                        continue;
                    }

                    var (podName, targetPort) = resolutionResult.Value;

                    LogPortForwardStarting(serviceName, podName, localPort, targetPort);
                    LogPortResolution(servicePort, targetPort, podName);

                    // Create socket listener
                    var ipEndPoint = new IPEndPoint(address, localPort);

                    listener = new Socket(
                        address.AddressFamily,
                        SocketType.Stream,
                        ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    listener.Bind(ipEndPoint);
                    listener.Listen(100);

                    LogPortForwardListening(localPort, podName, targetPort);

                    // Accept connections and forward them
                    await AcceptAndForwardConnectionsAsync(
                        listener,
                        podName,
                        namespaceName,
                        targetPort,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    LogPortInUse(localPort);
                    break;
                }
                catch (Exception ex)
                {
                    LogPortForwardError(serviceName, ex.Message);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(RetryDelay, cancellationToken);
                    }
                }
                finally
                {
                    try { listener?.Close(); } catch { }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
    }

    /// <summary>
    /// Stops all active port forwards.
    /// </summary>
    public void StopAll()
    {
        logger.Information("Stopping all port forwards...");

        // Cancel all active forwards
        foreach (var kvp in _activeForwards.ToArray())
        {
            try
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                LogPortForwardStopFailed(kvp.Key);
                logger.Error(ex, "Failed to cancel port forward..");
            }
        }
        _activeForwards.Clear();
    }

    /// <summary>
    /// Accepts incoming connections and forwards each one with its own WebSocket tunnel.
    /// </summary>
    private async Task AcceptAndForwardConnectionsAsync(
        Socket listener,
        string podName,
        string namespaceName,
        int remotePort,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Accept connection
                var handler = await listener.AcceptAsync(cancellationToken);
                LogConnectionAccepted(podName, remotePort);

                // Handle each connection in a separate task with its own WebSocket
                _ = Task.Run(async () =>
                {
                    await HandleSingleConnectionAsync(
                        handler,
                        podName,
                        namespaceName,
                        remotePort,
                        cancellationToken);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                // Listener closed
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Copies data from socket to stream (local -> pod).
    /// </summary>
    private void CopySocketToStream(
        Socket socket,
        Stream stream,
        string podName,
        CancellationToken cancellationToken)
    {
        const string Direction = "local->pod";
        var buffer = new byte[4096];
        long totalBytes = 0;
        bool hasLoggedStart = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.Connected)
            {
                int bytesReceived = socket.Receive(buffer);
                if (bytesReceived == 0)
                {
                    break; // Connection closed
                }

                if (!hasLoggedStart)
                {
                    LogTrafficStart(podName, Direction);
                    hasLoggedStart = true;
                }

                stream.Write(buffer, 0, bytesReceived);
                stream.Flush();
                totalBytes += bytesReceived;
            }
        }
        catch (SocketException ex)
        {
            LogStreamClosed(podName, Direction, ex.Message);
        }
        catch (IOException ex)
        {
            LogStreamClosed(podName, Direction, ex.Message);
        }
        catch (ObjectDisposedException ex)
        {
            // Socket or stream disposed - normal during shutdown
            LogStreamClosed(podName, Direction, ex.Message);
        }
        finally
        {
            if (hasLoggedStart)
            {
                LogTrafficEnd(podName, Direction, totalBytes);
            }
        }
    }

    /// <summary>
    /// Copies data from stream to socket (pod -> local).
    /// </summary>
    private void CopyStreamToSocket(
        Stream stream,
        Socket socket,
        string podName,
        CancellationToken cancellationToken)
    {
        const string Direction = "pod->local";
        var buffer = new byte[4096];
        long totalBytes = 0;
        bool hasLoggedStart = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // Stream closed
                }

                if (!hasLoggedStart)
                {
                    LogTrafficStart(podName, Direction);
                    hasLoggedStart = true;
                }

                socket.Send(buffer, bytesRead, SocketFlags.None);
                totalBytes += bytesRead;
            }
        }
        catch (SocketException ex)
        {
            LogStreamClosed(podName, Direction, ex.Message);
        }
        catch (IOException ex)
        {
            LogStreamClosed(podName, Direction, ex.Message);
        }
        catch (ObjectDisposedException ex)
        {
            // Socket or stream disposed - normal during shutdown
            LogStreamClosed(podName, Direction, ex.Message);
        }
        finally
        {
             if (hasLoggedStart)
             {
                 LogTrafficEnd(podName, Direction, totalBytes);
             }
        }
    }

    /// <summary>
    /// Handles a single connection with its own WebSocket tunnel to the pod.
    /// </summary>
    private async Task HandleSingleConnectionAsync(Socket handler, string podName, string namespaceName, int remotePort, CancellationToken cancellationToken)
    {
        WebSocket? webSocket = null;
        StreamDemuxer? demuxer = null;

        try
        {
            // Create WebSocket connection for this specific connection
            using var timeoutCts = new CancellationTokenSource(ConnectionTimeout);
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
                token1: cancellationToken,
                token2: timeoutCts.Token);

            try
            {
                webSocket = await kubernetesService.Client
                    .WebSocketNamespacedPodPortForwardAsync(
                        name: podName,
                        @namespace: namespaceName,
                        ports: [remotePort],
                        webSocketSubProtocol: "v4.channel.k8s.io",
                        cancellationToken: connectCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                LogConnectionTimeout(podName, remotePort);
                return;
            }

            // Create stream demuxer for port forwarding
            demuxer = new StreamDemuxer(
                webSocket,
                StreamType.PortForward);
            demuxer.Start();

            // Get the stream for the port (Channel 0)
            var podStream = demuxer.GetStream((byte?)0, (byte?)0);

            // Get the error stream (Channel 1)
            var errorStream = demuxer.GetStream((byte?)1, (byte?)1);

            // Read error stream in background
            var errorTask = Task.Run(async () =>
            {
                try
                {
                    using var reader = new StreamReader(errorStream);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line != null)
                        {
                            LogPodErrorOutput(podName, line);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch { /* Ignore error stream errors */ }
            }, cancellationToken);

            // Create tasks for bidirectional copy (like the official example)
            var socketToPod = Task.Run(() =>
                CopySocketToStream(
                    socket: handler,
                    stream: podStream,
                    podName: podName,
                    cancellationToken: cancellationToken),
                cancellationToken);
            var podToSocket = Task.Run(() =>
                CopyStreamToSocket(
                    stream: podStream,
                    socket: handler,
                    podName: podName,
                    cancellationToken: cancellationToken),
                cancellationToken);

            // Wait for either direction to complete (connection closed)
            await Task.WhenAny(socketToPod, podToSocket);
        }
        catch (WebSocketException ex)
        {
            LogWebSocketError(podName, ex.Message);
        }
        catch (Exception ex)
        {
            LogConnectionError(podName, ex.Message);
        }
        finally
        {
            try { handler.Close(); } catch { }
            try { demuxer?.Dispose(); } catch { }
            try { webSocket?.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// Resolves a pod and the actual target port from a service.
    /// Handles mapping Service Port -> Target Port (Int or String).
    /// </summary>
    private async Task<(string PodName, int TargetPort)?> ResolvePodAndPortAsync(
        string serviceName,
        string namespaceName,
        int servicePort,
        CancellationToken cancellationToken)
    {
        var client = kubernetesService.Client;

        // 1. Get Service to find Selector and TargetPort mapping
        var service = await client.CoreV1.ReadNamespacedServiceAsync(
            name: serviceName,
            namespaceParameter: namespaceName,
            cancellationToken: cancellationToken);

        if (service?.Spec?.Selector == null || service.Spec.Selector.Count == 0)
        {
            return null;
        }

        // 2. Find the ServicePort entry matching our input port
        var servicePortEntry = service.Spec.Ports?.FirstOrDefault(p => p.Port == servicePort);
        var targetPortVal = servicePortEntry?.TargetPort;

        // 3. Find a Pod matching the selector
        var labelSelector = string.Join(",", service.Spec.Selector.Select(kv => $"{kv.Key}={kv.Value}"));
        var pods = await client.CoreV1.ListNamespacedPodAsync(
            namespaceParameter: namespaceName,
            labelSelector: labelSelector,
            cancellationToken: cancellationToken);

        var runningPod = pods.Items
            .FirstOrDefault(p =>
                p.Status.Phase == "Running"
                && p.Status.ContainerStatuses?.All(c => c.Ready == true) == true);

        if (runningPod == null)
        {
            return null;
        }

        int resolvedPort = servicePort; // Default fallback

        // 4. Resolve TargetPort
        if (targetPortVal != null)
        {
            // Case A: TargetPort is an Integer (e.g., 8080)
            if (targetPortVal.Value != null && int.TryParse(targetPortVal.Value, out int parsedPort)) 
            {
                resolvedPort = parsedPort;
            }
            // Case B: TargetPort is a String/Name (e.g., "http") or value was not an int
            else 
            {
                string portName = targetPortVal.Value; // e.g. "http"
                
                // Look up container port by name in the Pod Spec
                var containerPort = runningPod.Spec.Containers
                    .SelectMany(c => c.Ports ?? [])
                    .FirstOrDefault(p => p.Name == portName);

                if (containerPort != null)
                {
                    resolvedPort = containerPort.ContainerPort;
                }
                else
                {
                   // Fallback logic matches original implementation
                }
            }
        }

        return (runningPod.Metadata.Name, resolvedPort);
    }
}
