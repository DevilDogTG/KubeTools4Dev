using k8s.Models;
using KubeTools4Dev.ViewModels;
using System.Collections.Generic;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DeploymentViewModel"/> property mapping and update logic.
/// </summary>
public class DeploymentViewModelTests
{
    private static V1Deployment MakeDeployment(
        string name,
        string ns,
        int desiredReplicas,
        int readyReplicas,
        int availableReplicas,
        string image)
    {
        return new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns },
            Spec = new V1DeploymentSpec
            {
                Replicas = desiredReplicas,
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        Containers = [new V1Container { Name = "app", Image = image }]
                    }
                }
            },
            Status = new V1DeploymentStatus
            {
                ReadyReplicas = readyReplicas,
                AvailableReplicas = availableReplicas
            }
        };
    }

    [Fact]
    public void Constructor_MapsAllPropertiesFromDeployment()
    {
        var deployment = MakeDeployment("my-app", "production", 3, 2, 2, "nginx:1.25");

        var vm = new DeploymentViewModel(deployment);

        Assert.Equal("my-app", vm.Name);
        Assert.Equal("production", vm.Namespace);
        Assert.Equal(3, vm.DesiredReplicas);
        Assert.Equal(2, vm.ReadyReplicas);
        Assert.Equal(2, vm.AvailableReplicas);
        Assert.Equal("nginx:1.25", vm.ImageTag);
    }

    [Fact]
    public void Update_RemapsAllPropertiesFromNewDeployment()
    {
        var initial = MakeDeployment("my-app", "default", 1, 1, 1, "nginx:1.0");
        var vm = new DeploymentViewModel(initial);

        var updated = MakeDeployment("my-app", "default", 5, 4, 4, "nginx:2.0");
        vm.Update(updated);

        Assert.Equal(5, vm.DesiredReplicas);
        Assert.Equal(4, vm.ReadyReplicas);
        Assert.Equal(4, vm.AvailableReplicas);
        Assert.Equal("nginx:2.0", vm.ImageTag);
    }

    [Fact]
    public void ImageTag_ReadsFirstContainerImage()
    {
        var deployment = MakeDeployment("svc", "ns", 1, 1, 1, "myrepo/myimage:v3");

        var vm = new DeploymentViewModel(deployment);

        Assert.Equal("myrepo/myimage:v3", vm.ImageTag);
    }

    [Fact]
    public void ImageTag_IsEmptyString_WhenContainersIsNull()
    {
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = "x", NamespaceProperty = "y" },
            Spec = new V1DeploymentSpec
            {
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec { Containers = null }
                }
            },
            Status = new V1DeploymentStatus()
        };

        var vm = new DeploymentViewModel(deployment);

        Assert.Equal(string.Empty, vm.ImageTag);
    }

    [Fact]
    public void DesiredReplicas_IsZero_WhenSpecReplicasIsNull()
    {
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = "x", NamespaceProperty = "y" },
            Spec = new V1DeploymentSpec
            {
                Replicas = null,
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        Containers = [new V1Container { Name = "app", Image = "img:1" }]
                    }
                }
            },
            Status = new V1DeploymentStatus()
        };

        var vm = new DeploymentViewModel(deployment);

        Assert.Equal(0, vm.DesiredReplicas);
    }
}
