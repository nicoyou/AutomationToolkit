namespace AutomationToolkit.Core.Models;

/// <summary>JSON マクロファイルのルートドキュメント</summary>
public sealed class Macro {
	/// <summary>このアプリが読み書きできる最新のスキーマバージョン</summary>
	public const int CURRENT_SCHEMA_VERSION = 1;

	/// <summary>ファイルのスキーマバージョン</summary>
	/// <remarks>将来の互換性判定に使う</remarks>
	public int schemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;
	/// <summary>マクロの表示名</summary>
	/// <remarks>ファイル名の元にもなる</remarks>
	public string name { get; set; } = string.Empty;
	/// <summary>マクロの作成日時</summary>
	public DateTimeOffset createdUtc { get; set; }
	/// <summary>再生順に並んだ操作ステップの一覧</summary>
	public List<MacroStep> steps { get; set; } = [];
}
