using CommunityToolkit.Mvvm.ComponentModel;
using k8s.Models;
using System.Linq;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for a single Kubernetes deployment, exposing observable properties
/// for name, namespace, replica counts, and the first-container image tag.
/// </summary>
public partial class DeploymentViewModel : ObservableObject
{
    /// <summary>
    /// The backing deployment object.
    /// </summary>
    private V1Deployment _deployment;

    /// <summary>
    /// The name of the deployment.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// The namespace of the deployment.
    /// </summary>
    [ObservableProperty]
    private string _namespace = string.Empty;

    /// <summary>
    /// The desired (spec) replica count.
    /// </summary>
    [ObservableProperty]
    private int _desiredReplicas;

    /// <summary>
    /// The number of ready replicas reported by status.
    /// </summary>
    [ObservableProperty]
    private int _readyReplicas;

    /// <summary>
    /// The number of available replicas reported by status.
    /// </summary>
    [ObservableProperty]
    private int _availableReplicas;

    /// <summary>
    /// The image tag of the first container in the pod template spec.
    /// </summary>
    [ObservableProperty]
    private string _imageTag = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentViewModel"/> class.
    /// </summary>
    /// <param name="deployment">The Kubernetes deployment object to wrap.</param>
    public DeploymentViewModel(V1Deployment deployment)
    {
        _deployment = deployment;
        Update(deployment);
    }

    /// <summary>
    /// Updates all observable properties from a new <see cref="V1Deployment"/> object.
    /// </summary>
    /// <param name="deployment">The updated Kubernetes deployment object.</param>
    public void Update(V1Deployment deployment)
    {
        _deployment = deployment;
        Name = deployment.Metadata?.Name ?? string.Empty;
        Namespace = deployment.Metadata?.NamespaceProperty ?? string.Empty;
        DesiredReplicas = deployment.Spec?.Replicas ?? 0;
        ReadyReplicas = deployment.Status?.ReadyReplicas ?? 0;
        AvailableReplicas = deployment.Status?.AvailableReplicas ?? 0;
        ImageTag = deployment.Spec?.Template?.Spec?.Containers?.FirstOrDefault()?.Image ?? string.Empty;
    }
}
