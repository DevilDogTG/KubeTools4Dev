using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Test subclass that runs UI-dispatched work inline so we don't need an Avalonia dispatcher.
/// </summary>
file sealed class TestServiceListViewModel(
    ISettingsService settings,
    IClusterConnectionManager manager,
    ILogger<ServiceListViewModel> logger,
    ILoggerFactory loggerFactory)
    : ServiceListViewModel(settings, manager, logger, loggerFactory)
{
    protected override void DispatchToUI(Action action) => action();
}

/// <summary>
/// Tests for <see cref="ServiceListViewModel"/>'s integration with
/// <see cref="IProfilePortForwardSupervisor"/>.
/// </summary>
public class ServiceListViewModelSupervisorTests
{
    private static readonly Guid ClusterId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly IClusterConnectionManager _manager = Substitute.For<IClusterConnectionManager>();
    private readonly ILogger<ServiceListViewModel> _logger = Substitute.For<ILogger<ServiceListViewModel>>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly RecordingSupervisor _supervisor = new();

    private ClusterEntry _clusterEntry = new() { Id = ClusterId, DisplayName = "test-cluster" };

    private async Task<ServiceListViewModel> MakeVmAsync(params PortForwardProfile[] preExistingProfiles)
    {
        _settings.Services.Returns(new ServicesSettings());
        _settings.Clusters.Returns(new ClustersSettings { Clusters = [_clusterEntry] });
        _settings.Namespaces.Returns(new NamespacesSettings());
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _manager.GetProfileSupervisor(ClusterId.ToString()).Returns(_supervisor);
        _clusterEntry.PortForwardProfiles = preExistingProfiles.ToList();

        var vm = new TestServiceListViewModel(_settings, _manager, _logger, _loggerFactory);
        var pfService = Substitute.For<IPortForwardService>();
        var kubeService = Substitute.For<IKubernetesService>();
        kubeService.IsConnected.Returns(false);
        await vm.UpdateScopeAsync(kubeService, pfService, "default", ClusterId.ToString());
        return vm;
    }

    private static PortForwardProfile MakeProfile(Guid id, params PortForwardProfileEntry[] entries)
        => new() { Id = id, Name = "Test", Entries = entries.ToList() };

    [Fact]
    public async Task ToggleProfileCommand_WhenNotRunning_StartsProfile()
    {
        var pid = Guid.NewGuid();
        var profile = MakeProfile(pid,
            new PortForwardProfileEntry { Namespace = "ns", ServiceName = "svc", TargetPort = "80", LocalPort = 8080 });
        var vm = await MakeVmAsync(profile);

        Assert.False(vm.IsProfileRunning);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleProfileCommand).ExecuteAsync(null);

        Assert.Equal(pid, _supervisor.StartedProfileId);
        Assert.Single(_supervisor.StartedEntries!);
        Assert.True(vm.IsProfileRunning);
    }

    [Fact]
    public async Task ToggleProfileCommand_WhenRunning_StopsProfile()
    {
        var pid = Guid.NewGuid();
        var profile = MakeProfile(pid,
            new PortForwardProfileEntry { Namespace = "ns", ServiceName = "svc", TargetPort = "80", LocalPort = 8080 });
        var vm = await MakeVmAsync(profile);

        vm.IsProfileRunning = true;
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleProfileCommand).ExecuteAsync(null);

        Assert.Equal(pid, _supervisor.StoppedProfileId);
        Assert.False(vm.IsProfileRunning);
    }

    [Fact]
    public async Task ProfileToggleLabel_ReflectsRunningState()
    {
        var vm = await MakeVmAsync();
        Assert.Equal("▶ Forward", vm.ProfileToggleLabel);
        vm.IsProfileRunning = true;
        Assert.Equal("■ Stop", vm.ProfileToggleLabel);
    }

    [Fact]
    public async Task ProfileToggleLabel_BecomesResume_WhenEntryUnsupervised()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        // Simulate the row going Forwarding then Unsupervised.
        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc-x", "80", 8080,
            SupervisedForwardState.Forwarding, 1, 10, null));
        Assert.True(vm.IsProfileRunning);
        Assert.Equal("■ Stop", vm.ProfileToggleLabel);

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc-x", "80", 8080,
            SupervisedForwardState.Unsupervised, 1, 10, null));

        Assert.True(vm.HasUnsupervisedEntries);
        Assert.Equal("▶ Resume", vm.ProfileToggleLabel);
    }

    [Fact]
    public async Task ToggleProfileCommand_WhenRunningWithUnsupervised_ResumesProfile()
    {
        var pid = Guid.NewGuid();
        var profile = MakeProfile(pid,
            new PortForwardProfileEntry { Namespace = "ns", ServiceName = "svc", TargetPort = "80", LocalPort = 8080 });
        var vm = await MakeVmAsync(profile);

        // Start the profile.
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleProfileCommand).ExecuteAsync(null);
        Assert.Equal(1, _supervisor.StartCallCount);

        // Drive an Unsupervised snapshot for an entry in this profile.
        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc", "80", 8080,
            SupervisedForwardState.Unsupervised, 1, 10, null));
        Assert.Equal("▶ Resume", vm.ProfileToggleLabel);

        // Toggle again: should call StartProfileAsync (resume), not StopProfileAsync.
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.ToggleProfileCommand).ExecuteAsync(null);
        Assert.Null(_supervisor.StoppedProfileId);
        Assert.Equal(2, _supervisor.StartCallCount);
    }

    [Fact]
    public async Task EntryStateChanged_Forwarding_ClearsHasUnsupervisedEntries()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc", "80", 8080,
            SupervisedForwardState.Unsupervised, 1, 10, null));
        Assert.True(vm.HasUnsupervisedEntries);

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc", "80", 8080,
            SupervisedForwardState.Forwarding, 1, 10, null));

        Assert.False(vm.HasUnsupervisedEntries);
    }

    [Fact]
    public async Task OnEntryStateChanged_Forwarding_SetsIsProfileRunning()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc", "80", 8080,
            SupervisedForwardState.Forwarding, 1, 10, null));

        Assert.True(vm.IsProfileRunning);
    }

    [Fact]
    public async Task OnEntryStateChanged_Failed_DoesNotSetBanner_OnlyOnFailureEvent()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc", "80", 8080,
            SupervisedForwardState.Failed, 10, 10, "boom"));

        // EntryStateChanged Failed alone should not set the banner — only ProfileStoppedDueToFailure does.
        Assert.Null(vm.BannerMessage);
    }

    [Fact]
    public async Task OnProfileStoppedDueToFailure_SetsErrorBanner()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        _supervisor.RaiseProfileStoppedDueToFailure(new ProfileFailureReason(
            pid, "ns", "svc-broken", 10, "Port 8080 already in use"));

        Assert.NotNull(vm.BannerMessage);
        Assert.Contains("svc-broken", vm.BannerMessage);
        Assert.Contains("Port 8080 already in use", vm.BannerMessage);
        Assert.Equal(BannerSeverity.Error, vm.BannerSeverity);
        Assert.False(vm.IsProfileRunning);
    }

    [Fact]
    public async Task OnEntryStateChanged_Unsupervised_SetsInfoBanner()
    {
        var vm = await MakeVmAsync();
        var pid = Guid.NewGuid();

        _supervisor.RaiseEntryStateChanged(new SupervisedForwardSnapshot(
            pid, "ns", "svc-x", "80", 8080,
            SupervisedForwardState.Unsupervised, 1, 10, null));

        Assert.NotNull(vm.BannerMessage);
        Assert.Contains("svc-x", vm.BannerMessage);
        Assert.Equal(BannerSeverity.Info, vm.BannerSeverity);
    }

    [Fact]
    public async Task DismissBannerCommand_ClearsBanner()
    {
        var vm = await MakeVmAsync();
        vm.BannerMessage = "Something happened";
        Assert.True(vm.DismissBannerCommand.CanExecute(null));

        vm.DismissBannerCommand.Execute(null);

        Assert.Null(vm.BannerMessage);
        Assert.False(vm.DismissBannerCommand.CanExecute(null));
    }

    [Fact]
    public async Task UpdateScope_UnsubscribesFromOldSupervisor()
    {
        var vm = await MakeVmAsync();

        // Switch to a different cluster — old supervisor should be detached.
        var otherId = Guid.NewGuid();
        var otherEntry = new ClusterEntry { Id = otherId, DisplayName = "other" };
        _settings.Clusters.Returns(new ClustersSettings { Clusters = [_clusterEntry, otherEntry] });
        var newSupervisor = new RecordingSupervisor();
        _manager.GetProfileSupervisor(otherId.ToString()).Returns(newSupervisor);

        await vm.UpdateScopeAsync(Substitute.For<IKubernetesService>(),
            Substitute.For<IPortForwardService>(), "default", otherId.ToString());

        // Raising on the old supervisor should not affect the VM anymore.
        var preBanner = vm.BannerMessage;
        _supervisor.RaiseProfileStoppedDueToFailure(new ProfileFailureReason(
            Guid.NewGuid(), "ns", "should-be-ignored", 10, "x"));

        Assert.Equal(preBanner, vm.BannerMessage);
    }

    [Fact]
    public async Task Cleanup_UnsubscribesFromSupervisor()
    {
        var vm = await MakeVmAsync();
        vm.Cleanup();

        var preBanner = vm.BannerMessage;
        _supervisor.RaiseProfileStoppedDueToFailure(new ProfileFailureReason(
            Guid.NewGuid(), "ns", "svc", 10, "x"));

        Assert.Equal(preBanner, vm.BannerMessage);
    }

    /// <summary>
    /// Lightweight in-memory <see cref="IProfilePortForwardSupervisor"/> that records calls
    /// and exposes event-raise helpers for tests.
    /// </summary>
    private sealed class RecordingSupervisor : IProfilePortForwardSupervisor
    {
        public Guid? StartedProfileId { get; set; }
        public IReadOnlyList<PortForwardProfileEntry>? StartedEntries { get; private set; }
        public Guid? StoppedProfileId { get; set; }
        public (string ns, string svc, string port)? UnsupervisedKey { get; private set; }
        public bool StoppedAll { get; private set; }
        public int StartCallCount { get; private set; }

        private readonly HashSet<Guid> _running = new();

        public event Action<SupervisedForwardSnapshot>? EntryStateChanged;
        public event Action<ProfileFailureReason>? ProfileStoppedDueToFailure;

        public void RaiseEntryStateChanged(SupervisedForwardSnapshot snapshot)
            => EntryStateChanged?.Invoke(snapshot);

        public void RaiseProfileStoppedDueToFailure(ProfileFailureReason reason)
            => ProfileStoppedDueToFailure?.Invoke(reason);

        public Task StartProfileAsync(Guid profileId, IReadOnlyList<PortForwardProfileEntry> entries)
        {
            StartedProfileId = profileId;
            StartedEntries = entries;
            StartCallCount++;
            _running.Add(profileId);
            return Task.CompletedTask;
        }

        public Task StopProfileAsync(Guid profileId)
        {
            StoppedProfileId = profileId;
            _running.Remove(profileId);
            return Task.CompletedTask;
        }

        public Task UnsuperviseAsync(string namespaceName, string serviceName, string targetPort)
        {
            UnsupervisedKey = (namespaceName, serviceName, targetPort);
            return Task.CompletedTask;
        }

        public bool IsSupervised(string namespaceName, string serviceName, string targetPort) => false;

        public bool IsProfileRunning(Guid profileId) => _running.Contains(profileId);

        public void StopAll()
        {
            StoppedAll = true;
            _running.Clear();
        }
    }
}
