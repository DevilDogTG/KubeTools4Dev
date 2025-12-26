using Avalonia;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace KubeTools4Dev;

class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Load Base Configuration
        var baseConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 2. Load User Settings (Manual/Direct Read to get values, as ConfigBuilder doesn't merge robustly across arbitrary files well without structure matching)
        // Actually, we can just load the file config IF we ensured the file has "Settings" section. 
        // But here we need to map Settings:General:LogLevel -> Serilog:MinimumLevel:Default

        var userSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KubeTools4Dev", "settings.json");
        var inMemoryOverrides = new System.Collections.Generic.Dictionary<string, string?>();

        // Apply Default LogPath from baseConfig
        var defaultLogPath = baseConfig["Settings:General:LogPath"];
        if (!string.IsNullOrEmpty(defaultLogPath))
        {
            inMemoryOverrides["Serilog:WriteTo:1:Args:path"] = defaultLogPath;
        }

        if (File.Exists(userSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(userSettingsPath);
                var settingsNode = System.Text.Json.Nodes.JsonNode.Parse(json);
                var generalParams = settingsNode?["General"];

                if (generalParams != null)
                {
                    var logLevel = generalParams["LogLevel"]?.ToString();
                    var logPath = generalParams["LogPath"]?.ToString();

                    if (!string.IsNullOrEmpty(logLevel))
                    {
                        inMemoryOverrides["Serilog:MinimumLevel:Default"] = logLevel;
                    }

                    if (!string.IsNullOrEmpty(logPath))
                    {
                        // Assuming File sink is index 1 based on appsettings.json "Using": [Console, File] and WriteTo array order.
                        // Ideally we'd find the File sink args, but simple override works for known structure.
                        inMemoryOverrides["Serilog:WriteTo:1:Args:path"] = logPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback/Ignore if settings corrupt, just log to console roughly or standard init
                Console.WriteLine($"Failed to read user settings for logging config: {ex.Message}");
            }
        }

        // 3. Build Final Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddInMemoryCollection(inMemoryOverrides);

        var configuration = builder.Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting KubeTools4Dev...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
