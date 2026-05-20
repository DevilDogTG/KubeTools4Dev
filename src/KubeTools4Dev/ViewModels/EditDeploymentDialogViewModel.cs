using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the Edit Deployment dialog, exposing fields for replica count and image tag
/// with validation on confirm.
/// </summary>
public partial class EditDeploymentDialogViewModel : ObservableObject
{
    /// <summary>
    /// The name of the deployment being edited (display only).
    /// </summary>
    [ObservableProperty]
    private string _deploymentName = string.Empty;

    /// <summary>
    /// The desired replica count.
    /// </summary>
    [ObservableProperty]
    private int _replicas;

    /// <summary>
    /// The full image tag to apply to the first container.
    /// </summary>
    [ObservableProperty]
    private string _imageTag = string.Empty;

    /// <summary>
    /// Error message shown when validation fails in the dialog.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the user confirmed the dialog with valid inputs.
    /// </summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>
    /// Gets or sets an optional callback invoked by <see cref="ConfirmCommand"/> and
    /// <see cref="CancelCommand"/> to close the owning window.
    /// </summary>
    public Action? CloseCallback { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EditDeploymentDialogViewModel"/> class.
    /// </summary>
    /// <param name="deploymentName">The name of the deployment to display.</param>
    /// <param name="currentReplicas">The current replica count to pre-populate.</param>
    /// <param name="currentImageTag">The current image tag to pre-populate.</param>
    public EditDeploymentDialogViewModel(string deploymentName, int currentReplicas, string currentImageTag)
    {
        DeploymentName = deploymentName;
        Replicas = currentReplicas;
        ImageTag = currentImageTag;
    }

    /// <summary>
    /// Validates inputs and, if valid, sets <see cref="IsConfirmed"/> to <c>true</c> then closes the dialog.
    /// Sets <see cref="ErrorMessage"/> and does not close if validation fails.
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (Replicas < 0)
        {
            ErrorMessage = "Replica count must be 0 or greater.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ImageTag))
        {
            ErrorMessage = "Image tag must not be empty.";
            return;
        }

        ErrorMessage = string.Empty;
        IsConfirmed = true;
        CloseCallback?.Invoke();
    }

    /// <summary>
    /// Closes the dialog without confirming.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        CloseCallback?.Invoke();
    }
}
