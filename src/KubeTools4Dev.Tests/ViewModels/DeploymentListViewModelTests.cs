using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DeploymentListViewModel"/> command logic and
/// <see cref="EditDeploymentDialogViewModel"/> validation.
/// </summary>
public class DeploymentListViewModelTests
{
    private readonly IKubernetesService _kubeService = Substitute.For<IKubernetesService>();
    private readonly ILogger<DeploymentListViewModel> _logger = Substitute.For<ILogger<DeploymentListViewModel>>();

    private TestableDeploymentListViewModel MakeVm() =>
        new(_kubeService, _logger);

    private static DeploymentViewModel MakeDeploymentVm(
        string name = "my-app",
        string ns = "default",
        int replicas = 2,
        string image = "nginx:1.0")
    {
        var d = new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns },
            Spec = new V1DeploymentSpec
            {
                Replicas = replicas,
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        Containers = [new V1Container { Name = "app", Image = image }]
                    }
                }
            },
            Status = new V1DeploymentStatus { ReadyReplicas = replicas, AvailableReplicas = replicas }
        };
        return new DeploymentViewModel(d);
    }

    // ── RolloutRestart ────────────────────────────────────────────────────────

    [Fact]
    public async Task RolloutRestartCommand_CallsRestartDeploymentAsync_WithCorrectArgs()
    {
        var vm = MakeVm();
        var dep = MakeDeploymentVm("api", "prod");

        await vm.RolloutRestartCommand.ExecuteAsync(dep);

        await _kubeService.Received(1).RestartDeploymentAsync("prod", "api");
    }

    [Fact]
    public async Task RolloutRestartCommand_SetsErrorMessage_WhenServiceThrows()
    {
        _kubeService
            .RestartDeploymentAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        var vm = MakeVm();
        var dep = MakeDeploymentVm();

        await vm.RolloutRestartCommand.ExecuteAsync(dep);

        Assert.Contains("connection refused", vm.ErrorMessage);
    }

    [Fact]
    public async Task RolloutRestartCommand_ClearsErrorMessage_BeforeEachInvocation()
    {
        var vm = MakeVm();
        vm.ErrorMessage = "stale error";
        var dep = MakeDeploymentVm();

        await vm.RolloutRestartCommand.ExecuteAsync(dep);

        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    // ── EditDeployment ────────────────────────────────────────────────────────

    [Fact]
    public async Task EditDeploymentCommand_CallsPatchDeploymentAsync_WhenConfirmed()
    {
        var vm = MakeVm();
        vm.TestDialogReplicas = 5;
        vm.TestDialogImage = "nginx:2.0";
        vm.TestDialogShouldConfirm = true;

        var dep = MakeDeploymentVm("svc", "staging", 2, "nginx:1.0");

        await vm.EditDeploymentCommand.ExecuteAsync(dep);

        await _kubeService.Received(1).PatchDeploymentAsync("staging", "svc", 5, "nginx:2.0");
    }

    [Fact]
    public async Task EditDeploymentCommand_DoesNotCallPatch_WhenCancelled()
    {
        var vm = MakeVm();
        vm.TestDialogShouldConfirm = false;

        var dep = MakeDeploymentVm();

        await vm.EditDeploymentCommand.ExecuteAsync(dep);

        await _kubeService.DidNotReceive().PatchDeploymentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
    }

    // ── EditDeploymentDialogViewModel ─────────────────────────────────────────

    [Fact]
    public void EditDialog_ConfirmCommand_RejectsNegativeReplicas()
    {
        var vm = new EditDeploymentDialogViewModel("app", -1, "nginx:1.0");

        vm.ConfirmCommand.Execute(null);

        Assert.False(vm.IsConfirmed);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void EditDialog_ConfirmCommand_RejectsEmptyImageTag()
    {
        var vm = new EditDeploymentDialogViewModel("app", 2, "");

        vm.ConfirmCommand.Execute(null);

        Assert.False(vm.IsConfirmed);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void EditDialog_ConfirmCommand_SetsIsConfirmed_WhenInputIsValid()
    {
        var vm = new EditDeploymentDialogViewModel("app", 3, "nginx:2.0");

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsConfirmed);
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    [Fact]
    public void EditDialog_CancelCommand_LeavesIsConfirmedFalse()
    {
        var vm = new EditDeploymentDialogViewModel("app", 1, "nginx:1.0");

        vm.CancelCommand.Execute(null);

        Assert.False(vm.IsConfirmed);
    }

    // ── Test double ───────────────────────────────────────────────────────────

    private sealed class TestableDeploymentListViewModel(
        IKubernetesService kubernetesService,
        ILogger<DeploymentListViewModel> logger)
        : DeploymentListViewModel(kubernetesService, logger)
    {
        public bool TestDialogShouldConfirm { get; set; }
        public int TestDialogReplicas { get; set; }
        public string TestDialogImage { get; set; } = string.Empty;

        protected override Task ShowEditDialogAsync(EditDeploymentDialogViewModel vm)
        {
            vm.Replicas = TestDialogReplicas;
            vm.ImageTag = TestDialogImage;
            if (TestDialogShouldConfirm)
                vm.ConfirmCommand.Execute(null);
            return Task.CompletedTask;
        }
    }
}
