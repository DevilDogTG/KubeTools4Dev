using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using k8s.Models;
using System;
using System.Linq;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for a single pod.
/// </summary>
/// <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject" />
public partial class PodViewModel : ObservableObject
{
    /// <summary>
    /// The age
    /// </summary>
    [ObservableProperty]
    private string _age = string.Empty;

    /// <summary>
    /// The last restart
    /// </summary>
    [ObservableProperty]
    private string _lastRestart = string.Empty;

    /// <summary>
    /// The name
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// The namespace
    /// </summary>
    [ObservableProperty]
    private string _namespace = string.Empty;

    /// <summary>
    /// The pod
    /// </summary>
    private V1Pod _pod;
    /// <summary>
    /// The restarts
    /// </summary>
    [ObservableProperty]
    private int _restarts = 0;

    /// <summary>
    /// The status
    /// </summary>
    [ObservableProperty]
    private string _status = string.Empty;
    /// <summary>
    /// The status color
    /// </summary>
    [ObservableProperty]
    private IBrush _statusColor = Brushes.Gray;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodViewModel"/> class.
    /// </summary>
    /// <param name="pod">The pod.</param>
    public PodViewModel(V1Pod pod)
    {
        _pod = pod;
        Update(pod);
    }

    /// <summary>
    /// Refreshes the age.
    /// </summary>
    public void RefreshAge()
    {
        if (_pod.Metadata.CreationTimestamp.HasValue)
        {
            Age = FormatAge(DateTime.UtcNow - _pod.Metadata.CreationTimestamp.Value);
        }
    }

    /// <summary>
    /// Updates the specified pod.
    /// </summary>
    /// <param name="pod">The pod.</param>
    public void Update(V1Pod pod)
    {
        _pod = pod;
        Name = pod.Metadata.Name;
        Namespace = pod.Metadata.NamespaceProperty;

        Status = GetPodStatus(pod);

        // Age
        Age = pod.Metadata.CreationTimestamp.HasValue
            ? FormatAge(DateTime.UtcNow - pod.Metadata.CreationTimestamp.Value)
            : "N/A";

        // Restarts
        if (pod.Status.ContainerStatuses != null)
        {
            Restarts = pod.Status.ContainerStatuses.Sum(c => c.RestartCount);
        }
        else
        {
            Restarts = 0;
        }

        // Last Restart
        LastRestart = "-";
        if (pod.Status.ContainerStatuses != null)
        {
            var lastTerminated = pod.Status.ContainerStatuses
                .Select(c => c.LastState?.Terminated?.FinishedAt)
                .Where(t => t.HasValue)
                .OrderByDescending(t => t)
                .FirstOrDefault();

            if (lastTerminated.HasValue)
            {
                LastRestart = FormatAge(DateTime.UtcNow - lastTerminated.Value);
            }
        }

        // Color
        StatusColor = GetStatusColor(Status);
    }

    /// <summary>
    /// Formats the age.
    /// </summary>
    /// <param name="age">The age.</param>
    /// <returns></returns>
    private static string FormatAge(TimeSpan age)
    {
        // Ensure non-negative age: set negative values to zero (clock skew)
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;

        if (age.TotalDays >= 1) return $"{(int)age.TotalDays}d{(int)age.Hours}h";
        if (age.TotalHours >= 1) return $"{(int)age.TotalHours}h{(int)age.Minutes}m";
        if (age.TotalMinutes >= 1) return $"{(int)age.TotalMinutes}m";
        return $"{(int)age.TotalSeconds}s";
    }

    private IBrush GetStatusColor(string status)
    {
        if (status is "Running" or "Completed" or "Succeeded")
        {
            return status == "Running" ? Brushes.SpringGreen : Brushes.LightBlue;
        }

        if (status is "Terminating")
        {
            return Brushes.DarkOrange;
        }

        if (status is "Pending" or "ContainerCreating" or "PodInitializing")
        {
            return Brushes.Orange;
        }

        if (status.Contains("BackOff") || status.Contains("Err") || status.Contains("Crash") || status.Contains("Failed") || status.Contains("OOMKilled") || status.Contains("Invalid"))
        {
            return Brushes.Red;
        }

        if (status.StartsWith("Init:"))
        {
            return Brushes.Orange;
        }

        return Brushes.Gray;
    }

    private string GetPodStatus(V1Pod pod)
    {
        var reason = pod.Status?.Phase ?? "Unknown";

        if (pod.Status?.Reason != null)
        {
            reason = pod.Status.Reason;
        }

        var initializing = false;

        if (pod.Status?.InitContainerStatuses != null)
        {
            for (var i = 0; i < pod.Status.InitContainerStatuses.Count; i++)
            {
                var container = pod.Status.InitContainerStatuses[i];

                if (container.State?.Terminated != null && container.State.Terminated.ExitCode == 0)
                {
                    continue;
                }

                if (container.State?.Terminated != null)
                {
                    if (string.IsNullOrEmpty(container.State.Terminated.Reason))
                    {
                        if (container.State.Terminated.Signal != 0)
                        {
                            reason = $"Init:Signal:{container.State.Terminated.Signal}";
                        }
                        else
                        {
                            reason = $"Init:ExitCode:{container.State.Terminated.ExitCode}";
                        }
                    }
                    else
                    {
                        reason = $"Init:{container.State.Terminated.Reason}";
                    }

                    initializing = true;
                }
                else if (container.State?.Waiting != null && !string.IsNullOrEmpty(container.State.Waiting.Reason) && container.State.Waiting.Reason != "PodInitializing")
                {
                    reason = $"Init:{container.State.Waiting.Reason}";
                    initializing = true;
                }
                else
                {
                    reason = $"Init:{i}/{pod.Spec?.InitContainers?.Count ?? 0}";
                    initializing = true;
                }

                break;
            }
        }

        if (!initializing)
        {
            var hasRunning = false;

            if (pod.Status?.ContainerStatuses != null)
            {
                for (var i = pod.Status.ContainerStatuses.Count - 1; i >= 0; i--)
                {
                    var container = pod.Status.ContainerStatuses[i];

                    if (container.State?.Waiting != null && !string.IsNullOrEmpty(container.State.Waiting.Reason))
                    {
                        reason = container.State.Waiting.Reason;
                    }
                    else if (container.State?.Terminated != null && !string.IsNullOrEmpty(container.State.Terminated.Reason))
                    {
                        reason = container.State.Terminated.Reason;
                    }
                    else if (container.State?.Terminated != null && string.IsNullOrEmpty(container.State.Terminated.Reason))
                    {
                        if (container.State.Terminated.Signal != 0)
                        {
                            reason = $"Signal:{container.State.Terminated.Signal}";
                        }
                        else
                        {
                            reason = $"ExitCode:{container.State.Terminated.ExitCode}";
                        }
                    }
                    else if (container.Ready && container.State?.Running != null)
                    {
                        hasRunning = true;
                    }
                }
            }

            if (reason == "Completed" && hasRunning)
            {
                var hasReadyCondition = pod.Status?.Conditions?.Any(c => c.Type == "Ready" && c.Status == "True") == true;

                if (hasReadyCondition)
                {
                    reason = "Running";
                }
                else
                {
                    reason = "NotReady";
                }
            }
        }

        if (pod.Metadata?.DeletionTimestamp != null && pod.Status?.Reason == "NodeLost")
        {
            reason = "Unknown";
        }
        else if (pod.Metadata?.DeletionTimestamp != null)
        {
            reason = "Terminating";
        }

        return reason;
    }
}
