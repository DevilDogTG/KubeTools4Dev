using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace KubeTools4Dev.Services;

public interface ISettingsService
{
    int RefreshIntervalSeconds { get; set; }
    List<string> ExcludedServices { get; set; }
    void Save();
}

public class SettingsService : ISettingsService
{
    private const string FolderName = "ElysianMonitor";
    private const string FileName = "settings.json";
    private readonly string _filePath;

    public int RefreshIntervalSeconds { get; set; } = 5;
    public List<string> ExcludedServices { get; set; } = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, FolderName);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _filePath = Path.Combine(folder, FileName);
        Load();
    }

    private void Load()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<SettingsModel>(json);
                if (settings != null)
                {
                    RefreshIntervalSeconds = settings.RefreshIntervalSeconds;
                    ExcludedServices = settings.ExcludedServices ?? new();
                }
            }
            catch 
            { 
                // Ignore load errors, use defaults 
            }
        }
    }

    public void Save()
    {
        try
        {
            var settings = new SettingsModel 
            { 
                RefreshIntervalSeconds = RefreshIntervalSeconds,
                ExcludedServices = ExcludedServices
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch 
        { 
            // Ignore save errors 
        }
    }

    private class SettingsModel
    {
        public int RefreshIntervalSeconds { get; set; }
        public List<string> ExcludedServices { get; set; }
    }
}
