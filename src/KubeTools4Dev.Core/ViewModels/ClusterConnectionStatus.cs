namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents the connection lifecycle state of a cluster node.
/// </summary>
public enum ClusterConnectionStatus
{
    /// <summary>Not yet connected (default).</summary>
    Disconnected,

    /// <summary>Connection attempt in progress.</summary>
    Connecting,

    /// <summary>Successfully connected and ready.</summary>
    Connected,

    /// <summary>Connection failed; see the error message for details.</summary>
    Error
}
