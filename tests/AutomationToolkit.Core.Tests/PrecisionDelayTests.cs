using System.Diagnostics;
using AutomationToolkit.Core.Playback;

namespace AutomationToolkit.Core.Tests;

/// <summary>PrecisionDelay の待機精度のテスト</summary>
public class PrecisionDelayTests
{
    /// <summary>指定した待機時間とほぼ同じ時間だけ待機する</summary>
    [Fact]
    public async Task WaitUntilAsync_WaitsApproximatelyTargetDuration()
    {
        using var highRes = PrecisionDelay.BeginHighResolutionTimers();
        var start = Stopwatch.GetTimestamp();
        var target = start + PrecisionDelay.MsToTimestampTicks(100);

        await PrecisionDelay.WaitUntilAsync(target, CancellationToken.None);

        var elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        Assert.InRange(elapsedMs, 99, 140); // 早すぎは NG、負荷による多少の遅れは許容
    }

    /// <summary>過去の時刻を指定すると即座に戻る</summary>
    [Fact]
    public async Task WaitUntilAsync_PastTarget_ReturnsImmediately()
    {
        var past = Stopwatch.GetTimestamp() - PrecisionDelay.MsToTimestampTicks(50);
        var start = Stopwatch.GetTimestamp();

        await PrecisionDelay.WaitUntilAsync(past, CancellationToken.None);

        var elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        Assert.InRange(elapsedMs, 0, 20);
    }

    /// <summary>待機中にキャンセルすると OperationCanceledException が発生する</summary>
    [Fact]
    public async Task WaitUntilAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource(30);
        var target = Stopwatch.GetTimestamp() + PrecisionDelay.MsToTimestampTicks(5000);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PrecisionDelay.WaitUntilAsync(target, cts.Token));
    }

    /// <summary>1000ms の変換結果が Stopwatch.Frequency と一致する</summary>
    [Fact]
    public void MsToTimestampTicks_RoundTripsWithFrequency()
    {
        var ticks = PrecisionDelay.MsToTimestampTicks(1000);

        Assert.Equal(Stopwatch.Frequency, ticks);
    }
}
