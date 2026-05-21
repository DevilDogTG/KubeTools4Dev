using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KubeTools4Dev.ViewModels;
using System.Collections.Generic;

namespace KubeTools4Dev.Views;

/// <summary>
/// Modal dialog for adding a Kubernetes cluster from a kubeconfig file.
/// </summary>
public partial class AddClusterDialog : Window
{
    /// <summary>Initializes a new instance of <see cref="AddClusterDialog"/>.</summary>
    public AddClusterDialog()
    {
        InitializeComponent();

        var browseButton = this.Find<Button>("BrowseButton")!;
        browseButton.Click += OnBrowseClicked;
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddClusterDialogViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select kubeconfig file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Kubeconfig files") { Patterns = ["config", "*.yaml", "*.yml", "*.conf"] },
                new("All files") { Patterns = ["*"] }
            }
        });

        if (files.Count == 0) return;

        vm.KubeConfigPath = files[0].Path.LocalPath;
        await vm.LoadContextsCommand.ExecuteAsync(null);
    }
}
