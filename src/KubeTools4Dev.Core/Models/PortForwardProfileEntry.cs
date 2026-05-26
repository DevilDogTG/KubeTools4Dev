namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Represents a single service entry within a port-forward profile.
/// </summary>
public class PortForwardProfileEntry
{
    /// <summary>
    /// Gets or sets the Kubernetes namespace that contains the service.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the Kubernetes service.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target port on the service (stored as a string to support both integer
    /// and named ports).
    /// </summary>
    public string TargetPort { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local port that will be bound on the developer's machine.
    /// </summary>
    public int LocalPort { get; set; }
}
