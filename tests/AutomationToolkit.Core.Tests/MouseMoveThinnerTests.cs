using AutomationToolkit.Core.Recording;

namespace AutomationToolkit.Core.Tests;

/// <summary>MouseMoveThinner の間引き判定のテスト</summary>
public class MouseMoveThinnerTests {
	/// <summary>テストで使う最小記録間隔のタイムスタンプ刻み</summary>
	private const long INTERVAL_TICKS = 1000;
	/// <summary>テストで使う移動距離のしきい値 ( px )</summary>
	private const int DISTANCE_THRESHOLD = 8;
	/// <summary>基準位置とする X 座標</summary>
	private const int BASELINE_X = 100;
	/// <summary>基準位置とする Y 座標</summary>
	private const int BASELINE_Y = 100;
	/// <summary>基準位置とするタイムスタンプ</summary>
	private const long BASELINE_TIMESTAMP = 0;

	/// <summary>最初の移動は必ず記録される</summary>
	[Fact]
	public void FirstMove_IsEmitted() {
		var thinner = new MouseMoveThinner(DISTANCE_THRESHOLD, INTERVAL_TICKS);

		Assert.True(thinner.ShouldEmit(BASELINE_X, BASELINE_Y, BASELINE_TIMESTAMP));
	}

	/// <summary>間隔内の小さな移動は保留される</summary>
	[Fact]
	public void SmallMoveWithinInterval_IsHeld() {
		var thinner = new MouseMoveThinner(DISTANCE_THRESHOLD, INTERVAL_TICKS);
		thinner.MarkEmitted(BASELINE_X, BASELINE_Y, BASELINE_TIMESTAMP);

		// 距離・経過時間ともにしきい値未満
		Assert.False(thinner.ShouldEmit(103, 103, 500));
	}

	/// <summary>距離しきい値以上の移動は記録される</summary>
	[Fact]
	public void MoveBeyondDistanceThreshold_IsEmitted() {
		var thinner = new MouseMoveThinner(DISTANCE_THRESHOLD, INTERVAL_TICKS);
		thinner.MarkEmitted(BASELINE_X, BASELINE_Y, BASELINE_TIMESTAMP);

		// 距離がしきい値ちょうど
		Assert.True(thinner.ShouldEmit(108, 100, 100));
	}

	/// <summary>間隔以上経過した移動は距離が小さくても記録される</summary>
	[Fact]
	public void SlowDragBeyondInterval_IsEmitted() {
		var thinner = new MouseMoveThinner(DISTANCE_THRESHOLD, INTERVAL_TICKS);
		thinner.MarkEmitted(BASELINE_X, BASELINE_Y, BASELINE_TIMESTAMP);

		// 距離が小さくても間隔経過で記録される
		Assert.True(thinner.ShouldEmit(101, 100, INTERVAL_TICKS));
	}

	/// <summary>MarkEmitted を呼ぶと間引き判定の基準位置が更新される</summary>
	[Fact]
	public void MarkEmitted_UpdatesBaseline() {
		var thinner = new MouseMoveThinner(DISTANCE_THRESHOLD, INTERVAL_TICKS);
		thinner.MarkEmitted(BASELINE_X, BASELINE_Y, BASELINE_TIMESTAMP);
		thinner.MarkEmitted(200, 200, 100);

		// 直前の MarkEmitted 呼び出しの座標が基準になる
		Assert.False(thinner.ShouldEmit(203, 200, 200));
	}
}
