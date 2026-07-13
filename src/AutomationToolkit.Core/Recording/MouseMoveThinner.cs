namespace AutomationToolkit.Core.Recording;

/// <summary>マウス移動の間引き判定を担当する</summary>
/// <remarks>
/// 前回記録位置から一定距離以上、または前回記録から一定時間以上で記録する。
/// 速いフリックは距離条件で形が残り、遅いドラッグは時間条件で滑らかさが残る
/// </remarks>
/// <param name="minDistancePx">記録する最小移動距離のピクセル数</param>
/// <param name="minIntervalTicks">記録する最小間隔の Stopwatch タイムスタンプ刻み</param>
public sealed class MouseMoveThinner(int minDistancePx, long minIntervalTicks) {
	/// <summary>基準位置が設定済みかどうか</summary>
	private bool hasLast;
	/// <summary>基準位置の X 座標</summary>
	private int lastX;
	/// <summary>基準位置の Y 座標</summary>
	private int lastY;
	/// <summary>基準位置を記録した時刻の Stopwatch タイムスタンプ</summary>
	private long lastTicks;

	/// <summary>このマウス移動を記録すべきかどうかを判定する</summary>
	/// <param name="x">カーソルの X 座標</param>
	/// <param name="y">カーソルの Y 座標</param>
	/// <param name="timestampTicks">イベント発生時刻の Stopwatch タイムスタンプ</param>
	/// <returns>記録すべきなら true</returns>
	public bool ShouldEmit(int x, int y, long timestampTicks) {
		if (hasLast == false) return true;
		long dx = x - lastX;
		long dy = y - lastY;
		if (dx * dx + dy * dy >= (long)minDistancePx * minDistancePx) return true;
		return timestampTicks - lastTicks >= minIntervalTicks;
	}

	/// <summary>移動・クリックなど位置を持つステップを記録したときに呼び、基準位置を更新する</summary>
	/// <param name="x">記録したステップの X 座標</param>
	/// <param name="y">記録したステップの Y 座標</param>
	/// <param name="timestampTicks">記録したステップの Stopwatch タイムスタンプ</param>
	public void MarkEmitted(int x, int y, long timestampTicks) {
		hasLast = true;
		lastX = x;
		lastY = y;
		lastTicks = timestampTicks;
	}
}
