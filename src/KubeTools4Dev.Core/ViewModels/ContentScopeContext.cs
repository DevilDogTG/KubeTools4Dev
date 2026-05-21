namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Identifies which cluster + namespace + resource type the content panel should display.
/// </summary>
/// <param name="ClusterId">The <see cref="Models.ClusterEntry.Id"/> as a string.</param>
/// <param name="Namespace">Namespace name, or empty string for all namespaces.</param>
/// <param name="Kind">Which resource type to show.</param>
public record ContentScopeContext(string ClusterId, string Namespace, ResourceKind Kind);
