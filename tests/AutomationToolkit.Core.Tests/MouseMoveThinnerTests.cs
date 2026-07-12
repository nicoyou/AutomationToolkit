using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.Core.Tests;

/// <summary>MouseMoveThinner の間引き判定のテスト</summary>
public class MouseMoveThinnerTests
{
    /// <summary>テストで使う最小記録間隔のタイムスタンプ刻み</summary>
    private const long IntervalTicks = 1000;

    /// <summary>最初の移動は必ず記録される</summary>
    [Fact]
    public void FirstMove_IsEmitted()
    {
        var thinner = new MouseMoveThinner(8, IntervalTicks);

        Assert.True(thinner.ShouldEmit(100, 100, 0));
    }

    /// <summary>間隔内の小さな移動は保留される</summary>
    [Fact]
    public void SmallMoveWithinInterval_IsHeld()
    {
        var thinner = new MouseMoveThinner(8, IntervalTicks);
        thinner.MarkEmitted(100, 100, 0);

        Assert.False(thinner.ShouldEmit(103, 103, 500)); // 距離 ~4.2px, 経過 500 < 1000
    }

    /// <summary>距離しきい値以上の移動は記録される</summary>
    [Fact]
    public void MoveBeyondDistanceThreshold_IsEmitted()
    {
        var thinner = new MouseMoveThinner(8, IntervalTicks);
        thinner.MarkEmitted(100, 100, 0);

        Assert.True(thinner.ShouldEmit(108, 100, 100)); // 距離 8px ちょうど
    }

    /// <summary>間隔以上経過した移動は距離が小さくても記録される</summary>
    [Fact]
    public void SlowDragBeyondInterval_IsEmitted()
    {
        var thinner = new MouseMoveThinner(8, IntervalTicks);
        thinner.MarkEmitted(100, 100, 0);

        Assert.True(thinner.ShouldEmit(101, 100, IntervalTicks)); // 距離 1px でも時間経過で記録
    }

    /// <summary>MarkEmitted を呼ぶと間引き判定の基準位置が更新される</summary>
    [Fact]
    public void MarkEmitted_UpdatesBaseline()
    {
        var thinner = new MouseMoveThinner(8, IntervalTicks);
        thinner.MarkEmitted(100, 100, 0);
        thinner.MarkEmitted(200, 200, 100);

        Assert.False(thinner.ShouldEmit(203, 200, 200)); // 基準は (200,200)
    }
}
