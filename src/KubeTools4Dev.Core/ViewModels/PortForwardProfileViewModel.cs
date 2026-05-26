using CommunityToolkit.Mvvm.ComponentModel;
using KubeTools4Dev.Core.Models;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// View model that wraps a <see cref="PortForwardProfile"/> for display and editing.
/// </summary>
public partial class PortForwardProfileViewModel : ObservableObject
{
    private readonly PortForwardProfile _profile;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortForwardProfileViewModel"/> class.
    /// </summary>
    /// <param name="profile">The underlying model profile.</param>
    public PortForwardProfileViewModel(PortForwardProfile profile)
    {
        _profile = profile;
        Entries = new ObservableCollection<PortForwardProfileEntryViewModel>(
            profile.Entries.Select(e => new PortForwardProfileEntryViewModel(e, RemoveEntry)));
    }

    /// <summary>
    /// Gets the unique identifier of this profile.
    /// </summary>
    public Guid Id => _profile.Id;

    /// <summary>
    /// Gets or sets the human-readable name of this profile.
    /// </summary>
    public string Name
    {
        get => _profile.Name;
        set
        {
            if (_profile.Name != value)
            {
                _profile.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the observable collection of entry view models for this profile.
    /// </summary>
    public ObservableCollection<PortForwardProfileEntryViewModel> Entries { get; }

    /// <summary>
    /// Gets the underlying <see cref="PortForwardProfile"/> model (with up-to-date entries).
    /// </summary>
    public PortForwardProfile Model => _profile;

    /// <summary>
    /// Returns <see langword="true"/> when this profile contains an entry matching the given
    /// namespace, service name, and target port.
    /// </summary>
    /// <param name="namespaceName">The Kubernetes namespace.</param>
    /// <param name="serviceName">The service name.</param>
    /// <param name="targetPort">The target port string.</param>
    public bool Contains(string namespaceName, string serviceName, string targetPort) =>
        Entries.Any(e =>
            e.Namespace == namespaceName &&
            e.ServiceName == serviceName &&
            e.TargetPort == targetPort);

    /// <summary>
    /// Adds a new entry to this profile and synchronises the underlying model.
    /// </summary>
    /// <param name="entry">The entry model to add.</param>
    public void AddEntry(PortForwardProfileEntry entry)
    {
        _profile.Entries.Add(entry);
        Entries.Add(new PortForwardProfileEntryViewModel(entry, RemoveEntry));
    }

    /// <summary>
    /// Removes the entry represented by <paramref name="entryVm"/> from this profile and
    /// synchronises the underlying model.
    /// </summary>
    /// <param name="entryVm">The entry view model to remove.</param>
    public void RemoveEntry(PortForwardProfileEntryViewModel entryVm)
    {
        _profile.Entries.Remove(entryVm.Model);
        Entries.Remove(entryVm);
    }
}
