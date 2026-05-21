using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the Add Cluster dialog.
/// Handles browsing to a kubeconfig file, enumerating its contexts, and adding selected ones.
/// </summary>
public partial class AddClusterDialogViewModel : ViewModelBase
{
    private readonly IClusterConnectionManager _manager;
    private readonly ClusterTreeViewModel _tree;

    /// <summary>Initializes a new instance of <see cref="AddClusterDialogViewModel"/>.</summary>
    public AddClusterDialogViewModel(
        IClusterConnectionManager manager,
        ClusterTreeViewModel tree)
    {
        _manager = manager;
        _tree = tree;
    }

    /// <summary>Gets or sets the path to the kubeconfig file to import.</summary>
    [ObservableProperty]
    private string _kubeConfigPath = string.Empty;

    /// <summary>Gets or sets a value indicating whether contexts are being loaded.</summary>
    [ObservableProperty]
    private bool _isLoadingContexts;

    /// <summary>Gets the list of contexts discovered in the selected kubeconfig file.</summary>
    public ObservableCollection<SelectableContext> AvailableContexts { get; } = [];

    /// <summary>Gets or sets the error message shown when enumeration fails.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Raised when the user confirms the selection and the dialog should close.</summary>
    public event Action? ConfirmRequested;

    /// <summary>Raised when the user cancels and the dialog should close.</summary>
    public event Action? CancelRequested;

    /// <summary>Loads contexts from the currently entered kubeconfig path.</summary>
    [RelayCommand]
    private async Task LoadContextsAsync()
    {
        if (string.IsNullOrWhiteSpace(KubeConfigPath)) return;
        if (!File.Exists(KubeConfigPath))
        {
            ErrorMessage = "File not found.";
            return;
        }

        IsLoadingContexts = true;
        ErrorMessage = string.Empty;
        AvailableContexts.Clear();

        try
        {
            var contexts = await _manager.EnumerateContextsAsync(KubeConfigPath);
            foreach (var ctx in contexts)
            {
                AvailableContexts.Add(new SelectableContext(ctx));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingContexts = false;
        }
    }

    /// <summary>Adds the selected contexts to the cluster registry and refreshes the tree.</summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var selected = AvailableContexts.Where(c => c.IsSelected).Select(c => c.Name);
        await _tree.AddSourceAsync(KubeConfigPath, selected);
        ConfirmRequested?.Invoke();
    }

    /// <summary>Cancels the dialog without making changes.</summary>
    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke();
}

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
