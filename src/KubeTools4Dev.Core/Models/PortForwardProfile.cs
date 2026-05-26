namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Represents a named, reusable port-forward profile that groups one or more service
/// port-forward entries for a cluster.
/// </summary>
public class PortForwardProfile
{
    /// <summary>
    /// Gets or sets the unique identifier of this profile.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the human-readable name displayed in the profile selector.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service entries that belong to this profile.
    /// </summary>
    public List<PortForwardProfileEntry> Entries { get; set; } = [];
}
