using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.Shares;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// 
/// </summary>
/// <seealso cref="IPortForwardService" />
public partial class PortForwardService(
    ILogger<PortForwardService> logger
) : IPortForwardService
{
    /// <summary>
    /// The active forwards
    /// </summary>
    private readonly ConcurrentDictionary<string, Process> _activeForwards = new();

    /// <summary>
    /// Starts the service port forward asynchronous.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="targetPort">The target port.</param>
    /// <param name="localPort">The local port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartServicePortForwardAsync(string serviceName, string namespaceName, object targetPort, int localPort, CancellationToken cancellationToken)
    {
        var key = $"{namespaceName}/{serviceName}:{localPort}";

        // Use kubectl port-forward directy
        // Command: kubectl port-forward svc/{serviceName} {localPort}:{targetPort} -n {namespace}

        // Resolve port string
        string remotePortStr = targetPort switch
        {
            int iVal => iVal.ToString(),
            string sVal => sVal,
            IntOrString ios => ios.Value ?? ios.ToInt().ToString(),
            _ => targetPort?.ToString() ?? string.Empty
        };

        // If it's a named port, kubectl usually handles it if we target the pod, but for service it might need numeric.
        // However, user said "8088->8088" works. 
        // Let's rely on kubectl's capability.

        while (!cancellationToken.IsCancellationRequested)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = $"port-forward svc/{serviceName} {localPort}:{remotePortStr} -n {namespaceName} --address 0.0.0.0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            logger.LogInformation("Starting kubectl: {FileName} {Arguments}", processStartInfo.FileName, processStartInfo.Arguments);

            var process = new Process { StartInfo = processStartInfo };

            // Logging handlers
            process.OutputDataReceived += (sender, args)
                => LogKubectlInfo(serviceName, args.Data);
            process.ErrorDataReceived += (sender, args)
                => LogKubectlError(serviceName, args.Data);

            if (!process.Start())
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError("Failed to start kubectl process for {ServiceName}", serviceName);
                }
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            _activeForwards.AddOrUpdate(key, process, (k, oldProcess) => process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Register cancellation to kill process
            using var ctr = cancellationToken.Register(() =>
            {
                logger.LogInformation("Stopping port forward for {ServiceName}", serviceName);
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Kill tree if possible
                        process.WaitForExit(1000);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error stopping kubectl process");
                }
            });

            // Loop to keep task alive until cancelled or process exits
            try
            {
                await process.WaitForExitAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    break; // Normal stop
                }

                if (process.ExitCode != 0)
                {
                    logger.LogWarning("kubectl exited with code {Code}. Restarting in 3s...", process.ExitCode);
                }
                else
                {
                    logger.LogWarning("kubectl exited. Restarting in 3s...");
                }
            }
            catch (OperationCanceledException)
            {
                break; // Normal stop
            }
            finally
            {
                // Ensure process is removed from active list if this specific instance is there?
                // Actually, AddOrUpdate handles replacements.
                // We should remove ONLY if we are breaking out of the loop (i.e. truly stopping).
                // But inside the loop, we want it in the list.
            }

            // Wait before restart
            try
            {
                await Task.Delay(3000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Final cleanup
        _activeForwards.TryRemove(key, out _);
    }

    /// <summary>
    /// Stops all.
    /// </summary>
    public void StopAll()
    {
        logger.Info("Stopping all port forwards...");
        Parallel.ForEach(_activeForwards.ToArray(), activeForward =>
        {
            try
            {
                if (!activeForward.Value.HasExited)
                {
                    activeForward.Value.Kill(true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to kill process for {Key}", activeForward.Key);
            }
        });
        _activeForwards.Clear();
    }
}
