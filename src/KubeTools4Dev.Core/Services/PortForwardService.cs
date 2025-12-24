using k8s.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KubeTools4Dev.Core.Services;

public interface IPortForwardService
{
    // We keep the signature similar but implementation changes
    Task StartServicePortForwardAsync(string serviceName, string namespaceName, object targetPort, int localPort, CancellationToken cancellationToken);
    void StopAll();
}

public class PortForwardService : IPortForwardService
{
    private readonly ILogger<PortForwardService> _logger;
    private readonly ConcurrentDictionary<string, Process> _activeForwards = new();

    public PortForwardService(ILogger<PortForwardService> logger)
    {
        _logger = logger;
    }

    public void StopAll()
    {
        _logger.LogInformation("Stopping all port forwards...");
        foreach (var kvp in _activeForwards)
        {
            try
            {
                if (!kvp.Value.HasExited)
                {
                    kvp.Value.Kill(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process for {Key}", kvp.Key);
            }
        }
        _activeForwards.Clear();
    }

    public async Task StartServicePortForwardAsync(string serviceName, string namespaceName, object targetPort, int localPort, CancellationToken cancellationToken)
    {
        var key = $"{namespaceName}/{serviceName}:{localPort}";
        
        // Use kubectl port-forward directy
        // Command: kubectl port-forward svc/{serviceName} {localPort}:{targetPort} -n {namespace}

        // Resolve port string
        string remotePortStr;
        if (targetPort is int iVal) remotePortStr = iVal.ToString();
        else if (targetPort is string sVal) remotePortStr = sVal; // Assume resolved or named port works with kubectl
        else if (targetPort is IntOrString ios) remotePortStr = ios.Value ?? ios.ToInt().ToString();
        else remotePortStr = targetPort.ToString();

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

            _logger.LogInformation("Starting kubectl: {FileName} {Arguments}", processStartInfo.FileName, processStartInfo.Arguments);

            var process = new Process { StartInfo = processStartInfo };

            // Logging handlers
            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) _logger.LogInformation("[kubectl {ServiceName}] {Data}", serviceName, args.Data);
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) _logger.LogError("[kubectl {ServiceName}] {Data}", serviceName, args.Data);
            };

            if (!process.Start())
            {
                _logger.LogError("Failed to start kubectl process for {ServiceName}", serviceName);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            _activeForwards.AddOrUpdate(key, process, (k, oldProcess) => process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Register cancellation to kill process
            using var ctr = cancellationToken.Register(() =>
            {
                _logger.LogInformation("Stopping port forward for {ServiceName}", serviceName);
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
                    _logger.LogWarning(ex, "Error stopping kubectl process");
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
                     _logger.LogWarning("kubectl exited with code {Code}. Restarting in 3s...", process.ExitCode);
                }
                else
                {
                     _logger.LogWarning("kubectl exited. Restarting in 3s...");
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
}
