using KubeTools4Dev.ViewModels;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="LogLineClassifier"/> severity inference across common log formats.
/// </summary>
public class LogLineClassifierTests
{
    // Bracketed level (the app's own pods log like this):
    [Theory]
    [InlineData("[06/05/2026 07:15:18][Warning][Microsoft.AspNetCore.DataProtection] Using an in-memory repository.", LogSeverity.Warning)]
    [InlineData("[06/05/2026 07:15:18][Information][XmlKeyManager] Creating key {7d55b87e}", LogSeverity.Default)]
    [InlineData("[06/05/2026 07:15:18][Error][Some.Source] boom", LogSeverity.Error)]
    [InlineData("[06/05/2026 07:15:18][Debug][Some.Source] details", LogSeverity.Debug)]
    // Microsoft.Extensions.Logging console short forms:
    [InlineData("fail: Microsoft.AspNetCore.Server.Kestrel[13]", LogSeverity.Error)]
    [InlineData("crit: Program[0] unhandled", LogSeverity.Error)]
    [InlineData("warn: Microsoft.AspNetCore.HttpsPolicy[3]", LogSeverity.Warning)]
    [InlineData("info: Microsoft.Hosting.Lifetime[14]", LogSeverity.Default)]
    [InlineData("dbug: Microsoft.EntityFrameworkCore.Query[10101]", LogSeverity.Debug)]
    [InlineData("trce: Microsoft.AspNetCore.Routing[1]", LogSeverity.Debug)]
    // Serilog short forms:
    [InlineData("2026-06-05 07:15:18 [ERR] request failed", LogSeverity.Error)]
    [InlineData("2026-06-05 07:15:18 [FTL] terminating", LogSeverity.Error)]
    [InlineData("2026-06-05 07:15:18 [WRN] slow query", LogSeverity.Warning)]
    [InlineData("2026-06-05 07:15:18 [INF] started", LogSeverity.Default)]
    [InlineData("2026-06-05 07:15:18 [DBG] cache hit", LogSeverity.Debug)]
    [InlineData("2026-06-05 07:15:18 [VRB] raw frame", LogSeverity.Debug)]
    // logfmt / full words / other runtimes:
    [InlineData("level=error msg=\"connection refused\"", LogSeverity.Error)]
    [InlineData("level=warn msg=\"retrying\"", LogSeverity.Warning)]
    [InlineData("ERROR 2026-06-05 something broke", LogSeverity.Error)]
    [InlineData("FATAL: out of memory", LogSeverity.Error)]
    [InlineData("panic: runtime error: index out of range", LogSeverity.Error)]
    [InlineData("WARNING: deprecated flag", LogSeverity.Warning)]
    [InlineData("TRACE enter handler", LogSeverity.Debug)]
    // No level token:
    [InlineData("Connecting to log stream for my-pod...", LogSeverity.Default)]
    [InlineData("GET /healthz 200 3ms", LogSeverity.Default)]
    [InlineData("", LogSeverity.Default)]
    public void Classify_InfersSeverityFromCommonFormats(string line, LogSeverity expected)
        => Assert.Equal(expected, LogLineClassifier.Classify(line));

    [Fact]
    public void Classify_IgnoresTokensBeyondScanWindow()
    {
        var line = new string('x', LogLineClassifier.ScanWindow) + " error after the window";
        Assert.Equal(LogSeverity.Default, LogLineClassifier.Classify(line));
    }

    [Fact]
    public void Classify_ErrorWinsOverWarning_WhenBothPresent()
    {
        Assert.Equal(LogSeverity.Error, LogLineClassifier.Classify("warn: retry scheduled after error"));
    }
}
