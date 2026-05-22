using KubeTools4Dev.Core.ViewModels;

namespace KubeTools4Dev.Core.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="NamespaceNodeViewModel"/>.
/// </summary>
public class NamespaceNodeViewModelTests
{
    [Fact]
    public void Constructor_ExposesCorrectProperties()
    {
        var sut = new NamespaceNodeViewModel("default", "cluster-1");

        Assert.Equal("default", sut.Name);
        Assert.Equal("cluster-1", sut.ClusterId);
    }

    [Fact]
    public void ResourceTypes_ContainsExactlyThreeItems()
    {
        var sut = new NamespaceNodeViewModel("default", "cluster-1");

        Assert.Equal(3, sut.ResourceTypes.Count);
    }

    [Fact]
    public void ResourceTypes_ContainsPods_Services_Deployments_InOrder()
    {
        var sut = new NamespaceNodeViewModel("default", "cluster-1");

        Assert.Equal(ResourceKind.Pods, sut.ResourceTypes[0].Kind);
        Assert.Equal(ResourceKind.Services, sut.ResourceTypes[1].Kind);
        Assert.Equal(ResourceKind.Deployments, sut.ResourceTypes[2].Kind);
    }

    [Fact]
    public void ResourceTypes_DisplayNames_AreSet()
    {
        var sut = new NamespaceNodeViewModel("default", "cluster-1");

        Assert.All(sut.ResourceTypes, rt => Assert.False(string.IsNullOrEmpty(rt.DisplayName)));
    }

    // ── SelectCommand wiring ──────────────────────────────────────────────────

    [Fact]
    public void SelectCommand_IsNull_WhenNoCallbackProvided()
    {
        var sut = new NamespaceNodeViewModel("default", "cluster-1");

        Assert.All(sut.ResourceTypes, rt => Assert.Null(rt.SelectCommand));
    }

    [Fact]
    public void SelectCommand_InvokesCallback_WithCorrectContext_ForPods()
    {
        ContentScopeContext? captured = null;
        var sut = new NamespaceNodeViewModel("default", "cluster-1", ctx => captured = ctx);

        sut.ResourceTypes[0].SelectCommand!.Execute(null); // Pods

        Assert.NotNull(captured);
        Assert.Equal("cluster-1", captured.ClusterId);
        Assert.Equal("default", captured.Namespace);
        Assert.Equal(ResourceKind.Pods, captured.Kind);
    }

    [Fact]
    public void SelectCommand_InvokesCallback_WithCorrectContext_ForDeployments()
    {
        ContentScopeContext? captured = null;
        var sut = new NamespaceNodeViewModel("staging", "cluster-2", ctx => captured = ctx);

        sut.ResourceTypes[2].SelectCommand!.Execute(null); // Deployments

        Assert.NotNull(captured);
        Assert.Equal("cluster-2", captured.ClusterId);
        Assert.Equal("staging", captured.Namespace);
        Assert.Equal(ResourceKind.Deployments, captured.Kind);
    }
}
