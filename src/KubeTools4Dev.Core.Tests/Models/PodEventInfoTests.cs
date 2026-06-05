using k8s.Models;
using KubeTools4Dev.Core.Models;

namespace KubeTools4Dev.Core.Tests.Models;

/// <summary>
/// Tests for <see cref="PodEventInfo"/> mapping, ordering, and age formatting.
/// </summary>
public class PodEventInfoTests
{
    private static readonly DateTime Now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    private static Corev1Event MakeEvent(
        string? type = "Normal",
        string? reason = "Pulled",
        string? message = "Container image pulled",
        int? count = 1,
        DateTime? lastTimestamp = null,
        DateTime? eventTime = null,
        DateTime? firstTimestamp = null,
        DateTime? creationTimestamp = null) => new()
    {
        Type = type,
        Reason = reason,
        Message = message,
        Count = count,
        LastTimestamp = lastTimestamp,
        EventTime = eventTime,
        FirstTimestamp = firstTimestamp,
        Metadata = new V1ObjectMeta { CreationTimestamp = creationTimestamp },
        InvolvedObject = new V1ObjectReference { Kind = "Pod", Name = "test-pod" },
    };

    [Fact]
    public void FromEvent_MapsCoreFields()
    {
        var info = PodEventInfo.FromEvent(MakeEvent(
            type: "Warning", reason: "BackOff", message: "Back-off restarting", count: 7,
            lastTimestamp: Now.AddMinutes(-5)));

        Assert.Equal("Warning", info.Type);
        Assert.Equal("BackOff", info.Reason);
        Assert.Equal("Back-off restarting", info.Message);
        Assert.Equal(7, info.Count);
        Assert.Equal(Now.AddMinutes(-5), info.Timestamp);
        Assert.True(info.IsWarning);
    }

    [Fact]
    public void FromEvent_NullStrings_MapToEmpty()
    {
        var info = PodEventInfo.FromEvent(MakeEvent(type: null, reason: null, message: null));

        Assert.Equal(string.Empty, info.Type);
        Assert.Equal(string.Empty, info.Reason);
        Assert.Equal(string.Empty, info.Message);
        Assert.False(info.IsWarning);
    }

    [Fact]
    public void FromEvent_Timestamp_FallsBack_LastThenEventThenFirstThenCreation()
    {
        var last = Now.AddMinutes(-1);
        var evtTime = Now.AddMinutes(-2);
        var first = Now.AddMinutes(-3);
        var creation = Now.AddMinutes(-4);

        Assert.Equal(last, PodEventInfo.FromEvent(MakeEvent(
            lastTimestamp: last, eventTime: evtTime, firstTimestamp: first, creationTimestamp: creation)).Timestamp);
        Assert.Equal(evtTime, PodEventInfo.FromEvent(MakeEvent(
            eventTime: evtTime, firstTimestamp: first, creationTimestamp: creation)).Timestamp);
        Assert.Equal(first, PodEventInfo.FromEvent(MakeEvent(
            firstTimestamp: first, creationTimestamp: creation)).Timestamp);
        Assert.Equal(creation, PodEventInfo.FromEvent(MakeEvent(
            creationTimestamp: creation)).Timestamp);
        Assert.Null(PodEventInfo.FromEvent(MakeEvent()).Timestamp);
    }

    [Fact]
    public void FromEvent_Count_FallsBack_CountThenSeriesThenOne()
    {
        var withSeries = MakeEvent(count: null);
        withSeries.Series = new Corev1EventSeries { Count = 12 };
        Assert.Equal(12, PodEventInfo.FromEvent(withSeries).Count);

        Assert.Equal(1, PodEventInfo.FromEvent(MakeEvent(count: null)).Count);
    }

    [Fact]
    public void FromEvents_OrdersNewestFirst_TimestamplessLast()
    {
        var older = MakeEvent(reason: "Older", lastTimestamp: Now.AddHours(-2));
        var newer = MakeEvent(reason: "Newer", lastTimestamp: Now.AddMinutes(-1));
        var none = MakeEvent(reason: "NoTimestamp");

        var result = PodEventInfo.FromEvents([older, none, newer]);

        Assert.Equal(["Newer", "Older", "NoTimestamp"], result.Select(e => e.Reason).ToArray());
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(45, "45s")]
    [InlineData(60, "1m")]
    [InlineData(59 * 60, "59m")]
    [InlineData(60 * 60, "1h")]
    [InlineData(23 * 60 * 60, "23h")]
    [InlineData(24 * 60 * 60, "1d")]
    [InlineData(5 * 24 * 60 * 60, "5d")]
    public void FormatAge_FormatsKubectlStyle(int secondsAgo, string expected)
    {
        var info = PodEventInfo.FromEvent(MakeEvent(lastTimestamp: Now.AddSeconds(-secondsAgo)));
        Assert.Equal(expected, info.FormatAge(Now));
    }

    [Fact]
    public void FormatAge_NoTimestamp_ReturnsUnknown()
    {
        var info = PodEventInfo.FromEvent(MakeEvent());
        Assert.Equal("unknown", info.FormatAge(Now));
    }

    [Fact]
    public void FormatAge_FutureTimestamp_ClampsToZero()
    {
        var info = PodEventInfo.FromEvent(MakeEvent(lastTimestamp: Now.AddSeconds(30)));
        Assert.Equal("0s", info.FormatAge(Now));
    }
}
