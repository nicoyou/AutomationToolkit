namespace AutomationToolkit.Core.Recording;

/// <summary>マクロ録画の動作設定</summary>
public sealed class RecordingOptions {
	/// <summary>マウス移動を記録する最小移動距離のピクセル数</summary>
	/// <remarks>前回記録位置からこれ以上離れたら記録する</remarks>
	public int moveMinDistancePx { get; set; } = 8;
	/// <summary>マウス移動を記録する最小間隔のミリ秒数</summary>
	/// <remarks>前回記録からこれ以上経過していたら距離に関係なく記録する</remarks>
	public int moveMinIntervalMs { get; set; } = 50;
	/// <summary>マウス移動を記録するかどうか</summary>
	/// <remarks>false ならクリック位置のみ記録される</remarks>
	public bool recordMouseMoves { get; set; } = true;
}
