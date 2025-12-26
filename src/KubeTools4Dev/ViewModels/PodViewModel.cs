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

        // Check for Terminating state (DeletionTimestamp is set)
        if (pod.Metadata.DeletionTimestamp.HasValue)
        {
            Status = "Terminating";
        }
        else
        {
            Status = pod.Status.Phase;
        }

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
        StatusColor = Status switch
        {
            "Running" => Brushes.SpringGreen,
            "Succeeded" => Brushes.LightBlue,
            "Pending" => Brushes.Orange,
            "Failed" => Brushes.Red,
            "Terminating" => Brushes.DarkOrange, // Distinct color for terminating
            _ => Brushes.Gray
        };
    }

    /// <summary>
    /// Formats the age.
    /// </summary>
    /// <param name="age">The age.</param>
    /// <returns></returns>
    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1) return $"{(int)age.TotalDays}d{(int)age.Hours}h";
        if (age.TotalHours >= 1) return $"{(int)age.TotalHours}h{(int)age.Minutes}m";
        if (age.TotalMinutes >= 1) return $"{(int)age.TotalMinutes}m";
        return $"{(int)age.TotalSeconds}s";
    }
}
