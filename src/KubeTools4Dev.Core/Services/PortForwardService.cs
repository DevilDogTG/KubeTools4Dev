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
    public async Task StartServicePortForwardAsync(string serviceName, string namespaceName, object targetPort, int localPort, CancellationToken cancellationToken)
    {
        var key = $"{namespaceName}/{serviceName}:{localPort}";

        // Resolve port to integer
        int remotePort = targetPort switch
        {
            int iVal => iVal,
            string sVal => int.TryParse(sVal, out var parsed) ? parsed : throw new ArgumentException($"Invalid port: {sVal}"),
            IntOrString ios => ios.Value != null ? int.Parse(ios.Value) : ios.ToInt(),
            _ => throw new ArgumentException($"Unsupported port type: {targetPort?.GetType().Name}")
        };

        // Create a linked cancellation token source
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeForwards[key] = linkedCts;

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                Socket? listener = null;

                try
                {
                    // Resolve pod from service
                    var podName = await ResolvePodFromServiceAsync(serviceName, namespaceName, linkedCts.Token);
                    if (string.IsNullOrEmpty(podName))
                    {
                        LogNoPodFound(serviceName, namespaceName);
                        await Task.Delay(RetryDelay, linkedCts.Token);
                        continue;
                    }

                    LogPortForwardStarting(serviceName, podName, localPort, remotePort);

                    // Create socket listener
                    var ipEndPoint = new IPEndPoint(IPAddress.Any, localPort);
                    listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    listener.Bind(ipEndPoint);
                    listener.Listen(100);

                    LogPortForwardListening(localPort, podName, remotePort);

                    // Accept connections and forward them - each connection gets its own WebSocket
                    await AcceptAndForwardConnectionsAsync(listener, podName, namespaceName, remotePort, linkedCts.Token);
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
                    if (!linkedCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(RetryDelay, linkedCts.Token);
                    }
                }
                finally
                {
                    try { listener?.Close(); } catch { }
                }
            }
        }
        finally
        {
            _activeForwards.TryRemove(key, out _);
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
                logger.LogWarning(ex, "Failed to cancel port forward for {Key}", kvp.Key);
            }
        }
        _activeForwards.Clear();
    }

    /// <summary>
    /// Accepts incoming connections and forwards each one with its own WebSocket tunnel.
    /// </summary>
    private async Task AcceptAndForwardConnectionsAsync(Socket listener, string podName, string namespaceName, int remotePort, CancellationToken cancellationToken)
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
                    await HandleSingleConnectionAsync(handler, podName, namespaceName, remotePort, cancellationToken);
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
    private void CopySocketToStream(Socket socket, Stream stream, string podName, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.Connected)
            {
                int bytesReceived = socket.Receive(buffer);
                if (bytesReceived == 0)
                {
                    break; // Connection closed
                }
                stream.Write(buffer, 0, bytesReceived);
                stream.Flush();
            }
        }
        catch (SocketException ex)
        {
            LogStreamClosed(podName, "local->pod", ex.Message);
        }
        catch (IOException ex)
        {
            LogStreamClosed(podName, "local->pod", ex.Message);
        }
        catch (ObjectDisposedException)
        {
            // Socket or stream disposed - normal during shutdown
        }
    }

    /// <summary>
    /// Copies data from stream to socket (pod -> local).
    /// </summary>
    private void CopyStreamToSocket(Stream stream, Socket socket, string podName, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // Stream closed
                }
                socket.Send(buffer, bytesRead, SocketFlags.None);
            }
        }
        catch (SocketException ex)
        {
            LogStreamClosed(podName, "pod->local", ex.Message);
        }
        catch (IOException ex)
        {
            LogStreamClosed(podName, "pod->local", ex.Message);
        }
        catch (ObjectDisposedException)
        {
            // Socket or stream disposed - normal during shutdown
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
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                webSocket = await kubernetesService.Client
                    .WebSocketNamespacedPodPortForwardAsync(
                        podName,
                        namespaceName,
                        new[] { remotePort },
                        "v4.channel.k8s.io",
                        cancellationToken: connectCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                LogConnectionTimeout(podName, $"Timed out connecting to pod {podName}:{remotePort}");
                return;
            }

            // Create stream demuxer for port forwarding
            demuxer = new StreamDemuxer(webSocket, StreamType.PortForward);
            demuxer.Start();

            // Get the stream for the port
            var podStream = demuxer.GetStream((byte?)0, (byte?)0);

            // Create tasks for bidirectional copy (like the official example)
            var socketToPod = Task.Run(() => CopySocketToStream(handler, podStream, podName, cancellationToken), cancellationToken);
            var podToSocket = Task.Run(() => CopyStreamToSocket(podStream, handler, podName, cancellationToken), cancellationToken);

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
    /// Resolves a pod name from a service by looking up pods matching the service's selector.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of a running pod matching the service selector, or null if none found.</returns>
    private async Task<string?> ResolvePodFromServiceAsync(string serviceName, string namespaceName, CancellationToken cancellationToken)
    {
        var client = kubernetesService.Client;

        // Get the service to find its selector
        var service = await client.CoreV1.ReadNamespacedServiceAsync(serviceName, namespaceName, cancellationToken: cancellationToken);
        var selector = service.Spec.Selector;

        if (selector == null || selector.Count == 0)
        {
            return null;
        }

        // Build label selector string
        var labelSelector = string.Join(",", selector.Select(kv => $"{kv.Key}={kv.Value}"));

        // List pods matching the selector
        var pods = await client.CoreV1.ListNamespacedPodAsync(
            namespaceName,
            labelSelector: labelSelector,
            cancellationToken: cancellationToken);

        // Return the first running pod
        var runningPod = pods.Items
            .FirstOrDefault(p => p.Status.Phase == "Running" &&
                                  p.Status.ContainerStatuses?.All(c => c.Ready == true) == true);

        return runningPod?.Metadata.Name;
    }
}
