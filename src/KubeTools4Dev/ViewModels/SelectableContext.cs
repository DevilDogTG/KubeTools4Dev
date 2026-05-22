using CommunityToolkit.Mvvm.ComponentModel;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Represents a selectable kubeconfig context entry in the Add Cluster dialog list.
/// </summary>
public partial class SelectableContext : ObservableObject
{
    /// <summary>Initializes a new selectable context with the given name.</summary>
    public SelectableContext(string name)
    {
        Name = name;
    }

    /// <summary>Gets the context name.</summary>
    public string Name { get; }

    /// <summary>Gets or sets a value indicating whether this context is selected for import.</summary>
    [ObservableProperty]
    private bool _isSelected;
}
