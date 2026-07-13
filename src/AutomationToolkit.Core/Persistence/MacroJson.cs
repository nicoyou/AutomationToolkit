using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationToolkit.Core.Persistence;

/// <summary>マクロファイル用の JsonSerializerOptions を一元管理する</summary>
public static class MacroJson {
	/// <summary>通常使用する共有インスタンス</summary>
	/// <remarks>camelCase の予約語と衝突するため、プロパティ名のみ PascalCase を維持する</remarks>
	public static JsonSerializerOptions Default { get; } = CreateOptions();

	/// <summary>インデント付き・camelCase・enum 文字列化のマクロファイル用設定を生成する</summary>
	/// <returns>マクロファイル用の JsonSerializerOptions</returns>
	public static JsonSerializerOptions CreateOptions() => new() {
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() },
	};
}
