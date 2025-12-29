using KubeTools4Dev.Core.Models;
using System.Collections.Generic;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Interface for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the general settings.
    /// </summary>
    GeneralSettings General { get; }

    /// <summary>
    /// Gets or sets the pods settings.
    /// </summary>
    PodsSettings Pods { get; }

    /// <summary>
    /// Gets or sets the services settings.
    /// </summary>
    ServicesSettings Services { get; }
    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    void Save();

    /// <summary>
    /// Occurs when [settings changed].
    /// </summary>
    event Action SettingsChanged;

    /// <summary>
    /// Gets the default log path from appsettings.json.
    /// </summary>
    /// <returns>The default log path.</returns>
    string GetDefaultLogPath();
}
