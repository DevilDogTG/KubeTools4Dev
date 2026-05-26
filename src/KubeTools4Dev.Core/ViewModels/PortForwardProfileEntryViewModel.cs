using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Models;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// View model that wraps a <see cref="PortForwardProfileEntry"/> for display and editing.
/// </summary>
public partial class PortForwardProfileEntryViewModel : ObservableObject
{
    private readonly PortForwardProfileEntry _entry;
    private readonly Action<PortForwardProfileEntryViewModel> _removeCallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortForwardProfileEntryViewModel"/> class.
    /// </summary>
    /// <param name="entry">The underlying model entry.</param>
    /// <param name="removeCallback">
    /// Callback invoked when the user removes this entry from the profile.
    /// </param>
    public PortForwardProfileEntryViewModel(
        PortForwardProfileEntry entry,
        Action<PortForwardProfileEntryViewModel> removeCallback)
    {
        _entry = entry;
        _removeCallback = removeCallback;
    }

    /// <summary>
    /// Gets the Kubernetes namespace of the target service.
    /// </summary>
    public string Namespace => _entry.Namespace;

    /// <summary>
    /// Gets the name of the target service.
    /// </summary>
    public string ServiceName => _entry.ServiceName;

    /// <summary>
    /// Gets the target port string (may be an integer or a named port).
    /// </summary>
    public string TargetPort => _entry.TargetPort;

    /// <summary>
    /// Gets or sets the local port bound on the developer's machine.
    /// </summary>
    public int LocalPort
    {
        get => _entry.LocalPort;
        set
        {
            if (_entry.LocalPort != value)
            {
                _entry.LocalPort = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the underlying <see cref="PortForwardProfileEntry"/> model.
    /// </summary>
    public PortForwardProfileEntry Model => _entry;

    /// <summary>
    /// Removes this entry from its parent profile.
    /// </summary>
    [RelayCommand]
    private void Remove() => _removeCallback(this);
}
